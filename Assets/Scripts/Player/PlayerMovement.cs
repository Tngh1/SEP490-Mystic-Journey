using Fusion;
using UnityEngine;

/// <summary>
/// 2D Rigidbody-based movement executor. Receives a movement vector from
/// <see cref="NetworkPlayer"/> each network tick and applies it via Rigidbody2D.
///
/// This component owns the Rigidbody2D and the physics-driven position update.
/// It does NOT:
///   - Spawn or despawn anything.
///   - Apply movement for remote players. Movement is replicated to remotes
///     via the NetworkTransform component on the same GameObject.
///
/// Single-player fallback:
///   When the GameObject is NOT a Fusion NetworkObject (e.g. it was instantiated
///   directly by <see cref="PlayerSpawner"/> without going through Fusion),
///   <see cref="PlayerMovement"/> reads WASD from the new Input System itself
///   in <see cref="Update"/> and moves the body locally. This keeps the player
///   controllable in Phase 1 builds before multiplayer is fully wired.
///
/// Singleton note (multiplayer migration):
///   - The static <see cref="Instance"/> accessor is preserved for backward
///     compatibility with single-player code (PlayerCombat.LastMove,
///     EnemyBehaviour target lookup, QuestWaypointArrow).
///   - In a multi-player session, <see cref="Instance"/> will only ever point
///     to ONE player (the most recently spawned input-authority one). This is
///     acceptable for the legacy single-player AI / aim callers because they
///     only care about the LOCAL player.
///
/// Lifetime: lives on the PlayerNetwork prefab root. Constructed by Unity,
/// never instantiated manually.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [Tooltip("Base movement speed in world units per second. The actual speed " +
             "may be scaled by PlayerEntity.MoveSpeed in a future phase.")]
    [SerializeField] private float baseMoveSpeed = 4f;

    [Tooltip("If true and this GameObject has no NetworkObject, read WASD directly " +
             "and move the body locally. Disable only when a different script owns " +
             "local input for this player.")]
    [SerializeField] private bool fallbackLocalInput = true;

    private float _currentMoveSpeed;

    private Rigidbody2D _rb;
    private PlayerAnimation _animation;

    // Single source of truth for gameplay input. In the offline fallback path we
    // read the movement vector from here instead of the Input System / keyboard
    // directly, so movement always honours the player's rebindings.
    private GameplayInputProvider _input;

    // The last input vector — used by PlayerAnimation for facing direction.
    private Vector2 _moveInput;
    private Vector2 _lastMove = Vector2.down;

    // ─────────────────────────────────────────────────────────────────────────
    // Singleton (legacy — see class doc)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Legacy single-player singleton accessor. Holds the most recently spawned
    /// input-authority <see cref="PlayerMovement"/>. Will be replaced by a
    /// PlayerRegistry lookup in a later phase.
    /// </summary>
    public static PlayerMovement Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Public read-only state
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The most recent movement vector received from the network.</summary>
    public Vector2 MoveInput => _moveInput;

    /// <summary>Last non-zero movement direction. Used for sprite facing.</summary>
    public Vector2 LastMove => _lastMove;

    /// <summary>True when the player is currently issuing a movement command.</summary>
    public bool IsMoving => _moveInput.sqrMagnitude > 0.01f;

    /// <summary>Current movement speed (units/sec). Read-only from outside.</summary>
    public float CurrentMoveSpeed => _currentMoveSpeed;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animation = GetComponent<PlayerAnimation>();

        // Resolve (or add) the shared input provider on this GameObject. It owns
        // all InputAction reading; PlayerMovement never touches the Input System
        // or the keyboard directly.
        _input = GetComponent<GameplayInputProvider>();
        if (_input == null) _input = gameObject.AddComponent<GameplayInputProvider>();

        if (_rb == null)
        {
            Debug.LogError("[PlayerMovement.Awake] Rigidbody2D NOT FOUND on this GameObject. " +
                           "Player CANNOT move. Add a Rigidbody2D component (Body Type = Kinematic).");
        }
        else
        {
            Debug.Log($"[PlayerMovement.Awake] Rigidbody2D found. bodyType={_rb.bodyType} " +
                      $"gravityScale={_rb.gravityScale}");
        }

        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        // Must be None, not Interpolate. ApplyRaw() below writes transform.position
        // directly every Fusion tick (a teleport-style write, not a physics step), so
        // there is no sequence of physics-simulation snapshots for Interpolate to
        // smooth between — it would just add a frame of lag for no benefit.
        _rb.interpolation = RigidbodyInterpolation2D.None;

        _currentMoveSpeed = baseMoveSpeed;
    }

    public override void Spawned()
    {
        // Only the local input-authority player is the canonical "Instance"
        // for legacy single-player callers (Combat aim, AI target, quest arrow).
        if (Object != null && Object.HasInputAuthority)
        {
            Instance = this;
        }
        else if (Object == null)
        {
            // Single-player fallback (no Fusion NetworkObject).
            Instance = this;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // Only the fallback path needs to run in Update. When Fusion is driving
        // movement (Object != null), NetworkPlayer.FixedUpdateNetwork handles input.
        if (!fallbackLocalInput) return;
        if (_rb == null) return;
        if (Object != null && HasInputAuthority)
        {
            // Fusion tick will set _moveInput via Move() this frame; skip fallback.
            return;
        }
        if (Object != null && !HasInputAuthority)
        {
            // Remote player — do not move locally; NetworkTransform handles it.
            return;
        }

        // No NetworkObject at all → fall back to direct keyboard input.
        // NetworkPlayer.Render() drives animation from the network state, but that
        // component doesn't exist yet in this mode, so we drive it directly here —
        // otherwise the offline player moves with no walk animation.
        var input = ReadMoveFallback();
        if (input == Vector2.zero && _moveInput != Vector2.zero)
        {
            // Player released keys — commit zero so animation settles.
            _moveInput = Vector2.zero;
            ApplyRaw(Vector2.zero, Time.deltaTime);
            if (_animation != null) _animation.SetMovement(Vector2.zero, true);
            return;
        }

        if (input == Vector2.zero) return;
        ApplyRaw(input, Time.deltaTime);
        if (_animation != null) _animation.SetMovement(input, true);
    }

    private Vector2 ReadMoveFallback()
    {
        // Delegate to the single source of truth. The provider reads the same
        // InputAction that ControlRebindManager writes overrides onto, so the
        // offline path respects rebindings and never reads raw Keyboard.current
        // WASD (which would ignore a rebound Move key — the original bug).
        return _input != null ? _input.Move : Vector2.zero;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Network-driven entry point
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply a movement input for this tick. Called by <see cref="NetworkPlayer"/>
    /// on the input-authority client only. Remote players receive movement via
    /// the NetworkTransform component instead.
    /// </summary>
    /// <param name="input">Normalized 2D direction vector (already clamped to magnitude 1).</param>
    /// <param name="deltaTime">Simulation tick delta (Runner.DeltaTime).</param>
    public void Move(Vector2 input, float deltaTime)
    {
        if (Object == null)
        {
            // Single-player mode (player spawned without Fusion). Apply directly.
            ApplyRaw(input, deltaTime);
            return;
        }
        if (!HasInputAuthority)
        {
            Debug.LogWarning($"[PlayerMovement.Move] BLOCKED — no input authority. " +
                             $"HasInputAuth={HasInputAuthority} input={input} " +
                             $"InputAuth={Object.InputAuthority} StateAuth={Object.StateAuthority} " +
                             $"LocalPlayer={Runner?.LocalPlayer}");
            return;
        }
        if (deltaTime <= 0f) return;
        if (_rb == null)
        {
            Debug.LogError("[PlayerMovement.Move] Rigidbody2D is NULL — prefab is missing Rigidbody2D!");
            return;
        }

        ApplyRaw(input, deltaTime);
    }

    /// <summary>
    /// Move the Rigidbody2D by <paramref name="input"/> * speed * dt. Used both by
    /// the network path (<see cref="Move"/>) and the single-player fallback in
    /// <see cref="Update"/>.
    /// </summary>
    private void ApplyRaw(Vector2 input, float deltaTime)
    {
        if (_rb == null) return;

        _moveInput = input;
        if (_moveInput != Vector2.zero)
        {
            _lastMove = _moveInput.normalized;
        }

        if (input.sqrMagnitude > 0.01f)
        {
            // Assign position directly rather than Rigidbody2D.MovePosition(). MovePosition
            // queues the move for Unity's next physics step, which runs on its own FixedUpdate
            // cadence — out of step with Fusion's FixedUpdateNetwork tick. NetworkTransform reads
            // transform.position right after this call, so a queued MovePosition is invisible to
            // it and movement never replicates (or appears to not move at all).
            //
            // Write transform.position (not rb.position). Physics2D.SyncTransforms() copies
            // Transform -> physics engine, never the other way around — writing rb.position and
            // then calling SyncTransforms() leaves transform.position (and therefore the
            // SpriteRenderer and NetworkTransform's replication sample) permanently stale until
            // Unity's own FixedUpdate physics step happens to run on its own cadence. Confirmed
            // empirically: 5 consecutive rb.position writes + SyncTransforms() never moved
            // transform.position once. Writing transform.position directly + SyncTransforms()
            // (to keep the Rigidbody2D's internal position in sync for collision queries) updates
            // both instantly every call.
            transform.position += (Vector3)(_moveInput * _currentMoveSpeed * deltaTime);
            Physics2D.SyncTransforms();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Speed control
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Override the current movement speed. Pass 0 or negative to revert to
    /// the inspector-configured base speed.
    /// </summary>
    public void SetMoveSpeedOverride(float speed)
    {
        _currentMoveSpeed = speed > 0f ? speed : baseMoveSpeed;
    }
}