using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using MysticJourney.API.Models.Response;
using UnityEngine;
using UnityEngine.InputSystem;

// Executes network behaviour operation.
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerEntity))]
public class NetworkPlayer : NetworkBehaviour
{
    // Executes local operation.
    public static NetworkPlayer Local { get; private set; }
    // Executes all operation.
    public static List<NetworkPlayer> All { get; private set; } = new List<NetworkPlayer>();


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

    [Header("Player Nameplate")]
    [SerializeField] private Vector3 nameplateOffset = new Vector3(0f, 0.72f, 0f);
    [SerializeField] private int nameplateSortingOrder = 60;
    [SerializeField] private TMP_FontAsset nameplateFont;
    [SerializeField, Min(1f)] private float nameplateFontSize = 120f;
    [SerializeField, Min(0.0001f)] private float nameplateWorldScale = 0.003f;
    [SerializeField, Range(0f, 1f)] private float nameplateOutlineWidth = 0.06f;
    [SerializeField] private Color localNameplateColor = new Color(1f, 0.82f, 0.18f, 1f);
    [SerializeField] private Color remoteNameplateColor = Color.white;

    private Canvas _nameplateCanvas;
    private TextMeshProUGUI _nameplateText;


    [Networked] public int PlayerProfileId { get; set; }
    [Networked, OnChangedRender(nameof(OnPlayerNameChanged))] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public int Level { get; set; }

    [Networked] public NetworkString<_32> AvatarUrl { get; set; }

    [Networked, OnChangedRender(nameof(OnPlayerClassChanged))] public int PlayerClass { get; set; }
    [Networked, OnChangedRender(nameof(OnSkinChanged))] public int EquippedSkinId { get; set; }

    private long _builtVisualKey = -1;
    private int _localSkinOverride = -1;

    // Callback triggered when the networked skin ID changes; updates character sprite/visuals.
    public void OnSkinChanged()
    {
        OnPlayerClassChanged();
    }

    // Callback triggered when the networked player name changes; updates overhead nameplate UI.
    private void OnPlayerNameChanged()
    {
        RefreshNameplate();
    }


    // Executes on player class changed operation.
    public void OnPlayerClassChanged()
    {
        if (visualRoot == null) return;

        int visualSkinId = (Object != null && Object.HasInputAuthority && _localSkinOverride >= 0)
            ? _localSkinOverride
            : EquippedSkinId;
        long visualKey = ((long)PlayerClass << 32) | (uint)visualSkinId;
        if (_spawnedVisual != null && _builtVisualKey == visualKey) return;
        _builtVisualKey = visualKey;

        Debug.Log($"[NetworkPlayer] Building visual for {(CharacterClass)PlayerClass} skin={visualSkinId}");

        if (_spawnedVisual != null)
        {
            Destroy(_spawnedVisual);
            _spawnedVisual = null;
        }

        if (characterFactory != null)
        {
            _spawnedVisual = characterFactory.Create(visualSkinId, (CharacterClass)PlayerClass, visualRoot);
        }
        else
        {
            _spawnedVisual = CreateFallbackVisual((CharacterClass)PlayerClass, visualRoot);
        }

        if (_spawnedVisual != null)
        {
            var newAnimator = _spawnedVisual.GetComponentInChildren<Animator>(true);
            var newAnimation = _spawnedVisual.GetComponentInChildren<PlayerAnimation>(true);

            if (_combat == null) _combat = GetComponent<PlayerCombat>();
            if (_combat != null)
            {
                var visualCombat = _spawnedVisual.GetComponent<PlayerCombat>();
                if (visualCombat != null)
                {
                    _combat.CopyCombatSettingsFrom(visualCombat);
                }

                _combat.SetVisualComponents(newAnimator, newAnimation);
            }

            _animation = newAnimation;
        }
    }

    [Networked] public int CurrentHp { get; set; }
    [Networked] public int MaxHp { get; set; }
    [Networked, OnChangedRender(nameof(OnAliveChanged))] public NetworkBool IsAlive { get; set; }

    // Executes is ready to restart operation.
    [Networked, OnChangedRender(nameof(OnReadyStateChanged))]
    public NetworkBool IsReadyToRestart { get; set; }

    // Callback triggered when player alive state changes; updates collider, animation, and death UI.
    private void OnAliveChanged()
    {
        if (Object.HasInputAuthority)
        {
            if (IsAlive)
            {
                var hud = FindFirstObjectByType<PlayerHUDUIManager>();
                if (hud != null)
                {
                    hud.HideDeathPopup();
                }
            }
            else
            {
                OnDied?.Invoke();
            }
        }
    }

    public static event Action OnAnyReadyStateChanged;

    // Executes on ready state changed operation.
    private void OnReadyStateChanged() => EvaluateRestartReadiness();

    // Executes evaluate restart readiness operation.
    private static void EvaluateRestartReadiness()
    {
        OnAnyReadyStateChanged?.Invoke(); // Notify UI components (e.g., restart vote panel) that a ready state changed

        if (PhotonManager.Instance == null || !PhotonManager.Instance.IsHost) return; // Only the host evaluates and triggers restart
        if (All.Count == 0 || !All.TrueForAll(p => p.IsReadyToRestart)) return; // Wait until every connected player is ready

        foreach (var p in All)
        {
            if (p.Object == null) continue;
            if (p.Object.HasStateAuthority) p.IsReadyToRestart = false; // Reset flag directly when state authority
            else p.RPC_ClearReadyToRestart(); // Send RPC to reset flag on remote authority peer
        }

        Debug.Log("[NetworkPlayer] Master client detects all players ready, sending RPC_TriggerRestartDungeon!");

        if (Local != null) Local.RPC_TriggerRestartDungeon(); // Broadcast dungeon restart to all clients
        else Debug.LogWarning("[NetworkPlayer] All players ready but Local is null — cannot send restart RPC.");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Executes rpc_clear ready to restart operation.
    public void RPC_ClearReadyToRestart()
    {
        IsReadyToRestart = false;
    }

    // Executes cancel restart vote for exit operation.
    public void CancelRestartVoteForExit()
    {
        if (Object == null) return;

        try
        {
            if (Object.HasStateAuthority)
            {
                IsReadyToRestart = false;
                EvaluateRestartReadiness();
            }
            else
            {
                RPC_ClearReadyToRestart();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NetworkPlayer] Could not clear restart vote while exiting: {exception.Message}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_trigger restart dungeon operation.
    public void RPC_TriggerRestartDungeon()
    {
        Debug.Log("[NetworkPlayer] Received RPC to RestartDungeon!");
        DungeonManager.Instance?.RestartDungeon();
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    // Executes rpc_set restart session operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    public void RPC_SetRestartSession(int sessionId)
    {
        DungeonManager.Instance?.AdoptRestartSession(sessionId);
    }


    public static event Action<PartyChatMessageResponse> PartyChatReceived;

    // Executes can use party chat operation.
    // Validates input parameters against null or empty values.
    // Evaluates conditions and returns a boolean result.
    public static bool CanUsePartyChat =>
        Local != null && Local.Object != null && Local.Runner != null && Local.Runner.IsRunning;

    // Executes broadcast party chat operation.
    // Validates input parameters against null or empty values.
    public static bool BroadcastPartyChat(PartyChatMessageResponse message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content)) return false;

        if (Local == null)
        {
            Debug.LogWarning("[NetworkPlayer] Party chat send failed: no local avatar spawned yet.");
            return false;
        }
        if (Local.Object == null || !Local.Object.IsValid)
        {
            Debug.LogWarning("[NetworkPlayer] Party chat send failed: local NetworkObject is not valid.");
            return false;
        }
        if (Local.Runner == null || !Local.Runner.IsRunning)
        {
            Debug.LogWarning("[NetworkPlayer] Party chat send failed: runner is not running (still migrating?).");
            return false;
        }

        int senderId = WorldState.PlayerProfileId > 0 ? WorldState.PlayerProfileId : message.SenderId;
        if (senderId <= 0)
        {
            Debug.LogWarning("[NetworkPlayer] Party chat send failed: cannot resolve sender profile id.");
            return false;
        }

        string senderName = !string.IsNullOrWhiteSpace(WorldState.PlayerName)
            ? WorldState.PlayerName
            : message.SenderName;
        string sentAt = string.IsNullOrWhiteSpace(message.SentAt)
            ? DateTime.UtcNow.ToString("O")
            : message.SentAt;

        Debug.Log($"[NetworkPlayer] Party chat send | room='{Local.Runner.SessionInfo?.Name}' " +
                  $"peers={Local.Runner.SessionInfo?.PlayerCount} avatars={All.Count} sender={senderId}");

        Local.RPC_PartyChatMessage(
            senderId,
            NetworkChatText.ClampUtf8(senderName, NetworkChatText.MaxSenderNameBytes),
            NetworkChatText.ClampUtf8(message.Content, NetworkChatText.MaxContentBytes),
            NetworkChatText.ClampUtf8(sentAt, NetworkChatText.MaxTimestampBytes));
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    // Process rpc party chat message using sender id, sender name, content, and sent at; it guards invalid or unavailable states.
    private void RPC_PartyChatMessage(int senderId, string senderName,
                                      string content, string sentAt)
    {
        if (senderId <= 0) return;

        Debug.Log($"[NetworkPlayer] Party chat RPC received from {senderId} " +
                  $"({senderName}), listeners={PartyChatReceived?.GetInvocationList().Length ?? 0}");

        PartyChatReceived?.Invoke(new PartyChatMessageResponse
        {
            SenderId   = senderId,
            SenderName = senderName ?? string.Empty,
            Content    = content ?? string.Empty,
            Channel    = "Party",
            SentAt     = sentAt ?? string.Empty,
        });
    }

    // Executes trim for fusion operation.
    // Validates input parameters against null or empty values.
    private static string TrimForFusion(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    [Networked] public Vector2 NetworkedMove { get; set; }


    private PlayerMovement _movement;
    private PlayerCombat _combat;
    private PlayerEntity _entity;
    private PlayerAnimation _animation;
    private PlayerInput _playerInput;

    private GameObject _spawnedVisual;
    private NetworkButtons _previousButtons;


    public event Action<NetworkPlayer> OnPlayerReady;

    public event Action OnDied;


    // Executes player operation.
    public PlayerRef Player => Object.InputAuthority;

    // Executes visual object operation.
    public GameObject VisualObject => _spawnedVisual;


    // Initializes internal component caches and dependencies for NetworkPlayer upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>(); // Cache PlayerMovement for direct access during FixedUpdateNetwork
        _combat = GetComponent<PlayerCombat>(); // Cache PlayerCombat to dispatch skill/damage calls
        _entity = GetComponent<PlayerEntity>(); // Cache PlayerEntity for HP and stat access
        _animation = GetComponent<PlayerAnimation>(); // Cache PlayerAnimation to trigger visual state changes
        _playerInput = GetComponent<PlayerInput>(); // Cache Unity Input System component for enabling/disabling player control

        if (visualRoot == null)
        {
            var found = transform.Find("VisualRoot");
            if (found != null)
            {
                visualRoot = found; // Re-use existing VisualRoot child if one was attached in the prefab
            }
            else
            {
                var go = new GameObject("VisualRoot"); // Create a dedicated container for character visuals at runtime
                go.transform.SetParent(transform, worldPositionStays: false);
                visualRoot = go.transform;
            }
        }

        EnsureNameplate(); // Create or attach the overhead nameplate Canvas component
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy() => Unregister();

    // Executes unregister operation.
    private void Unregister()
    {
        All.Remove(this);

        if (Local == this) Local = null;

        if (_spawnedVisual != null)
        {
            Destroy(_spawnedVisual);
            _spawnedVisual = null;
        }

        if (Local != null) EvaluateRestartReadiness();
    }


    // Executes apply equipped skin operation.
    public void ApplyEquippedSkin(int skinId)
    {
        int normalizedSkinId = Mathf.Max(0, skinId);

        WorldState.EquippedSkinId = normalizedSkinId;
        WorldState.SaveToPlayerPrefs();

        if (Object != null && Object.HasStateAuthority)
        {
            _localSkinOverride = -1;
            EquippedSkinId = normalizedSkinId;
        }
        else if (Object != null && Object.HasInputAuthority)
        {
            _localSkinOverride = normalizedSkinId;
        }

        OnPlayerClassChanged();
    }

    // Executes publish local avatar operation.
    public static void PublishLocalAvatar(string avatarUrl)
    {
        WorldState.AvatarUrl = avatarUrl;
        WorldState.SaveToPlayerPrefs();

        var local = Local;
        if (local != null && local.Object != null && local.Object.HasStateAuthority)
        {
            local.AvatarUrl = TrimForFusion(avatarUrl, 30);
        }
    }

    // Executes resolve avatar sprite operation.
    // Validates input parameters against null or empty values.
    public static Sprite ResolveAvatarSprite(string avatarUrl)
    {
        Sprite sprite = null;
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            sprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
        }

        return sprite != null ? sprite : Resources.Load<Sprite>("Avatars/avatar_1");
    }

    // Fusion lifecycle callback invoked when this NetworkPlayer NetworkObject is spawned into the network session.
    // Configures input/state authority handlers, sets singleton references if local player, and applies initial visuals.
    public override void Spawned()
    {
        Debug.Log($"[NetworkPlayer] Spawned. InputAuthority={Object.InputAuthority}, " +
                  $"StateAuthority={Object.StateAuthority}");

        if (_playerInput != null)
        {
            _playerInput.enabled = Object.HasInputAuthority; // Only enable keyboard/gamepad input for the locally controlled player
        }

        if (Object.HasStateAuthority)
        {
            // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
            string className = WorldState.PlayerClass ?? "Knight";
            if (!Enum.TryParse<CharacterClass>(className, ignoreCase: true, out var parsed))
                parsed = CharacterClass.Knight; // Fall back to Knight when class name is unrecognized
            PlayerClass = (int)parsed; // Write class to networked property — triggers OnPlayerClassChanged on all clients
            PlayerName = WorldState.PlayerName ?? "Player"; // Write display name to networked string — triggers nameplate refresh
            PlayerProfileId = WorldState.PlayerProfileId; // Store server-assigned profile ID for API calls
            Level = Mathf.Max(1, WorldState.PlayerLevel); // Clamp level to minimum 1 in case of missing data
            AvatarUrl = TrimForFusion(WorldState.AvatarUrl, 30); // Truncate avatar URL to fit Fusion NetworkString limit

            EquippedSkinId = Mathf.Max(0, WorldState.EquippedSkinId); // Initialize skin ID from local player prefs

            Vector3 spawnBase = ResolveSpawnBase(); // Look up spawn point from SpawnPointManager or default position
            TeleportTo(spawnBase + FanOutOffset(Object.InputAuthority.PlayerId)); // Fan players out to prevent spawn overlap

            var pEntity = GetComponent<PlayerEntity>();
            if (MaxHp <= 0)
            {
                MaxHp = (pEntity != null && pEntity.MaxHealth > 0) ? pEntity.MaxHealth : 100; // Initialize max HP from entity stats or default 100
                CurrentHp = (pEntity != null) ? pEntity.CurrentHealth : MaxHp; // Initialize current HP from entity or full health
            }

            IsAlive = CurrentHp > 0; // Mark alive state based on current HP — drives death UI and collider activation
        }

        OnPlayerClassChanged(); // Rebuild character visual after networked class and skin values are set
        RefreshNameplate(); // Update overhead name text from networked PlayerName value

        if (Object.HasInputAuthority)
        {
            Local = this; // Register this instance as the local player singleton for global access
            name = "NetworkPlayer_Local"; // Rename GameObject for easy identification in the Hierarchy

            var localCombat = GetComponent<PlayerCombat>();
            if (localCombat != null) localCombat.LoadEquippedSkills(); // Load skill assets from server data into the local combat component

            var hud = FindFirstObjectByType<PlayerHUDUIManager>(FindObjectsInactive.Include);
            if (hud != null)
            {
                hud.SubscribeToLocalPlayer(this); // Connect HUD health bar and death events to this player instance
                if (!IsAlive)
                {
                    hud.ShowDeathPopup(); // Show death screen immediately if player was dead before respawn
                }
                else
                {
                    hud.HideDeathPopup();
                }
            }

            var pEntityLocal = GetComponent<PlayerEntity>();
            if (pEntityLocal != null)
            {
                PlayerEntity.Instance = pEntityLocal; // Set global PlayerEntity singleton for scene-wide stat access
            }

            RemoveLegacyLocalPlayers(); // Destroy any leftover local player GameObjects from previous scene loads
            AttachLocalCamera();
            EnsureLocalInputComponents();
        }
        else
        {
            name = $"NetworkPlayer_{Object.InputAuthority.PlayerId}";
        }

        if (!All.Contains(this))
        {
            All.Add(this);
        }

        IgnoreCollisionsWithOtherPlayers();

        OnPlayerReady?.Invoke(this);
    }

    // Executes ignore collisions with other players operation.
    private void IgnoreCollisionsWithOtherPlayers()
    {
        var mine = GetComponentsInChildren<Collider2D>(includeInactive: true);
        if (mine.Length == 0) return;

        for (int i = 0; i < All.Count; i++)
        {
            var other = All[i];
            if (other == null || other == this) continue;

            var theirs = other.GetComponentsInChildren<Collider2D>(includeInactive: true);
            for (int m = 0; m < mine.Length; m++)
            {
                if (mine[m] == null) continue;
                for (int t = 0; t < theirs.Length; t++)
                {
                    if (theirs[t] == null) continue;
                    Physics2D.IgnoreCollision(mine[m], theirs[t], true);
                }
            }
        }
    }

    // Executes fan out offset operation.
    public static Vector3 FanOutOffset(int playerId)
    {
        int playerIndex = Mathf.Max(0, playerId - 1);
        float angle = playerIndex * 137.508f * Mathf.Deg2Rad;
        float radius = 2.5f * Mathf.Sqrt(1f + playerIndex * 0.35f);
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
    }

    // Instantly teleports the player transform and networked position to the specified coordinates.
    private void TeleportTo(Vector3 position)
    {
        var nt = GetComponent<NetworkTransform>();
        if (nt != null) nt.Teleport(position);
        else transform.position = position;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = position;
        }
    }

    // Executes resolve spawn base operation.
    private Vector3 ResolveSpawnBase()
    {
        Vector3 last = WorldState.LastPosition;
        bool valid = last != Vector3.zero
                     && !float.IsNaN(last.x) && !float.IsNaN(last.y) && !float.IsNaN(last.z)
                     && !float.IsInfinity(last.x) && !float.IsInfinity(last.y) && !float.IsInfinity(last.z);
        return valid ? last : defaultSpawnPosition;
    }

    // Executes remove legacy local players operation.
    private void RemoveLegacyLocalPlayers()
    {
        var movers = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        foreach (var mover in movers)
        {
            if (mover.gameObject != this.gameObject &&
                !mover.transform.IsChildOf(this.transform) &&
                mover.GetComponent<NetworkObject>() == null)
            {
                Debug.Log("[NetworkPlayer] Removing leftover local (non-networked) player after connect.");
                Destroy(mover.gameObject);
            }
        }
    }

    // Executes attach local camera operation.
    private void AttachLocalCamera()
    {
        var cam = FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
        if (cam != null)
        {
            cam.Follow = transform;
            var composer = cam.GetComponent<Unity.Cinemachine.CinemachinePositionComposer>();
            if (composer != null)
            {
                composer.Damping = new Vector3(0.05f, 0.05f, 0.05f);
            }
        }
        else
            Debug.LogWarning("[NetworkPlayer] CinemachineCamera not found for local player follow.");

        var minimapCam = FindFirstObjectByType<MinimapCameraController>();
        if (minimapCam != null)
            minimapCam.InitializeMinimap(transform);
    }

    // Executes ensure local input components operation.
    private void EnsureLocalInputComponents()
    {
        if (GetComponent<GameplayInputProvider>() == null)
            gameObject.AddComponent<GameplayInputProvider>();

        if (GetComponent<PlayerWorldInteractor>() == null)
            gameObject.AddComponent<PlayerWorldInteractor>();
    }

    // Executes create fallback visual operation.
    private static GameObject CreateFallbackVisual(CharacterClass characterClass, Transform parent)
    {
        var go = new GameObject($"Visual_{characterClass}_Fallback");
        if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SolidSprite(characterClass);
        sr.sortingOrder = 10;

        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        Debug.Log($"[NetworkPlayer] Created fallback visual for class {characterClass} " +
                  $"(color={ClassColor(characterClass)}) — wire CharacterFactory to use proper sprites.");
        return go;
    }

    // Executes class color operation.
    private static Color ClassColor(CharacterClass c)
    {
        switch (c)
        {
            case CharacterClass.Archer: return new Color(0.30f, 0.85f, 0.30f);
            case CharacterClass.Mage:   return new Color(0.45f, 0.35f, 0.95f);
            case CharacterClass.Knight:
            default:                    return new Color(0.95f, 0.70f, 0.20f);
        }
    }

    private static readonly Dictionary<CharacterClass, Sprite> _fallbackSprites = new();

    // Executes solid sprite operation.
    private static Sprite SolidSprite(CharacterClass characterClass)
    {
        if (_fallbackSprites.TryGetValue(characterClass, out var cached) && cached != null)
            return cached;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var color = (Color32)ClassColor(characterClass);
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels32(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        _fallbackSprites[characterClass] = sprite;
        return sprite;
    }

    // Fusion lifecycle callback invoked when this NetworkPlayer NetworkObject is despawned from the network session.
    // Performs teardown, unregisters network listeners, and cleans up singleton references.
    public override void Despawned(NetworkRunner runner, bool hasState) => Unregister();

    // Executes render operation.
    public override void Render()
    {
        if (_nameplateCanvas != null)
        {
            _nameplateCanvas.transform.rotation = Quaternion.identity;
            Vector3 scale = _nameplateCanvas.transform.localScale;
            float expectedX = transform.lossyScale.x < 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            _nameplateCanvas.transform.localScale = new Vector3(expectedX, scale.y, scale.z);
        }

        if (_animation != null)
        {
            _animation.SetMovement(NetworkedMove, IsAlive);
        }
    }

    // Executes ensure nameplate operation.
    // Validates input parameters against null or empty values.
    private void EnsureNameplate()
    {
        if (_nameplateCanvas != null && _nameplateText != null) return;

        Transform existing = transform.Find("PlayerNameplate");
        GameObject canvasObject = existing != null
            ? existing.gameObject
            : new GameObject("PlayerNameplate", typeof(RectTransform));

        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = nameplateOffset;

        _nameplateCanvas = canvasObject.GetComponent<Canvas>();
        if (_nameplateCanvas == null) _nameplateCanvas = canvasObject.AddComponent<Canvas>();
        _nameplateCanvas.renderMode = RenderMode.WorldSpace;
        _nameplateCanvas.overrideSorting = true;
        _nameplateCanvas.sortingLayerName = "Default";
        _nameplateCanvas.sortingOrder = nameplateSortingOrder;

        Transform textTransform = canvasObject.transform.Find("Text");
        GameObject textObject = textTransform != null
            ? textTransform.gameObject
            : new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(canvasObject.transform, false);

        _nameplateText = textObject.GetComponent<TextMeshProUGUI>();
        if (_nameplateText == null) _nameplateText = textObject.AddComponent<TextMeshProUGUI>();
        _nameplateText.alignment = TextAlignmentOptions.Center;
        _nameplateText.textWrappingMode = TextWrappingModes.NoWrap;
        _nameplateText.overflowMode = TextOverflowModes.Overflow;
        _nameplateText.richText = false;
        _nameplateText.enableAutoSizing = false;
        _nameplateText.extraPadding = true;
        _nameplateText.fontSize = nameplateFontSize;
        _nameplateText.raycastTarget = false;

        ApplyNameplateStyle();

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(900f, 180f);
        textRect.localScale = new Vector3(nameplateWorldScale, nameplateWorldScale, 1f);
    }

    // Executes refresh nameplate operation.
    // Validates input parameters against null or empty values.
    private void RefreshNameplate()
    {
        EnsureNameplate();
        if (_nameplateText == null) return;

        ApplyNameplateStyle();
        string displayName = PlayerName.ToString().Trim();
        _nameplateText.text = displayName;
        _nameplateCanvas.gameObject.SetActive(!string.IsNullOrWhiteSpace(displayName));
    }

    // Executes apply nameplate style operation.
    private void ApplyNameplateStyle()
    {
        if (_nameplateText == null) return;

        if (nameplateFont != null)
        {
            _nameplateText.font = nameplateFont;
            _nameplateText.fontSharedMaterial = nameplateFont.material;
        }

        _nameplateText.outlineWidth = nameplateOutlineWidth;
        _nameplateText.outlineColor = Color.black;

        bool isLocalPlayer = Object != null && Object.HasInputAuthority;
        _nameplateText.color = isLocalPlayer ? localNameplateColor : remoteNameplateColor;
    }


    // Networked fixed-step simulation tick callback executed by Photon Fusion.
    // Processes synchronized player input, applies physics velocities, and updates authoritative gameplay mechanics.
    public override void FixedUpdateNetwork()
    {
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
            _movement.Move(Vector2.zero, Runner.DeltaTime);
            NetworkedMove = Vector2.zero;
            return;
        }
        var inputData = input.Value;

        NetworkedMove = inputData.Move;
        _movement.Move(inputData.Move, Runner.DeltaTime);

        var buttons = inputData.Buttons;

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

        _previousButtons = buttons;
    }


    // Applies combat damage authoritatively to this player on the StateAuthority instance.
    // Reduces CurrentHp, triggers combat popup RPCs across clients, and calls Die() if HP reaches zero.
    public void ApplyDamage(int amount, bool isCritical = false)
    {
        if (!Object.HasStateAuthority) return; // Only the authoritative simulation peer may mutate networked HP
        if (!IsAlive) return; // Skip damage processing — player is already in death state

        int previousHp = CurrentHp; // Snapshot HP before reduction to compute actual damage absorbed
        CurrentHp = Mathf.Max(0, CurrentHp - amount); // Reduce HP and clamp to 0 to prevent negative health
        int appliedDamage = previousHp - CurrentHp; // Compute real damage after floor clamping
        if (appliedDamage > 0)
        {
            RPC_ShowPlayerCombatPopup(transform.position, appliedDamage, isCritical, false); // Broadcast floating damage number to all clients
        }

        if (CurrentHp <= 0)
        {
            Die(); // HP depleted — trigger death sequence (disable collider, play animation, show death UI)
        }
    }

    // Requests damage to be applied to this player, routing via RPC to the StateAuthority instance.
    public void RequestDamage(int amount, bool isCritical = false)
    {
        if (amount <= 0) return; // Ignore zero or negative damage values — no-op

        if (Object.HasStateAuthority)
            ApplyDamage(amount, isCritical); // Apply directly — no RPC needed, this peer owns state
        else
            RPC_RequestDamage(amount, isCritical); // Send RPC to the authority peer to apply damage
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Photon Fusion RPC receiving damage request on the StateAuthority and executing ApplyDamage().
    private void RPC_RequestDamage(int amount, NetworkBool isCritical)
    {
        ApplyDamage(amount, isCritical);
    }

    // Restores player health clamped to MaxHp and triggers combat popup visual effects.
    public void ApplyHeal(int amount)
    {
        if (!Object.HasStateAuthority) return; // Only the authoritative simulation peer may mutate networked HP
        if (!IsAlive) return; // Dead players cannot receive healing

        int previousHp = CurrentHp; // Snapshot HP before restoration to compute actual healed amount
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount); // Restore HP and clamp to MaxHp to prevent overhealing
        int appliedHeal = CurrentHp - previousHp; // Compute real heal after ceiling clamping
        if (appliedHeal > 0)
        {
            RPC_ShowPlayerCombatPopup(transform.position, appliedHeal, false, true); // Broadcast floating heal number to all clients
        }
    }

    // Requests healing to be applied to this player, routing via RPC to the StateAuthority instance.
    public void RequestHeal(int amount)
    {
        if (amount <= 0) return; // Ignore zero or negative heal values — no-op

        if (Object.HasStateAuthority)
            ApplyHeal(amount); // Apply directly — no RPC needed, this peer owns state
        else
            RPC_RequestHeal(amount); // Send RPC to the authority peer to apply heal
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Photon Fusion RPC receiving heal request on the StateAuthority and executing ApplyHeal().
    private void RPC_RequestHeal(int amount)
    {
        ApplyHeal(amount);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Process rpc show player combat popup using world position, amount, is critical, and is heal; it creates create and guards invalid or unavailable states.
    private void RPC_ShowPlayerCombatPopup(Vector3 worldPosition, int amount,
        NetworkBool isCritical, NetworkBool isHeal)
    {
        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.Create(worldPosition, amount, isCritical, true, isHeal);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    // Instantiates a visual-only buff effect prefab attached to the player across all clients.
    public void RPC_ShowBuffVisual(string prefabName)
    {
        GameObject prefab = PlayerCombat.FindLoadedSkillPrefab(prefabName); // Look up preloaded skill VFX prefab by name
        if (prefab == null)
        {
            Debug.LogWarning($"[NetworkPlayer] Cannot resolve buff visual prefab '{prefabName}'.");
            return; // Prefab not loaded — skip visual without crashing
        }

        GameObject effect = Instantiate(prefab, transform.position, Quaternion.identity); // Spawn VFX at current player position
        effect.SetActive(false); // Disable briefly to allow PlayerSkillVisualReplica.Mark to configure it before activation
        PlayerSkillVisualReplica.Mark(effect, transform); // Tag as visual-only replica — prevents this client from re-broadcasting effects
        effect.SetActive(true); // Activate after marking to trigger OnEnable logic correctly
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    // Applies a temporary defense multiplier buff to the player's combat component.
    public void RPC_ApplyDefBuff(float amount, float duration)
    {
        var combat = GetComponent<PlayerCombat>(); // Resolve combat component on this peer's instance
        if (combat != null)
        {
            combat.AddDefBuff(amount, duration); // Apply defense percentage multiplier buff for the given duration
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    // Applies temporary debuff immunity to the player's combat component.
    public void RPC_ApplyDebuffImmunity(float duration)
    {
        var combat = GetComponent<PlayerCombat>(); // Resolve combat component on this peer's instance
        if (combat != null)
        {
            combat.AddDebuffImmunity(duration); // Grant immunity to incoming debuffs for the given duration
        }
    }

    // Marks player as dead (IsAlive = false), halts movement, and triggers death sequence.
    public void Die()
    {
        if (!IsAlive) return;
        IsAlive = false;
        Debug.Log($"[NetworkPlayer] {PlayerName} died.");

    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    // Executes rpc_set ready to restart operation.
    public void RPC_SetReadyToRestart()
    {
        if (!Object.HasStateAuthority) return;

        IsReadyToRestart = true;
        Debug.Log($"[NetworkPlayer] {PlayerName} is ready to restart.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_boss spawning operation.
    public void RPC_BossSpawning()
    {
        if (Object.HasStateAuthority) return;

        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.ClientReceiveBossSpawning();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    // Executes rpc_boss died operation.
    public void RPC_BossDied(Vector3 chestPosition)
    {
        if (Object.HasStateAuthority) return;

        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.ClientReceiveBossDeath(chestPosition);
        }
    }

    // Resets player stats, restores full HP, sets IsAlive to true, and teleports to respawn point.
    public void ResetForRestart(Vector3 spawnPos)
    {
        if (!Object.HasStateAuthority) return;

        CurrentHp = MaxHp;
        IsAlive = true;
        IsReadyToRestart = false;
        TeleportTo(spawnPos);
        Debug.Log($"[NetworkPlayer] {PlayerName} reset for restart at {spawnPos}.");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Authoritative RPC respawning player in the open world with 10% HP at specified respawn position.
    public void RPC_WorldRespawn(Vector3 position)
    {
        if (!Object.HasStateAuthority) return;
        if (IsAlive) return;

        CurrentHp = Mathf.Max(1, MaxHp / 10);
        IsAlive = true;
        IsReadyToRestart = false;
        TeleportTo(position);
        Debug.Log($"[NetworkPlayer] {PlayerName} respawned in world at {position} with 10% HP.");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    // Authoritative RPC respawning player inside a dungeon session with full HP at spawn position.
    public void RPC_DungeonRespawn(Vector3 position)
    {
        if (!Object.HasStateAuthority) return;

        CurrentHp = MaxHp;
        IsAlive = true;
        IsReadyToRestart = false;
        TeleportTo(position);
        Debug.Log($"[NetworkPlayer] {PlayerName} respawned in dungeon at {position} with FULL HP.");
    }


    // Executes on draw gizmos selected operation.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(defaultSpawnPosition, 0.5f);
    }
}
