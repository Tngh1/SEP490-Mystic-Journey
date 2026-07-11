using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerEntity))]
public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer Local { get; private set; }

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
    [Networked, OnChangedRender(nameof(OnPlayerClassChanged))] public int PlayerClass { get; set; }
    [Networked, OnChangedRender(nameof(OnSkinChanged))] public int EquippedSkinId { get; set; }

    public void OnSkinChanged()
    {
        Debug.Log($"[NetworkPlayer] OnSkinChanged to {EquippedSkinId}");
        OnPlayerClassChanged();
    }

    public void OnPlayerClassChanged()
    {
        Debug.Log($"[NetworkPlayer] OnPlayerClassChanged to {(CharacterClass)PlayerClass}");
        if (visualRoot == null) return;

        if (_spawnedVisual != null)
        {
            Destroy(_spawnedVisual);
            _spawnedVisual = null;
        }

        if (characterFactory != null)
        {
            _spawnedVisual = characterFactory.Create(EquippedSkinId, (CharacterClass)PlayerClass, visualRoot);
        }
        else
        {
            _spawnedVisual = CreateFallbackVisual((CharacterClass)PlayerClass, visualRoot);
        }

        // Rebind local combat/animation components if they exist
        if (_spawnedVisual != null)
        {
            var newAnimator = _spawnedVisual.GetComponentInChildren<Animator>(true);
            var newAnimation = _spawnedVisual.GetComponentInChildren<PlayerAnimation>(true);
            
            if (_combat == null) _combat = GetComponent<PlayerCombat>();
            if (_combat != null)
            {
                // The spawned visual initially contains the class-specific PlayerCombat 
                // before CharacterFactory strips it. We copy its skills to our networked root.
                var visualCombat = _spawnedVisual.GetComponent<PlayerCombat>();
                if (visualCombat != null)
                {
                    _combat.CopyCombatSettingsFrom(visualCombat);
                }
                
                _combat.SetVisualComponents(newAnimator, newAnimation);
            }

            // Update the NetworkPlayer's own animation reference so Render() drives the correct animator
            _animation = newAnimation;
        }
    }

    [Networked] public int CurrentHp { get; set; }
    [Networked] public int MaxHp { get; set; }
    [Networked] public NetworkBool IsAlive { get; set; }

    // Replicated movement vector. The input-authority client writes it each tick
    // from its input; every OTHER client reads it in Render() to drive the walk
    // animation of the remote avatar. Without this, remote players slide to their
    // NetworkTransform position with no walk animation (idle pose while moving).
    [Networked] public Vector2 NetworkedMove { get; set; }

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
        if (Local == this) Local = null;

        if (_spawnedVisual != null)
        {
            Destroy(_spawnedVisual);
            _spawnedVisual = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fusion lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public void ApplyEquippedSkin(int skinId)
    {
        int normalizedSkinId = Mathf.Max(0, skinId);

        WorldState.EquippedSkinId = normalizedSkinId;
        WorldState.SaveToPlayerPrefs();

        if (Object != null)
        {
            if (!Object.HasStateAuthority)
            {
                Debug.LogWarning("[NetworkPlayer] Ignoring local skin apply without StateAuthority; waiting for network sync.");
                return;
            }

            EquippedSkinId = normalizedSkinId;
        }

        OnPlayerClassChanged();
    }

    public override void Spawned()
    {
        Debug.Log($"[NetworkPlayer] Spawned. InputAuthority={Object.InputAuthority}, " +
                  $"StateAuthority={Object.StateAuthority}");

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

            if (MysticJourney.API.Core.ApiClient.Instance.HasToken())
            {
                MysticJourney.API.Endpoints.InventoryApi.Instance.GetInventory(
                    response =>
                    {
                        if (response != null && response.PlayerSkins != null)
                        {
                            foreach (var skin in response.PlayerSkins)
                            {
                                if (skin.IsEquipped)
                                {
                                    EquippedSkinId = skin.SkinId;
                                    break;
                                }
                            }
                        }
                    },
                    error => Debug.LogWarning($"[NetworkPlayer] GetInventory failed: {error.Message}")
                );
            }

            // Anchor spawns at the current world position (e.g. ElfForest ~(11.9,17.8))
            // rather than world origin, then fan out so players don't stack.
            Vector3 spawnBase = ResolveSpawnBase();
            int playerIndex = Mathf.Max(0, Object.InputAuthority.PlayerId - 1);
            float angle = playerIndex * 60f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 2.5f;
            transform.position = spawnBase + offset;

            IsAlive = true;
            
            // Read initial stats from PlayerEntity if available (loaded from DB), else fallback
            var pEntity = GetComponent<PlayerEntity>();
            if (MaxHp <= 0)
            {
                MaxHp = (pEntity != null && pEntity.MaxHealth > 0) ? pEntity.MaxHealth : 100;
                CurrentHp = (pEntity != null) ? pEntity.CurrentHealth : MaxHp;
            }
        }

        // Force an initial visual creation since OnChangedRender might not fire for default/initial values
        OnPlayerClassChanged();

        if (Object.HasInputAuthority)
        {
            Local = this;
            var pEntityLocal = GetComponent<PlayerEntity>();
            if (pEntityLocal != null)
            {
                // In multiplayer, multiple PlayerEntity objects spawn. Ensure the singleton
                // points to the LOCAL player's entity, not the last spawned remote player.
                PlayerEntity.Instance = pEntityLocal;
            }

            // This is the local player's network avatar. Remove any leftover
            // non-networked local player (spawned by PlayerSpawner if we connected
            // AFTER entering the map) and hand the scene camera + minimap to us.
            RemoveLegacyLocalPlayers();
            AttachLocalCamera();
            EnsureLocalInputComponents();
        }

        OnPlayerReady?.Invoke(this);
    }

    /// <summary>
    /// World position the state authority uses as the spawn anchor. Prefers the
    /// last known world position (so players spawn in the active gameplay area,
    /// e.g. ElfForest ~(11.9,17.8)) and falls back to the inspector default.
    /// </summary>
    private Vector3 ResolveSpawnBase()
    {
        Vector3 last = WorldState.LastPosition;
        bool valid = last != Vector3.zero
                     && !float.IsNaN(last.x) && !float.IsNaN(last.y) && !float.IsNaN(last.z)
                     && !float.IsInfinity(last.x) && !float.IsInfinity(last.y) && !float.IsInfinity(last.z);
        return valid ? last : defaultSpawnPosition;
    }

    /// <summary>
    /// Destroy any non-networked PlayerMovement instances left over from the
    /// single-player spawn path. A local player is "non-networked" when its
    /// PlayerMovement has no Fusion Object; we must not touch network avatars.
    /// </summary>
    private void RemoveLegacyLocalPlayers()
    {
        var movers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var mover in movers)
        {
            // Do not destroy the NetworkPlayer root, and do not destroy its child visuals
            // (which temporarily still have PlayerMovement until the end of the frame when Destroy is processed).
            if (mover.gameObject != this.gameObject && 
                !mover.transform.IsChildOf(this.transform) && 
                mover.GetComponent<NetworkObject>() == null)
            {
                Debug.Log("[NetworkPlayer] Removing leftover local (non-networked) player after connect.");
                Destroy(mover.gameObject);
            }
        }
    }

    /// <summary>
    /// Point the scene's Cinemachine camera and minimap at this local avatar.
    /// </summary>
    private void AttachLocalCamera()
    {
        var cam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
        if (cam != null)
            cam.Follow = transform;
        else
            Debug.LogWarning("[NetworkPlayer] CinemachineCamera not found for local player follow.");

        var minimapCam = FindFirstObjectByType<MinimapCameraController>();
        if (minimapCam != null)
            minimapCam.InitializeMinimap(transform);
    }

    /// <summary>
    /// Ensure the local networked avatar carries the input-driven gameplay
    /// components that the offline spawn path (PlayerSpawner) would otherwise add.
    /// Only the local (input-authority) avatar needs these; remote avatars are
    /// driven purely by replicated NetworkTransform + the networked animation
    /// state written in <see cref="FixedUpdateNetwork"/>.
    ///   • GameplayInputProvider — single source of truth for input reads.
    ///   • PlayerWorldInteractor — polls the local GameplayInputProvider for the
    ///     rebindable Interact action to talk to NPCs / open dungeons / world
    ///     objects. Interact is client-local (opens panels / calls the API, not
    ///     the simulation), so it is never routed over the network.
    /// </summary>
    private void EnsureLocalInputComponents()
    {
        if (GetComponent<GameplayInputProvider>() == null)
            gameObject.AddComponent<GameplayInputProvider>();

        if (GetComponent<PlayerWorldInteractor>() == null)
            gameObject.AddComponent<PlayerWorldInteractor>();
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
            // Drive animation from the REPLICATED move vector so every client
            // (including those watching a remote avatar) animates walking. The
            // local player also uses this — it's written every tick below.
            _animation.SetMovement(NetworkedMove, IsAlive);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // [DEBUG-MOVE] tick heartbeat
        if (Time.frameCount % 30 == 0)
            Debug.Log($"[NetPlayer/FixedUpdateNet] tick frame={Time.frameCount} " +
                      $"HasInputAuth={HasInputAuthority} IsAlive={IsAlive} Runner={Runner?.Stage} " +
                      $"Object={(Object != null ? "valid" : "NULL")}");

        if (!HasInputAuthority) return;

        if (Object == null)
        {
            Debug.LogError("[NetPlayer/FixedUpdateNet] NetworkObject is NULL on PlayerNetwork prefab!");
            return;
        }

        if (!IsAlive)
        {
            _movement.Move(Vector2.zero, Runner.DeltaTime);
            NetworkedMove = Vector2.zero;
            return;
        }

        var input = GetInput<NetworkInputData>();
        if (!input.HasValue)
        {
            if (Time.frameCount % 60 == 0)
                Debug.LogWarning($"[NetPlayer/FixedUpdateNet] GetInput<NetworkInputData> returned null " +
                                 $"Runner.Stage={Runner?.Stage} Runner.IsRunning={Runner?.IsRunning}");
            return;
        }
        var inputData = input.Value;

        if (inputData.Move.sqrMagnitude > 0.01f || Time.frameCount % 30 == 0)
        {
            Debug.Log($"[NetPlayer/FixedUpdateNet] move={inputData.Move} dt={Runner.DeltaTime} " +
                      $"movement={(ReferenceEquals(_movement, null) ? "NULL" : "OK")} " +
                      $"rb={(ReferenceEquals(_movement, null) ? "n/a" : (_movement.GetComponent<Rigidbody2D>() == null ? "NULL" : "OK"))}");
        }

        NetworkedMove = inputData.Move;
        _movement.Move(inputData.Move, Runner.DeltaTime);

        var buttons = inputData.Buttons;

        // Attack / skills are edge-triggered off the previous tick's buttons so a
        // held key fires ONE request, not one every network tick (which would
        // spam RequestAttack/RPC and re-trigger the cast repeatedly).
        if (buttons.WasPressed(_previousButtons, InputButtons.Attack))
        {
            _combat.RequestAttack(inputData.AimWorldPosition);
        }
        if (buttons.WasPressed(_previousButtons, InputButtons.Skill1))
        {
            _combat.RequestSkill(0, inputData.AimWorldPosition);
        }
        if (buttons.WasPressed(_previousButtons, InputButtons.Skill2))
        {
            _combat.RequestSkill(1, inputData.AimWorldPosition);
        }
        if (buttons.WasPressed(_previousButtons, InputButtons.Skill3))
        {
            _combat.RequestSkill(2, inputData.AimWorldPosition);
        }
        // Interact / Inventory / Map are client-local (they open panels / talk to
        // the API, not the simulation) so they are polled directly on the local
        // player by PlayerWorldInteractor / the UI runtimes — not routed here.

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

    /// <summary>
    /// Damage this player from any client (e.g. an enemy AI running on the master
    /// client hitting a remote player). In Shared Mode each player owns
    /// StateAuthority over its own avatar, so the request is routed there via RPC
    /// and applied authoritatively. Safe to call from the enemy authority.
    /// </summary>
    public void RequestDamage(int amount)
    {
        if (amount <= 0) return;

        if (Object.HasStateAuthority)
            ApplyDamage(amount);
        else
            RPC_RequestDamage(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDamage(int amount)
    {
        ApplyDamage(amount);
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