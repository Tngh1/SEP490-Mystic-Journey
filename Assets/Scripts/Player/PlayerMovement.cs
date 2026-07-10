using Fusion;
using UnityEngine;

/// <summary>
/// 2D Rigidbody-based movement executor. Receives a movement vector from
/// <see cref="NetworkPlayer"/> each network tick and applies it via Rigidbody2D.
///
/// This component owns the Rigidbody2D and the physics-driven position update.
/// It does NOT:
///   - Read Unity Input directly. All input flows through NetworkInputData
///     collected by LocalInputCollector and dispatched by NetworkPlayer.
///   - Spawn or despawn anything.
///   - Apply movement for remote players. Movement is replicated to remotes
///     via the NetworkTransform component on the same GameObject.
///
/// Singleton note (multiplayer migration):
///   - The static <see cref="Instance"/> accessor is preserved for backward
///     compatibility with single-player code (PlayerCombat.LastMove,
///     EnemyBehaviour target lookup, QuestWaypointArrow).
///   - In a multi-player session, <see cref="Instance"/> will only ever point
///     to ONE player (the most recently spawned input-authority one). This is
///     acceptable for the legacy single-player AI / aim callers because they
///     only care about the LOCAL player.
///   - A proper PlayerRegistry keyed by PlayerRef will replace this pattern
///     in a later phase (Combat refactor).
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

    private float _currentMoveSpeed;

    private Rigidbody2D _rb;

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
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        _currentMoveSpeed = baseMoveSpeed;
    }

    public override void Spawned()
    {
        // Only the local input-authority player is the canonical "Instance"
        // for legacy single-player callers (Combat aim, AI target, quest arrow).
        if (Object.HasInputAuthority)
        {
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
        if (!HasInputAuthority)
        {
            Debug.LogWarning($"[PlayerMovement.Move] BLOCKED — no input authority. " +
                             $"HasInputAuth={HasInputAuthority} input={input}");
            return;
        }
        if (deltaTime <= 0f) return;
        if (_rb == null)
        {
            Debug.LogError("[PlayerMovement.Move] Rigidbody2D is NULL — prefab is missing Rigidbody2D!");
            return;
        }

        _moveInput = input;
        if (_moveInput != Vector2.zero)
        {
            _lastMove = _moveInput.normalized;
        }

        Debug.Log($"[PlayerMovement.Move] BEFORE pos={_rb.position} input={input} " +
                  $"speed={_currentMoveSpeed} dt={deltaTime}");
        _rb.MovePosition(_rb.position + _moveInput * _currentMoveSpeed * deltaTime);
        Debug.Log($"[PlayerMovement.Move] AFTER pos={_rb.position}");
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