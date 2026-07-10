using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerEntity))]
public class NetworkPlayer : NetworkBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector — character config
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Spawn")]
    [Tooltip("World position players of this prefab will spawn at. " +
             "Phase 2 will replace this with a SpawnPointManager lookup.")]
    [SerializeField] private Vector3 defaultSpawnPosition = Vector3.zero;

    [Header("Character Visual")]
    [Tooltip("Factory responsible for instantiating Archer / Knight / Mage visuals under VisualRoot.")]
    [SerializeField] private CharacterFactory characterFactory;

    [Tooltip("Empty child GameObject that will hold the character visual. " +
             "If null, one named 'VisualRoot' is auto-created at runtime.")]
    [SerializeField] private Transform visualRoot;

    // ─────────────────────────────────────────────────────────────────────────
    // Networked state
    // ─────────────────────────────────────────────────────────────────────────

    [Networked] public int PlayerProfileId { get; set; }
    [Networked] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public int Level { get; set; }
    [Networked] public int PlayerClass { get; set; }

    [Networked] public int CurrentHp { get; set; }
    [Networked] public int MaxHp { get; set; }
    [Networked] public NetworkBool IsAlive { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Local references
    // ─────────────────────────────────────────────────────────────────────────

    private PlayerMovement _movement;
    private PlayerCombat _combat;
    private PlayerEntity _entity;
    private PlayerAnimation _animation;
    private PlayerInput _playerInput;

    private GameObject _spawnedVisual;
    private NetworkButtons _previousButtons;

    // Last input move sent — cached so Render() can drive animation smoothly
    // between FixedUpdateNetwork ticks.
    private Vector2 _lastSentMove;

    // ─────────────────────────────────────────────────────────────────────────
    // Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised on every client after Spawned completes (visual is ready).</summary>
    public event Action<NetworkPlayer> OnPlayerReady;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Convenience accessor for UI/Party code that needs the PlayerRef.</summary>
    public PlayerRef Player => Object.InputAuthority;

    /// <summary>Returns the live visual GameObject for this player, or null before Spawned.</summary>
    public GameObject VisualObject => _spawnedVisual;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _combat = GetComponent<PlayerCombat>();
        _entity = GetComponent<PlayerEntity>();
        _animation = GetComponent<PlayerAnimation>();
        _playerInput = GetComponent<PlayerInput>();

        if (visualRoot == null)
        {
            var found = transform.Find("VisualRoot");
            if (found != null)
            {
                visualRoot = found;
            }
            else
            {
                var go = new GameObject("VisualRoot");
                go.transform.SetParent(transform, worldPositionStays: false);
                visualRoot = go.transform;
            }
        }
    }

    private void OnDestroy()
    {
        if (_spawnedVisual != null)
        {
            Destroy(_spawnedVisual);
            _spawnedVisual = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fusion lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public override void Spawned()
    {
        Debug.Log($"[NetworkPlayer] Spawned. InputAuthority={Object.InputAuthority}, " +
                  $"StateAuthority={Object.StateAuthority} class={PlayerClass}");

        if (_playerInput != null)
        {
            _playerInput.enabled = Object.HasInputAuthority;
        }

        if (Object.HasStateAuthority)
        {
            // Assign this player's class from WorldState (server-authoritative so all clients see the same value).
            string className = WorldState.PlayerClass ?? "Knight";
            if (!Enum.TryParse<CharacterClass>(className, ignoreCase: false, out var parsed))
                parsed = CharacterClass.Knight;
            PlayerClass = (int)parsed;
            PlayerName = WorldState.PlayerName ?? "Player";
            PlayerProfileId = WorldState.PlayerProfileId;
            Level = Mathf.Max(1, WorldState.PlayerLevel);

            // Spread spawn positions so multiple players don't stack at origin.
            int playerIndex = Mathf.Max(0, Object.InputAuthority.PlayerId - 1);
            float angle = playerIndex * 60f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 2.5f;
            transform.position = defaultSpawnPosition + offset;

            IsAlive = true;
            if (MaxHp <= 0) MaxHp = 100;
            CurrentHp = MaxHp;
        }

        if (characterFactory != null)
        {
            _spawnedVisual = characterFactory.Create((CharacterClass)PlayerClass, visualRoot);
        }
        else
        {
            // Fallback so the player is at least visible during Phase 1 wiring.
            _spawnedVisual = CreateFallbackVisual((CharacterClass)PlayerClass, visualRoot);
        }

        OnPlayerReady?.Invoke(this);
    }

    private static GameObject CreateFallbackVisual(CharacterClass characterClass, Transform parent)
    {
        var go = new GameObject($"Visual_{characterClass}_Fallback");
        if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSolidSprite(64, 64, ClassColor(characterClass));
        sr.sortingOrder = 10;

        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        Debug.Log($"[NetworkPlayer] Created fallback visual for class {characterClass} " +
                  $"(color={ClassColor(characterClass)}) — wire CharacterFactory to use proper sprites.");
        return go;
    }

    private static Color ClassColor(CharacterClass c)
    {
        switch (c)
        {
            case CharacterClass.Archer: return new Color(0.30f, 0.85f, 0.30f); // green
            case CharacterClass.Mage:   return new Color(0.45f, 0.35f, 0.95f); // purple
            case CharacterClass.Knight:
            default:                    return new Color(0.95f, 0.70f, 0.20f); // gold
        }
    }

    private static Sprite CreateSolidSprite(int width, int height, Color color)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, width, height),
                              new Vector2(0.5f, 0.5f), 64f);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_spawnedVisual != null)
        {
            Destroy(_spawnedVisual);
            _spawnedVisual = null;
        }
    }

    /// <summary>
    /// Render-phase callback. Runs every Unity Update after simulation has settled.
    /// Drives animation and other per-frame visual state from the latest network
    /// values (movement, alive/dead).
    /// </summary>
    public override void Render()
    {
        if (_animation != null)
        {
            _animation.SetMovement(_lastSentMove, IsAlive);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // [DEBUG-MOVE] tick heartbeat
        if (Time.frameCount % 30 == 0)
            Debug.Log($"[NetPlayer/FixedUpdateNet] tick frame={Time.frameCount} " +
                      $"HasInputAuth={HasInputAuthority} IsAlive={IsAlive} Runner={Runner?.Stage}");

        if (!HasInputAuthority) return;

        if (!IsAlive)
        {
            _movement.Move(Vector2.zero, Runner.DeltaTime);
            return;
        }

        var input = GetInput<NetworkInputData>();
        if (!input.HasValue)
        {
            // [DEBUG-MOVE] input missing — common on first ticks before OnInput fires
            if (Time.frameCount % 60 == 0) Debug.LogWarning("[NetPlayer] GetInput returned null");
            return;
        }
        var inputData = input.Value;

        Debug.Log($"[NetPlayer/Move] move={inputData.Move} dt={Runner.DeltaTime} " +
                  $"moveScript={(_movement != null ? "OK" : "NULL")}");

        _lastSentMove = inputData.Move;
        _movement.Move(inputData.Move, Runner.DeltaTime);

        var buttons = inputData.Buttons;

        if (buttons.IsSet(InputButtons.Attack))
        {
            _combat.RequestAttack(inputData.AimWorldPosition);
        }
        if (buttons.IsSet(InputButtons.Skill1))
        {
            _combat.RequestSkill(0, inputData.AimWorldPosition);
        }
        if (buttons.IsSet(InputButtons.Skill2))
        {
            _combat.RequestSkill(1, inputData.AimWorldPosition);
        }
        if (buttons.IsSet(InputButtons.Skill3))
        {
            _combat.RequestSkill(2, inputData.AimWorldPosition);
        }
        if (buttons.IsSet(InputButtons.Interact))
        {
            SendMessage("OnInteractRequested", SendMessageOptions.DontRequireReceiver);
        }

        _previousButtons = buttons;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Damage / Death / Respawn (server-authoritative)
    // ─────────────────────────────────────────────────────────────────────────

    public void ApplyDamage(int amount)
    {
        if (!Object.HasStateAuthority) return;
        if (!IsAlive) return;

        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        if (CurrentHp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        IsAlive = false;
        Debug.Log($"[NetworkPlayer] {PlayerName} died.");
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestRespawn()
    {
        if (!Object.HasStateAuthority) return;
        if (IsAlive) return;

        CurrentHp = MaxHp;
        IsAlive = true;
        transform.position = defaultSpawnPosition;
        Debug.Log($"[NetworkPlayer] {PlayerName} respawned at {defaultSpawnPosition}.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Editor / debug
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(defaultSpawnPosition, 0.5f);
    }
}