using Fusion;
using UnityEngine;

// Executes network behaviour operation.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [Tooltip("Base movement speed in world units per second. The actual speed " +
             "may be scaled by PlayerEntity.MoveSpeed in a future phase.")]
    [SerializeField] private float baseMoveSpeed = 3.75f;

    [Tooltip("If true and this GameObject has no NetworkObject, read WASD directly " +
             "and move the body locally. Disable only when a different script owns " +
             "local input for this player.")]
    [SerializeField] private bool fallbackLocalInput = true;

    private float _currentMoveSpeed;

    private Rigidbody2D _rb;
    private PlayerAnimation _animation;

    private GameplayInputProvider _input;

    private Vector2 _moveInput;
    private Vector2 _lastMove = Vector2.down;


    // Executes instance operation.
    public static PlayerMovement Instance { get; private set; }


    // Executes move input operation.
    public Vector2 MoveInput => _moveInput;

    // Executes last move operation.
    public Vector2 LastMove => _lastMove;

    // Executes is moving operation.
    public bool IsMoving => _moveInput.sqrMagnitude > 0.01f;

    // Executes current move speed operation.
    public float CurrentMoveSpeed => _currentMoveSpeed;


    // Initializes internal component caches and dependencies for PlayerMovement upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>(); // Cache Rigidbody2D reference for velocity-based movement application
        _animation = GetComponent<PlayerAnimation>(); // Cache animation component to sync movement direction visuals

        _input = GetComponent<GameplayInputProvider>(); // Try to find existing input provider on this GameObject
        if (_input == null) _input = gameObject.AddComponent<GameplayInputProvider>(); // Auto-add input provider if missing — avoids null reference on first frame

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

        _rb.gravityScale = 0f; // Disable Unity gravity — this is a top-down 2D game, no falling
        _rb.freezeRotation = true; // Prevent physics engine from rotating the player body
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Enable position interpolation for smooth visual motion between fixed updates

        _currentMoveSpeed = baseMoveSpeed; // Initialize runtime speed from serialized inspector value
    }

    // Fusion lifecycle callback invoked when this PlayerMovement NetworkObject is spawned into the network session.
    // Configures input/state authority handlers, sets singleton references if local player, and applies initial visuals.
    public override void Spawned()
    {
        if (Object != null && Object.HasInputAuthority)
        {
            Instance = this;
        }
        else if (Object == null)
        {
            Instance = this;
        }
    }

    // Fusion lifecycle callback invoked when this PlayerMovement NetworkObject is despawned from the network session.
    // Performs teardown, unregisters network listeners, and cleans up singleton references.
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
        {
            MysticJourney.Core.Services.AudioManager.Instance?.StopWalking();
            Instance = null;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            MysticJourney.Core.Services.AudioManager.Instance?.StopWalking();
    }

    // Per-frame update loop for PlayerMovement.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (!fallbackLocalInput) return;
        if (_rb == null) return;
        if (Object != null && HasInputAuthority)
        {
            return;
        }
        if (Object != null && !HasInputAuthority)
        {
            return;
        }

        bool isAlive = true;
        if (PlayerEntity.Instance != null && PlayerEntity.Instance.CurrentHealth <= 0)
        {
            isAlive = false; // Player is dead — zero out movement input to prevent corpse walking
        }

        var input = ReadMoveFallback(); // Sample WASD/joystick input from the fallback local input provider
        if (!isAlive) input = Vector2.zero; // Dead players cannot move

        if (input == Vector2.zero && _moveInput != Vector2.zero)
        {
            _moveInput = Vector2.zero;
            ApplyRaw(Vector2.zero, Time.deltaTime); // Zero velocity when player stops moving
            if (_animation != null) _animation.SetMovement(Vector2.zero, isAlive); // Transition to idle animation
            return;
        }

        if (input == Vector2.zero)
        {
            ApplyRaw(Vector2.zero, Time.deltaTime);
            if (_animation != null) _animation.SetMovement(Vector2.zero, isAlive);
            return;
        }

        ApplyRaw(input, Time.deltaTime); // Apply velocity vector to Rigidbody2D
        if (_animation != null) _animation.SetMovement(input, isAlive); // Update animation blend tree with movement direction
    }

    // Executes read move fallback operation.
    private Vector2 ReadMoveFallback()
    {
        return _input != null ? _input.Move : Vector2.zero;
    }


    // Executes move operation.
    public void Move(Vector2 input, float deltaTime)
    {
        if (Object == null)
        {
            ApplyRaw(input, deltaTime);
            return;
        }
        if (!HasInputAuthority) return;
        if (deltaTime <= 0f) return;
        if (_rb == null)
        {
            Debug.LogError("[PlayerMovement.Move] Rigidbody2D is NULL — prefab is missing Rigidbody2D!");
            return;
        }

        ApplyRaw(input, deltaTime);
    }

    private float _rootTimer = 0f;

    // Executes apply root operation.
    public void ApplyRoot(float duration, bool stackDuration = true, float maxCap = 5f)
    {
        var combat = GetComponent<PlayerCombat>();
        var buffMgr = GetComponent<BuffManager>();
        if ((combat != null && combat.IsDebuffImmune) || (buffMgr != null && buffMgr.IsStatusImmune))
        {
            // Player has debuff immunity — show immunity popup and skip rooting
            if (DamagePopupManager.Instance != null)
            {
                DamagePopupManager.Instance.CreateText(transform.position, "Immunity", Color.cyan);
            }
            return;
        }

        if (stackDuration)
        {
            _rootTimer = Mathf.Min(_rootTimer + duration, maxCap); // Stack root duration capped at maxCap seconds
        }
        else if (duration > _rootTimer)
        {
            _rootTimer = duration; // Only extend root if new duration is longer than remaining
        }
    }

    // Executes apply raw operation.
    private void ApplyRaw(Vector2 input, float deltaTime)
    {
        if (_rb == null) return;

        if (_rootTimer > 0f)
        {
            _rootTimer -= deltaTime; // Count down root duration each fixed frame
            _moveInput = Vector2.zero; // Zero input so IsMoving returns false while rooted
            _rb.linearVelocity = Vector2.zero; // Hard-stop velocity while rooted
            MysticJourney.Core.Services.AudioManager.Instance?.StopWalking();
            if (_animation != null) _animation.SetMovement(Vector2.zero, true); // Force idle animation during root
            return;
        }

        _moveInput = input; // Store current frame's movement direction for external readers (e.g., combat aim)
        if (_moveInput != Vector2.zero)
        {
            _lastMove = _moveInput.normalized; // Cache last non-zero direction for facing-direction-dependent attacks
        }

        _rb.linearVelocity = input.sqrMagnitude > 0.01f
            ? _moveInput * _currentMoveSpeed // Apply directional velocity scaled by current move speed
            : Vector2.zero; // Zero velocity when there's no input (prevents drift)

        if (input.sqrMagnitude > 0.01f)
        {
            MysticJourney.Core.Services.AudioManager.Instance?.PlayWalking();
        }
        else
        {
            MysticJourney.Core.Services.AudioManager.Instance?.StopWalking();
        }
    }


    // Executes set move speed override operation.
    public void SetMoveSpeedOverride(float speed)
    {
        _currentMoveSpeed = speed > 0f ? speed : baseMoveSpeed; // Apply override speed, falling back to base if zero or negative
    }
}
