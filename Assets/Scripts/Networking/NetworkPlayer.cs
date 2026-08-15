using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using MysticJourney.API.Models.Response;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerEntity))]
public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer Local { get; private set; }
    public static List<NetworkPlayer> All { get; private set; } = new List<NetworkPlayer>();

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

    // ─────────────────────────────────────────────────────────────────────────
    // Networked state
    // ─────────────────────────────────────────────────────────────────────────

    [Networked] public int PlayerProfileId { get; set; }
    [Networked, OnChangedRender(nameof(OnPlayerNameChanged))] public NetworkString<_32> PlayerName { get; set; }
    [Networked] public int Level { get; set; }

    /// <summary>
    /// Profile avatar resource name (e.g. "avatar_3") under Resources/Avatars. Replicated
    /// so party members can render each other's avatar in the in-dungeon roster — the HUD's
    /// own avatar comes straight from the profile API, but a proxy has no API access to
    /// another player's profile.
    /// </summary>
    [Networked] public NetworkString<_32> AvatarUrl { get; set; }

    [Networked, OnChangedRender(nameof(OnPlayerClassChanged))] public int PlayerClass { get; set; }
    [Networked, OnChangedRender(nameof(OnSkinChanged))] public int EquippedSkinId { get; set; }

    // (PlayerClass, EquippedSkinId) the current visual was built from. -1 = none yet.
    private long _builtVisualKey = -1;

    public void OnSkinChanged()
    {
        OnPlayerClassChanged();
    }

    private void OnPlayerNameChanged()
    {
        RefreshNameplate();
    }


    public void OnPlayerClassChanged()
    {
        if (visualRoot == null) return;

        // Spawned(), both OnChangedRender hooks and the async inventory fetch all
        // funnel here, so the same (class, skin) pair was rebuilt 2-3 times per
        // spawn — each rebuild destroys and re-instantiates the whole visual.
        long visualKey = ((long)PlayerClass << 32) | (uint)EquippedSkinId;
        if (_spawnedVisual != null && _builtVisualKey == visualKey) return;
        _builtVisualKey = visualKey;

        Debug.Log($"[NetworkPlayer] Building visual for {(CharacterClass)PlayerClass} skin={EquippedSkinId}");

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
    [Networked, OnChangedRender(nameof(OnAliveChanged))] public NetworkBool IsAlive { get; set; }

    [Networked, OnChangedRender(nameof(OnReadyStateChanged))] 
    public NetworkBool IsReadyToRestart { get; set; }

    private void OnAliveChanged()
    {
        if (Object.HasInputAuthority)
        {
            if (IsAlive)
            {
                // When revived (e.g. dungeon restart), ensure death UI is hidden on the client
                var hud = FindFirstObjectByType<PlayerHUDController>();
                if (hud != null)
                {
                    hud.HideDeathPopup();
                }
            }
            else
            {
                // Trigger death popup for the local player when IsAlive becomes false
                OnDied?.Invoke();
            }
        }
    }

    public static event Action OnAnyReadyStateChanged;

    private void OnReadyStateChanged() => EvaluateRestartReadiness();

    /// <summary>
    /// Runs on every client whenever a ready flag changes or a player leaves; only the
    /// master client actually triggers the restart.
    /// </summary>
    private static void EvaluateRestartReadiness()
    {
        OnAnyReadyStateChanged?.Invoke();

        // In Shared Mode EVERY client has StateAuthority over its own avatar, so
        // HasStateAuthority is not a host check — gating on it made all N clients
        // fire the restart RPC and DungeonManager.RestartDungeon ran N times.
        // Only the Fusion master client evaluates the all-ready condition.
        if (PhotonManager.Instance == null || !PhotonManager.Instance.IsHost) return;
        if (All.Count == 0 || !All.TrueForAll(p => p.IsReadyToRestart)) return;

        // Clearing the flag is per-avatar StateAuthority work, so ask each owner to
        // do it; locally we can write directly.
        foreach (var p in All)
        {
            if (p.Object == null) continue;
            if (p.Object.HasStateAuthority) p.IsReadyToRestart = false;
            else p.RPC_ClearReadyToRestart();
        }

        Debug.Log("[NetworkPlayer] Master client detects all players ready, sending RPC_TriggerRestartDungeon!");

        // Must be sent from an avatar we own. OnChangedRender fires on the avatar whose
        // flag changed, so when a REMOTE player was the last to press Again the master
        // was calling this on a proxy it has no StateAuthority over and Fusion rejected
        // it ("Local simulation is not allowed to send this RPC on NetworkPlayer_2"),
        // leaving every client stuck on "Waiting...". Local is always ours.
        if (Local != null) Local.RPC_TriggerRestartDungeon();
        else Debug.LogWarning("[NetworkPlayer] All players ready but Local is null — cannot send restart RPC.");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_ClearReadyToRestart()
    {
        IsReadyToRestart = false;
    }

    /// <summary>
    /// Best-effort vote cleanup used while this client is already leaving the dungeon.
    /// In Shared Mode the local player normally owns StateAuthority, so write directly
    /// instead of routing the Exit button through an RPC that Fusion may reject while the
    /// runner is migrating rooms.
    /// </summary>
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
            // Room migration/despawn can race this cleanup. Exit has already started, and
            // Unregister() will remove this avatar from the vote list.
            Debug.LogWarning($"[NetworkPlayer] Could not clear restart vote while exiting: {exception.Message}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TriggerRestartDungeon()
    {
        Debug.Log("[NetworkPlayer] Received RPC to RestartDungeon!");
        DungeonManager.Instance?.RestartDungeon();
    }

    /// <summary>
    /// Host → everyone: the backend session id of the NEW run. Only the host calls the
    /// Enter API on restart, so without this members keep the finished run's id and their
    /// claim-reward fails on run 2 (panel falls back to +0 / +0 / --:--).
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SetRestartSession(int sessionId)
    {
        DungeonManager.Instance?.AdoptRestartSession(sessionId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Party chat INSIDE the dungeon
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Party chat received while in the dungeon room. Static because the receiver
    /// (UIChatPanel) has no reason to know which avatar carried the RPC.
    ///
    /// PartyLobby cannot serve chat here: it is a NetworkObject of the SOCIAL LOBBY
    /// room, and entering a dungeon tears the runner down (PhotonManager.MigrateToRoomAsync),
    /// which despawns it and nulls PartyLobby.Local for the whole run. NetworkPlayer is
    /// the only party-wide replicated object that survives into DUNGEON_{hostProfileId} —
    /// and because that room contains exactly the party (capped at PartyLobby.MaxMembers),
    /// "everyone in the room" == "the party". UIDungeonPartyRoster already relies on that.
    /// </summary>
    public static event Action<PartyChatMessageResponse> PartyChatReceived;

    /// <summary>True when party chat can be sent/received over the dungeon room.</summary>
    public static bool CanUsePartyChat =>
        Local != null && Local.Object != null && Local.Runner != null && Local.Runner.IsRunning;

    /// <summary>
    /// Send a party chat message to everyone in the dungeon room. Returns false when
    /// the transport is not ready, so the caller can restore the typed text.
    /// </summary>
    public static bool BroadcastPartyChat(PartyChatMessageResponse message)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.Content)) return false;

        // Each gate logs which one tripped: the caller can only surface a generic
        // "Party chat is not ready", which made a failed send indistinguishable from a
        // dropped RPC.
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

        // Sent from the avatar we own — Fusion rejects RPCs raised on a proxy.
        // Logged with the room + peer count because the two clients silently landing in
        // DIFFERENT rooms looks exactly like a dropped RPC from here: the send "succeeds",
        // the local echo shows, and nobody else ever hears it. If SessionName differs
        // between the two clients' logs, the bug is the migration, not the chat.
        Debug.Log($"[NetworkPlayer] Party chat send | room='{Local.Runner.SessionInfo?.Name}' " +
                  $"peers={Local.Runner.SessionInfo?.PlayerCount} avatars={All.Count} sender={senderId}");

        Local.RPC_PartyChatMessage(
            senderId,
            NetworkChatText.ClampUtf8(senderName, NetworkChatText.MaxSenderNameBytes),
            NetworkChatText.ClampUtf8(message.Content, NetworkChatText.MaxContentBytes),
            NetworkChatText.ClampUtf8(sentAt, NetworkChatText.MaxTimestampBytes));
        return true;
    }

    // Plain `string` params, NOT NetworkString<_N>: a NetworkString always costs its full width
    // and _N counts 32-bit WORDS, so <_32>/<_128>/<_32> summed to 792 bytes against Fusion's
    // 512-byte ceiling — Fusion dropped every send and logged "payload is too large". A string
    // is weaved as variable-length UTF-8, so a short message now costs a few dozen bytes.
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_PartyChatMessage(int senderId, string senderName,
                                      string content, string sentAt)
    {
        if (senderId <= 0) return;

        // Logged so a silent chat can be pinned down: if this line appears but no message
        // shows, the RPC arrived and the UI dropped it (wrong tab / not subscribed); if it
        // never appears on the other client, the RPC itself did not travel.
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

    /// <summary>Clamp a string so it fits a fixed-size NetworkString without Fusion truncating mid-send.</summary>
    private static string TrimForFusion(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

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

    /// <summary>Raised when this player dies (only on the client that owns input authority).</summary>
    public event Action OnDied;

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

        EnsureNameplate();
    }

    private void OnDestroy() => Unregister();

    // Despawned and OnDestroy both run (in either order) depending on whether the
    // avatar leaves the session or the scene unloads, so cleanup is idempotent and
    // lives in one place.
    private void Unregister()
    {
        All.Remove(this);

        if (Local == this) Local = null;

        if (_spawnedVisual != null)
        {
            Destroy(_spawnedVisual);
            _spawnedVisual = null;
        }

        // Exit leaves the room, so the remaining players' "all ready" condition changes
        // without any ready flag changing. Without this re-check, P1 pressing Again then
        // P2 pressing Exit left P1 on "Waiting... (1/2)" forever — nothing ever fired again.
        if (Local != null) EvaluateRestartReadiness();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fusion lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public void ApplyEquippedSkin(int skinId)
    {
        int normalizedSkinId = Mathf.Max(0, skinId);

        WorldState.EquippedSkinId = normalizedSkinId;
        WorldState.SaveToPlayerPrefs();

        if (Object != null && Object.HasStateAuthority)
        {
            EquippedSkinId = normalizedSkinId;
        }

        OnPlayerClassChanged();
    }

    /// <summary>
    /// Record the local player's profile avatar and replicate it if the avatar already
    /// exists. Safe to call before Spawned() — WorldState is written either way and
    /// Spawned() picks it up from there.
    /// </summary>
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

    /// <summary>
    /// Load a player's avatar sprite from Resources/Avatars, falling back to avatar_1 when
    /// the profile has none or the named sprite is missing.
    /// </summary>
    public static Sprite ResolveAvatarSprite(string avatarUrl)
    {
        Sprite sprite = null;
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            sprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
        }

        return sprite != null ? sprite : Resources.Load<Sprite>("Avatars/avatar_1");
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
            // ignoreCase must match PhotonManager's parse, otherwise a lowercase
            // "knight" from the API silently falls back to a different class here.
            if (!Enum.TryParse<CharacterClass>(className, ignoreCase: true, out var parsed))
                parsed = CharacterClass.Knight;
            PlayerClass = (int)parsed;
            PlayerName = WorldState.PlayerName ?? "Player";
            PlayerProfileId = WorldState.PlayerProfileId;
            Level = Mathf.Max(1, WorldState.PlayerLevel);
            AvatarUrl = TrimForFusion(WorldState.AvatarUrl, 30);

            // PlayerSpawner đã hydrate skin trước khi spawn. Dùng state đã có để tránh
            // tải lại toàn bộ inventory ngay sau request bootstrap vừa hoàn tất.
            EquippedSkinId = Mathf.Max(0, WorldState.EquippedSkinId);

            // Anchor spawns at the current world position (e.g. ElfForest ~(11.9,17.8))
            // rather than world origin, then fan out so players don't stack.
            Vector3 spawnBase = ResolveSpawnBase();
            TeleportTo(spawnBase + FanOutOffset(Object.InputAuthority.PlayerId));

            // Read initial stats from PlayerEntity if available (loaded from DB), else fallback
            var pEntity = GetComponent<PlayerEntity>();
            if (MaxHp <= 0)
            {
                MaxHp = (pEntity != null && pEntity.MaxHealth > 0) ? pEntity.MaxHealth : 100;
                CurrentHp = (pEntity != null) ? pEntity.CurrentHealth : MaxHp;
            }

            IsAlive = CurrentHp > 0;
        }

        // Force an initial visual creation since OnChangedRender might not fire for default/initial values
        OnPlayerClassChanged();
        RefreshNameplate();

        if (Object.HasInputAuthority)
        {
            Local = this;
            name = "NetworkPlayer_Local";

            // Configure skills only after this dungeon avatar owns local input. This
            // keeps combat independent from SkillPanel.OnEnable, so the player can cast
            // immediately without opening the panel once.
            var localCombat = GetComponent<PlayerCombat>();
            if (localCombat != null) localCombat.LoadEquippedSkills();

            var hud = FindFirstObjectByType<PlayerHUDController>(FindObjectsInactive.Include);
            if (hud != null)
            {
                hud.SubscribeToLocalPlayer(this);
                if (!IsAlive)
                {
                    // Force death UI if they spawned dead, as OnChanged might not fire or fired early
                    hud.ShowDeathPopup();
                }
                else
                {
                    hud.HideDeathPopup();
                }
            }

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

    /// <summary>
    /// Stop this avatar from physically colliding with the other players' avatars.
    ///
    /// Every avatar — local AND remote proxies — is a DYNAMIC Rigidbody2D with a
    /// non-trigger CapsuleCollider2D on the Default layer, and the 2D collision
    /// matrix is fully enabled. A remote proxy is not driven by physics: NetworkTransform
    /// writes its position in Render(), i.e. it TELEPORTS every frame. When such a
    /// teleport lands it overlapping the local avatar, the solver sees a deep
    /// interpenetration and answers with a large depenetration impulse. That fights the
    /// local velocity write every single tick, which reads to the player as
    /// "movement is slow and stutters" — and only ever with 2+ players in the dungeon,
    /// because solo there is no second body to contact.
    ///
    /// Pair-wise Physics2D.IgnoreCollision instead of a Player physics layer: the
    /// avatars sit on Default (layer 0) and are found by tag, and several systems
    /// (skill overlaps, enemy aggro, world interactables) raycast against that layer.
    /// Moving avatars to the unused "Player" layer would silently change what all of
    /// those hit. Ignoring the specific pairs removes ONLY player-vs-player contact
    /// response and leaves every query untouched.
    /// </summary>
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

    /// <summary>
    /// Per-player offset from a shared spawn anchor, so party members never land on
    /// top of each other. This matters beyond looks: the avatar is a DYNAMIC
    /// Rigidbody2D with a non-trigger CapsuleCollider2D and players collide with
    /// each other, so two avatars teleported to the exact same point are interpenetrating
    /// and the physics solver holds them there — which reads to the player as
    /// "can't move at all after joining a dungeon together".
    ///
    /// Golden angle instead of a fixed 60° step: 60° wrapped around after 6 players
    /// so PlayerId 1 and 7 spawned on top of each other. Radius grows slowly so a
    /// large party spirals outward instead of ringing up.
    /// </summary>
    public static Vector3 FanOutOffset(int playerId)
    {
        int playerIndex = Mathf.Max(0, playerId - 1);
        float angle = playerIndex * 137.508f * Mathf.Deg2Rad;
        float radius = 2.5f * Mathf.Sqrt(1f + playerIndex * 0.35f);
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
    }

    /// <summary>
    /// Move this avatar without the remote clients interpolating across the gap.
    /// Writing transform.position directly makes NetworkTransform treat it as
    /// movement, so remotes see the avatar slide in from the previous position.
    /// </summary>
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
        sr.sprite = SolidSprite(characterClass);
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

    // One sprite per class, kept for the lifetime of the app. Building it per
    // fallback visual leaked a Texture2D on every class/skin change, and the
    // visual is rebuilt several times per spawn.
    private static readonly Dictionary<CharacterClass, Sprite> _fallbackSprites = new();

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

    public override void Despawned(NetworkRunner runner, bool hasState) => Unregister();

    /// <summary>
    /// Render-phase callback. Runs every Unity Update after simulation has settled.
    /// Drives animation and other per-frame visual state from the latest network
    /// values (movement, alive/dead).
    /// </summary>
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

    private void RefreshNameplate()
    {
        EnsureNameplate();
        if (_nameplateText == null) return;

        ApplyNameplateStyle();
        string displayName = PlayerName.ToString().Trim();
        _nameplateText.text = displayName;
        _nameplateCanvas.gameObject.SetActive(!string.IsNullOrWhiteSpace(displayName));
    }

    private void ApplyNameplateStyle()
    {
        if (_nameplateText == null) return;

        if (nameplateFont != null)
        {
            _nameplateText.font = nameplateFont;
            _nameplateText.fontSharedMaterial = nameplateFont.material;
        }

        // Assign outline after the font material because changing the material can
        // restore its default face/outline values.
        _nameplateText.outlineWidth = nameplateOutlineWidth;
        _nameplateText.outlineColor = Color.black;

        bool isLocalPlayer = Object != null && Object.HasInputAuthority;
        _nameplateText.color = isLocalPlayer ? localNameplateColor : remoteNameplateColor;
    }


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
            // Movement is velocity-driven, so an input-less tick must actively stop the
            // body. Returning bare would leave the previous velocity integrating and the
            // avatar would drift on its own until the next tick that carries input.
            _movement.Move(Vector2.zero, Runner.DeltaTime);
            NetworkedMove = Vector2.zero;
            return;
        }
        var inputData = input.Value;

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

    public void ApplyDamage(int amount, bool isCritical = false)
    {
        if (!Object.HasStateAuthority) return;
        if (!IsAlive) return;

        int previousHp = CurrentHp;
        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        int appliedDamage = previousHp - CurrentHp;
        if (appliedDamage > 0)
        {
            RPC_ShowPlayerCombatPopup(transform.position, appliedDamage, isCritical, false);
        }

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
    public void RequestDamage(int amount, bool isCritical = false)
    {
        if (amount <= 0) return;

        if (Object.HasStateAuthority)
            ApplyDamage(amount, isCritical);
        else
            RPC_RequestDamage(amount, isCritical);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDamage(int amount, NetworkBool isCritical)
    {
        ApplyDamage(amount, isCritical);
    }

    public void ApplyHeal(int amount)
    {
        if (!Object.HasStateAuthority) return;
        if (!IsAlive) return;

        int previousHp = CurrentHp;
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        int appliedHeal = CurrentHp - previousHp;
        if (appliedHeal > 0)
        {
            RPC_ShowPlayerCombatPopup(transform.position, appliedHeal, false, true);
        }
    }

    public void RequestHeal(int amount)
    {
        if (amount <= 0) return;

        if (Object.HasStateAuthority)
            ApplyHeal(amount);
        else
            RPC_RequestHeal(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestHeal(int amount)
    {
        ApplyHeal(amount);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowPlayerCombatPopup(Vector3 worldPosition, int amount,
        NetworkBool isCritical, NetworkBool isHeal)
    {
        if (DamagePopupManager.Instance != null)
        {
            DamagePopupManager.Instance.Create(worldPosition, amount, isCritical, true, isHeal);
        }
    }

    /// <summary>
    /// Replicates a legacy buff visual on the player who actually received it.
    /// The copy is marked visual-only before Start runs, so it cannot apply stats
    /// or broadcast another effect.
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ShowBuffVisual(string prefabName)
    {
        GameObject prefab = PlayerCombat.FindLoadedSkillPrefab(prefabName);
        if (prefab == null)
        {
            Debug.LogWarning($"[NetworkPlayer] Cannot resolve buff visual prefab '{prefabName}'.");
            return;
        }

        GameObject effect = Instantiate(prefab, transform.position, Quaternion.identity);
        effect.SetActive(false);
        PlayerSkillVisualReplica.Mark(effect, transform);
        effect.SetActive(true);
    }

    public void Die()
    {
        if (!IsAlive) return;
        IsAlive = false;
        Debug.Log($"[NetworkPlayer] {PlayerName} died.");

        // OnDied is raised from OnAliveChanged() only. Invoking it here as well made
        // the local player get two death popups (OnChangedRender fires on the
        // authority too). Spawning already dead is handled in Spawned().
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetReadyToRestart()
    {
        if (!Object.HasStateAuthority) return;

        IsReadyToRestart = true;
        Debug.Log($"[NetworkPlayer] {PlayerName} is ready to restart.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BossSpawning()
    {
        // Host already executes the sequence locally in DungeonManager, avoid double execution
        if (Object.HasStateAuthority) return;
        
        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.ClientReceiveBossSpawning();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BossDied(Vector3 chestPosition)
    {
        // Host already executes the sequence locally in DungeonManager, avoid double execution
        if (Object.HasStateAuthority) return;

        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.ClientReceiveBossDeath(chestPosition);
        }
    }

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
    public void RPC_WorldRespawn(Vector3 position)
    {
        if (!Object.HasStateAuthority) return;
        if (IsAlive) return;

        CurrentHp = Mathf.Max(1, MaxHp / 10); // 10% HP
        IsAlive = true;
        IsReadyToRestart = false;
        TeleportTo(position);
        Debug.Log($"[NetworkPlayer] {PlayerName} respawned in world at {position} with 10% HP.");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_DungeonRespawn(Vector3 position)
    {
        if (!Object.HasStateAuthority) return;
        
        CurrentHp = MaxHp; // Full HP
        IsAlive = true;
        IsReadyToRestart = false;
        TeleportTo(position);
        Debug.Log($"[NetworkPlayer] {PlayerName} respawned in dungeon at {position} with FULL HP.");
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
