using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(LocalInputCollector))]
public class PhotonManager : MonoBehaviour, INetworkRunnerCallbacks
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static PhotonManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Fusion")]
    [Tooltip("Default session/room name used when StartAsync() is called without an argument. " +
             "Production should always pass an explicit session (party code, dungeon id, etc.).")]
    [SerializeField] private string defaultSessionName = "MysticJourney_Default";

    [Tooltip("Photon Fusion App Version. Bump this when shipping a breaking change " +
             "to force a clean split from older clients.")]
    [SerializeField] private string appVersion = "0.1.0";

    [Header("Spawning")]
    [Tooltip("Network prefab spawned by the Host when a new player joins. Must contain " +
             "NetworkObject + NetworkPlayer + the rigidbody/collider hierarchy.")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Tooltip("Network prefab holding the party roster (member list + ready state). " +
             "Spawned once by the host when a party lobby is created. Must contain " +
             "NetworkObject + PartyLobby.")]
    [SerializeField] private NetworkPrefabRef partyLobbyPrefab;

    [Tooltip("Network prefab holding the per-player social presence / invite mailbox. " +
             "Each client spawns ONE for itself on joining the social lobby room. Must " +
             "contain NetworkObject + PlayerPresence. No avatar, no gameplay.")]
    [SerializeField] private NetworkPrefabRef presencePrefab;

    [Header("Social Lobby")]
    [Tooltip("Shared room every online player joins on entering Main. Used purely for " +
             "presence discovery + party invites — no avatars spawn here.")]
    [SerializeField] private string socialLobbySessionName = "MYSTIC_SOCIAL_LOBBY";

    // ─────────────────────────────────────────────────────────────────────────
    // Connection phase
    //
    // The SAME runner serves two very different phases so we do not have to tear
    // it down between the party lobby and the dungeon fight:
    //   • Lobby   — players gather, invite friends, toggle ready. NO gameplay
    //               avatar is spawned and ProvideInput is OFF (nothing to drive).
    //   • Dungeon — the real fight. Avatars spawn (OnPlayerJoined) and input is
    //               collected every tick. This is the original behaviour.
    // Milestone 1 only reaches Lobby; Milestone 2 flips the phase to Dungeon.
    // ─────────────────────────────────────────────────────────────────────────

    public enum PartyPhase { None, Lobby, Dungeon }

    /// <summary>Current connection phase. <see cref="PartyPhase.None"/> when offline.</summary>
    public PartyPhase Phase { get; private set; } = PartyPhase.None;

    /// <summary>Raised when Fusion delivers an updated session list (lobby browsing).</summary>
    public event Action<List<SessionInfo>> OnSessionListChanged;

    /// <summary>Latest session list from the lobby, or empty when not browsing.</summary>
    public IReadOnlyList<SessionInfo> KnownSessions => _knownSessions;

    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    private NetworkRunner _runner;

    // True from the moment StartAsync creates a runner until StartGame resolves.
    // IsConnected is false during that window (the runner exists but is not yet
    // IsRunning), so without this a second concurrent call would AddComponent a
    // second NetworkRunner and overwrite _runner, orphaning a live connection on
    // this DontDestroyOnLoad object for the rest of the session.
    private bool _connecting;

    private LocalInputCollector _inputCollector;
    private readonly HashSet<PlayerRef> _spawnedPlayers = new HashSet<PlayerRef>();
    private readonly List<SessionInfo> _knownSessions = new List<SessionInfo>();

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Underlying NetworkRunner. Null until StartAsync completes.</summary>
    public NetworkRunner Runner => _runner;

    /// <summary>True once the runner has successfully entered a session.</summary>
    public bool IsConnected => _runner != null && _runner.IsRunning;

    /// <summary>
    /// True only during a networked DUNGEON session (avatars + combat replicate).
    /// FALSE while merely connected to the social lobby. Gameplay spawn code
    /// (PlayerSpawner, DungeonSpawner) must gate on THIS, not <see cref="IsConnected"/>,
    /// so being present in the social lobby never triggers networked spawning.
    /// </summary>
    public bool IsDungeonSession => IsConnected && Phase == PartyPhase.Dungeon;

    /// <summary>True if the local client owns StateAuthority (i.e. is the Host).</summary>
    public bool IsHost => _runner != null && _runner.IsSharedModeMasterClient;

    /// <summary>The PlayerRef owned by this local client. Default(PlayerRef) until connected.</summary>
    public PlayerRef LocalPlayerRef => _runner != null ? _runner.LocalPlayer : default;

    /// <summary>Raised after the Host spawns a NetworkObject for a newly-joined player.</summary>
    public event Action<PlayerRef> OnPlayerJoinedNetwork;

    /// <summary>Raised after a player has left and their NetworkObject has been despawned.</summary>
    public event Action<PlayerRef> OnPlayerLeftNetwork;

    /// <summary>
    /// Raised after an explicit local disconnect (e.g. the user clicked "Disconnect").
    /// Fusion despawns the local NetworkPlayer avatar as part of runner shutdown, so
    /// listeners (PlayerSpawner) use this to spawn a non-networked fallback avatar back in.
    /// Not raised on app-quit/scene-teardown shutdown — see <see cref="OnDestroy"/>.
    /// </summary>
    public event Action OnDisconnected;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Debug.Log("[PhotonManager.Awake] Entering — Instance check.");
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PhotonManager.Awake] Duplicate PhotonManager instance detected. Destroying self.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[PhotonManager.Awake] Instance set. GameObject='{gameObject.name}', scene='{gameObject.scene.name}'");

        _inputCollector = GetComponent<LocalInputCollector>();
        if (_inputCollector == null)
        {
            // OnInput dereferences this every tick, so recover instead of only
            // logging — a missing collector used to NRE the whole simulation loop.
            Debug.LogWarning("[PhotonManager] LocalInputCollector missing on this GameObject. Adding one.");
            _inputCollector = gameObject.AddComponent<LocalInputCollector>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Shutdown(notify: false);
            Instance = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Connection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Join the shared SOCIAL LOBBY room (presence + party invites). No avatar is
    /// spawned and no input is collected. Every online client calls this once on
    /// entering Main. Safe to call when already connected — it no-ops. A Photon outage
    /// never blocks the Main scene: it retries a few times, then tells the player they
    /// are offline instead of failing silently (party/invites would just never work).
    /// </summary>
    public async Task JoinSocialLobbyAsync()
    {
        if (IsConnected)
        {
            Debug.Log("[PhotonManager] JoinSocialLobbyAsync: already connected, ignored.");
            return;
        }

        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (await StartAsync(socialLobbySessionName, PartyPhase.Lobby)) return;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhotonManager] Social lobby connect attempt {attempt} threw: {ex.Message}");
            }

            if (attempt < maxAttempts) await Task.Delay(1000 * attempt);
        }

        Debug.LogWarning("[PhotonManager] Could not join the social lobby — party and invites are unavailable.");
        WorldRuntimeEvents.RaiseMessage("Offline: party and invites are unavailable.");
    }

    /// <summary>
    /// Migrate from the social lobby to an isolated DUNGEON room in the Dungeon phase
    /// (avatars spawn, input collected). Cleanly shuts down the current runner FIRST
    /// (awaited, without raising <see cref="OnDisconnected"/> so no local fallback avatar
    /// is spawned mid-transition), then connects to <paramref name="dungeonRoomName"/>.
    /// Every party member calls this with the SAME room name so they land together.
    /// </summary>
    public Task MigrateToDungeonAsync(string dungeonRoomName)
    {
        return MigrateToRoomAsync(dungeonRoomName, PartyPhase.Dungeon);
    }

    /// <summary>
    /// Exit a dungeon room back to the shared SOCIAL LOBBY (Lobby phase — no avatars,
    /// no input). Called when a player leaves the dungeon so the dungeon room's runner
    /// is fully torn down (their avatar despawns for everyone) and they rejoin the
    /// common lobby where invites/party are possible again. Mirrors
    /// <see cref="MigrateToDungeonAsync"/> but targets the lobby room + phase.
    /// </summary>
    public Task MigrateToSocialLobbyAsync()
    {
        return MigrateToRoomAsync(socialLobbySessionName, PartyPhase.Lobby);
    }

    /// <summary>
    /// Shared runner-migration core: tear down the current runner (awaited, no local
    /// fallback avatar), wait out the UserId-release grace window, then connect to the
    /// target room/phase with retry+backoff against the transient reconnect kick.
    /// </summary>
    private async Task MigrateToRoomAsync(string roomName, PartyPhase phase)
    {
        // Tear down the current runner fully before reconnecting (reconnect-safe).
        if (_runner != null)
        {
            var old = _runner;
            old.RemoveCallbacks(this);
            _runner = null;
            _spawnedPlayers.Clear();
            _knownSessions.Clear();
            Phase = PartyPhase.None;

            try { await old.Shutdown(destroyGameObject: false); }
            catch (Exception ex) { Debug.LogWarning($"[PhotonManager] Runner shutdown during migrate threw: {ex.Message}"); }

            if (old != null) Destroy(old);

            // The Photon cloud releases the peer's UserId slightly AFTER Fusion's Shutdown
            // Task completes. Reconnecting to a new room with the same UserId before that
            // release makes the server kick the fresh peer with DisconnectByServerLogic
            // (code 104). A short grace delay lets the old peer fully drain first.
            await Task.Delay(700);
        }

        // The kick-on-reuse (code 104) is TRANSIENT: once the old peer fully drains
        // server-side, the reconnect succeeds. The host holds StateAuthority over shared
        // objects so its peer drains slower and is the one that tends to lose the race —
        // a single fixed delay is not reliable for it. Retry with backoff so whichever
        // client got kicked keeps trying until the slot is free.
        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Debug.Log($"[PhotonManager] Migrating into room '{roomName}' (phase={phase}, attempt {attempt}/{maxAttempts}).");
            bool ok = await StartAsync(roomName, phase);
            if (ok)
            {
                Debug.Log($"[PhotonManager] Migration into '{roomName}' succeeded on attempt {attempt}.");
                return;
            }

            if (attempt < maxAttempts)
            {
                int backoff = 600 * attempt; // 600, 1200, 1800, 2400 ms
                Debug.LogWarning($"[PhotonManager] Migration attempt {attempt} failed — retrying in {backoff}ms.");
                await Task.Delay(backoff);
            }
        }

        Debug.LogError($"[PhotonManager] Migration into '{roomName}' FAILED after {maxAttempts} attempts.");
    }

    /// <summary>
    /// Connect to a room as a PARTY LOBBY: players gather + ready-check, but no
    /// gameplay avatar is spawned and no input is collected. Milestone 1 flow.
    /// The host passes its own party room name (e.g. "PARTY_&lt;profileId&gt;"); an
    /// invited friend passes the SAME name to join.
    /// </summary>
    public Task StartPartyLobbyAsync(string sessionName)
    {
        return StartAsync(sessionName, PartyPhase.Lobby);
    }

    /// <summary>
    /// Legacy/direct entry: connect straight into the dungeon phase (avatars spawn,
    /// input collected). Kept for the old MultiplayerBootstrap test panel and any
    /// caller that wants the original one-step connect.
    /// </summary>
    /// <param name="sessionName">Room name. Pass null to use <see cref="defaultSessionName"/>.</param>
    public Task StartAsync(string sessionName = null)
    {
        return StartAsync(sessionName, PartyPhase.Dungeon);
    }

    /// <summary>Connect the runner to a session. Returns true on success, false if the
    /// StartGame call was rejected (e.g. a transient reconnect kick) so callers can retry.</summary>
    private async Task<bool> StartAsync(string sessionName, PartyPhase phase)
    {
        if (IsConnected)
        {
            Debug.LogWarning("[PhotonManager] StartAsync called while already connected. Ignored.");
            return true;
        }

        if (_connecting)
        {
            Debug.LogWarning("[PhotonManager] StartAsync called while a connect is already in flight. Ignored.");
            return false;
        }

        if (playerPrefab == default)
        {
            Debug.LogError("[PhotonManager] playerPrefab is not assigned in Inspector. " +
                           "Multiplayer cannot start.");
            return false;
        }

        _connecting = true;
        try
        {
            Phase = phase;

            // Create the runner on this GameObject. We never instantiate it from a prefab
            // because we want it to live exactly as long as PhotonManager itself.
            _runner = gameObject.AddComponent<NetworkRunner>();
            // Input is only collected in the dungeon phase — the lobby has no avatar to drive.
            _runner.ProvideInput = phase == PartyPhase.Dungeon;

            // Use a pass-through scene manager so Fusion does NOT unload the current scenes
            // (Main UI, ElfForest world, etc.). We want to netcode the scene we are already in.
            var sceneManager = GetComponent<MysticJourney.Networking.PassThroughSceneManager>();
            if (sceneManager == null)
            {
                sceneManager = gameObject.AddComponent<MysticJourney.Networking.PassThroughSceneManager>();
            }

            _runner.AddCallbacks(this);

            // Still hand Fusion a SceneRef so it knows where to spawn NetworkObjects,
            // even though our custom scene manager will not actually load the scene.
            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = string.IsNullOrWhiteSpace(sessionName) ? defaultSessionName : sessionName,
                Scene = ResolveGameplayScene(),
                SceneManager = sceneManager,
                CustomPhotonAppSettings = BuildAppSettings(),
            };

            Debug.Log($"[PhotonManager] Connecting to session '{args.SessionName}' " +
                      $"(phase={phase}, appVersion={appVersion})...");
            var result = await _runner.StartGame(args);

            if (!result.Ok)
            {
                Debug.LogError($"[PhotonManager] StartGame failed: {result.ShutdownReason}");
                Phase = PartyPhase.None;
                _runner.RemoveCallbacks(this);
                Destroy(_runner);
                _runner = null;
                return false;
            }

            Debug.Log($"<color=green>[PhotonManager] Connected to Photon (phase={phase}).</color>");
            // In the lobby phase we do NOT spawn a gameplay avatar — OnPlayerJoined is
            // guarded on Phase. Milestone 2 flips Phase to Dungeon to start spawning.
            return true;
        }
        finally
        {
            // Cleared on every exit path (success, rejection, and the exception
            // StartGame can throw) so a failed connect never wedges the guard.
            _connecting = false;
        }
    }

    /// <summary>
    /// Photon settings for this connect, with <see cref="appVersion"/> applied.
    /// StartGameArgs has no AppVersion field and PhotonAppSettings.Global is
    /// read-only, so the only way to version-split the matchmaking pool is to pass
    /// a modified copy of the global settings. Without this, builds of different
    /// versions land in the same rooms and desync.
    /// </summary>
    private Fusion.Photon.Realtime.FusionAppSettings BuildAppSettings()
    {
        var global = Fusion.Photon.Realtime.PhotonAppSettings.Global;
        if (global == null || global.AppSettings == null) return null;

        // Copy: mutating Global would leak the version into the asset on disk.
        var settings = global.AppSettings.GetCopy();
        settings.AppVersion = string.IsNullOrWhiteSpace(appVersion) ? string.Empty : appVersion.Trim();
        return settings;
    }

    /// <summary>
    /// Spawn this client's own <see cref="PlayerPresence"/> in the social lobby room.
    /// Called from <see cref="OnPlayerJoined"/> for the local player during the Lobby
    /// phase. Identity is copied from WorldState in the Spawn initializer so it is
    /// populated before first replication, and re-published later via
    /// <see cref="PlayerPresence.RefreshLocal"/> once the API hydration finishes.
    /// No-ops if a local presence already exists.
    /// </summary>
    private void SpawnLocalPresence(NetworkRunner runner)
    {
        if (PlayerPresence.Local != null) return;

        if (presencePrefab == default)
        {
            Debug.LogError("[PhotonManager] presencePrefab is not assigned in Inspector. " +
                           "Social presence / party invites cannot work.");
            return;
        }

        runner.Spawn(
            presencePrefab,
            Vector3.zero,
            Quaternion.identity,
            runner.LocalPlayer,
            (r, obj) => obj.GetComponent<PlayerPresence>()?.ApplyWorldState());
    }

    /// <summary>
    /// Create a new <see cref="PartyLobby"/> owned by the local player (host). In Shared
    /// Mode the spawning client automatically holds StateAuthority over it. No-ops and
    /// returns the existing party if the local player is already in one. Returns null if
    /// the prefab is unassigned or the runner is not connected.
    /// </summary>
    public PartyLobby CreateParty()
    {
        if (_runner == null || !_runner.IsRunning) return null;

        if (PartyLobby.Local != null) return PartyLobby.Local;

        if (partyLobbyPrefab == default)
        {
            Debug.LogError("[PhotonManager] partyLobbyPrefab is not assigned in Inspector. " +
                           "Cannot spawn the party roster.");
            return null;
        }

        var obj = _runner.Spawn(partyLobbyPrefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);
        return obj != null ? obj.GetComponent<PartyLobby>() : null;
    }

    /// <summary>
    /// Disconnect from the current session and tear down the runner. Safe to call when not connected.
    /// </summary>
    /// <param name="notify">
    /// When true (default), raises <see cref="OnDisconnected"/> so a non-networked fallback
    /// player is spawned back in. Pass false during app-quit/scene-teardown (see <see cref="OnDestroy"/>)
    /// where spawning a new GameObject would be pointless or unsafe.
    /// </param>
    public void Shutdown(bool notify = true)
    {
        if (_runner == null)
            return;

        Debug.Log("[PhotonManager] Shutting down runner...");

        var runnerToShutdown = _runner;
        runnerToShutdown.RemoveCallbacks(this);
        _runner = null;
        _spawnedPlayers.Clear();
        _knownSessions.Clear();
        Phase = PartyPhase.None;

        ShutdownRunnerAsync(runnerToShutdown, notify);
    }

    /// <summary>
    /// Awaits Fusion's own <see cref="NetworkRunner.Shutdown"/> (which returns a Task, not void)
    /// before destroying the runner and raising <see cref="OnDisconnected"/>.
    ///
    /// Root cause this fixes: the old code called <c>_runner.Shutdown()</c> without awaiting it,
    /// then immediately invoked <c>OnDisconnected</c> synchronously. Fusion despawns the local
    /// NetworkPlayer avatar (and destroys its GameObject) as part of that Shutdown Task, which
    /// does NOT complete within the same call — it finishes on a later frame. PlayerSpawner's
    /// respawn guard (<c>FindFirstObjectByType&lt;PlayerMovement&gt;()</c>) ran too early, still
    /// found the not-yet-destroyed NetworkPlayer's PlayerMovement, and bailed out — leaving the
    /// player with no controllable character and no camera target after disconnect.
    /// By awaiting here, OnDisconnected only fires once Fusion's own shutdown/despawn has
    /// actually finished.
    ///
    /// Root cause of the "PhotonManager lost after disconnect" bug: <c>NetworkRunner.Shutdown</c>
    /// takes a <c>destroyGameObject</c> parameter that defaults to <c>true</c>. Since <see cref="StartAsync"/>
    /// adds the NetworkRunner component onto THIS SAME GameObject (<c>gameObject.AddComponent&lt;NetworkRunner&gt;()</c>),
    /// calling <c>runner.Shutdown()</c> with no arguments told Fusion to destroy that entire
    /// GameObject — not just the NetworkRunner component. That took PhotonManager itself (and
    /// LocalInputCollector, PassThroughSceneManager) down with it, which ran <see cref="OnDestroy"/>
    /// and nulled out <see cref="Instance"/>, exactly matching the "PhotonManager not found in
    /// scene" message from MultiplayerBootstrap's debug panel. We pass <c>destroyGameObject: false</c>
    /// so Fusion only tears down its own internal state, and we destroy just the NetworkRunner
    /// component ourselves right after — leaving the PhotonManager GameObject (and singleton) alive.
    /// </summary>
    private async void ShutdownRunnerAsync(NetworkRunner runner, bool notify)
    {
        // async void: an exception out of Shutdown would be unobserved and would skip
        // Destroy + OnDisconnected, leaving a dead runner component on this GameObject
        // and no fallback avatar. Swallow it so teardown always completes.
        try { await runner.Shutdown(destroyGameObject: false); }
        catch (Exception ex) { Debug.LogWarning($"[PhotonManager] Runner shutdown threw: {ex.Message}"); }

        if (runner != null)
        {
            Destroy(runner);
        }

        Debug.Log("[PhotonManager] Runner shutdown complete. Instance still alive: " + (Instance != null));

        if (notify)
        {
            OnDisconnected?.Invoke();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INetworkRunnerCallbacks — connection lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[PhotonManager] OnConnectedToServer");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"[PhotonManager] OnDisconnectedFromServer: {reason}");
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        request.Accept();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[PhotonManager] OnConnectFailed: {reason}");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (shutdownReason == ShutdownReason.Ok || shutdownReason == ShutdownReason.GameClosed)
            Debug.Log($"[PhotonManager] OnShutdown: {shutdownReason} (intentional)");
        else
            Debug.LogError($"[PhotonManager] OnShutdown: {shutdownReason}");

        _spawnedPlayers.Clear();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        _knownSessions.Clear();
        if (sessionList != null)
            _knownSessions.AddRange(sessionList);

        Debug.Log($"[PhotonManager] OnSessionListUpdated: {_knownSessions.Count} session(s).");

        // Hand out a snapshot: subscribers that keep the list (lobby UI) used to see
        // it emptied under them on the next update, since _knownSessions is reused.
        OnSessionListChanged?.Invoke(new List<SessionInfo>(_knownSessions));
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data)
    {
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INetworkRunnerCallbacks — input
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by Fusion once per tick on the client that owns input authority.
    /// We delegate reading to <see cref="LocalInputCollector"/>; this method is the
    /// only place where <c>input.Set()</c> is allowed.
    /// </summary>
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = _inputCollector.Collect();
        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        input.Set(default(NetworkInputData));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INetworkRunnerCallbacks — scene
    // ─────────────────────────────────────────────────────────────────────────

    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"[PhotonManager] Scene load done. Active scene: {SceneManager.GetActiveScene().name}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INetworkRunnerCallbacks — spawn / despawn
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called on every client when a player connects. In Fusion Shared Mode each
    /// client is responsible for spawning ONLY its own avatar (so it holds both
    /// State + Input authority over it and NetworkTransform replicates its movement
    /// to everyone else). We therefore ignore joins for other players — their
    /// avatars arrive as replicated NetworkObjects.
    /// </summary>
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[PhotonManager] OnPlayerJoined: {player} (local={runner.LocalPlayer}, phase={Phase})");

        // Lobby phase: no gameplay avatar. Each client spawns ONLY its own lightweight
        // PlayerPresence (identity + invite mailbox) so others can discover/invite it.
        if (Phase != PartyPhase.Dungeon)
        {
            if (player == runner.LocalPlayer)
            {
                SpawnLocalPresence(runner);
            }
            return;
        }

        if (player != runner.LocalPlayer)
        {
            return;
        }

        if (_spawnedPlayers.Contains(player))
        {
            return;
        }

        var spawnPosition = ResolveSpawnPosition();
        var playerObject = runner.Spawn(
            playerPrefab,
            spawnPosition,
            Quaternion.identity,
            player,
            (r, obj) =>
            {
                var netPlayer = obj.GetComponent<NetworkPlayer>();
                if (netPlayer != null)
                {
                    string className = WorldState.PlayerClass ?? "Knight";
                    if (!Enum.TryParse<CharacterClass>(className, true, out var parsed))
                        parsed = CharacterClass.Knight;

                    netPlayer.PlayerClass = (int)parsed;
                    netPlayer.PlayerName = WorldState.PlayerName ?? "Player";
                    netPlayer.PlayerProfileId = WorldState.PlayerProfileId;
                    netPlayer.Level = Mathf.Max(1, WorldState.PlayerLevel);
                    netPlayer.EquippedSkinId = Mathf.Max(0, WorldState.EquippedSkinId);
                }
            });

        if (playerObject != null)
        {
            _spawnedPlayers.Add(player);
            OnPlayerJoinedNetwork?.Invoke(player);
        }
    }

    private static Vector3 ResolveSpawnPosition()
    {
        try
        {
            Vector3 last = WorldState.LastPosition;
            if (last != Vector3.zero
                && !float.IsNaN(last.x) && !float.IsNaN(last.y) && !float.IsNaN(last.z)
                && !float.IsInfinity(last.x) && !float.IsInfinity(last.y) && !float.IsInfinity(last.z))
            {
                return last;
            }
        }
        catch (Exception ex)
        {
            // Was an empty catch: a real failure here silently spawned everyone at origin.
            Debug.LogWarning($"[PhotonManager] Could not read WorldState.LastPosition, spawning at origin: {ex.Message}");
        }
        return Vector3.zero;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[PhotonManager] OnPlayerLeft: {player}");

        // In Shared Mode the leaving player owns StateAuthority over its own avatar,
        // so Fusion despawns that NetworkObject automatically. We only clear local
        // bookkeeping here.
        _spawnedPlayers.Remove(player);
        OnPlayerLeftNetwork?.Invoke(player);
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scene resolution
    // ─────────────────────────────────────────────────────────────────────────

    private static SceneRef ResolveGameplayScene()
    {
        // Preferred order:
        //  1. WorldState.CurrentMapName (e.g. "ElfForest", "AbandonedMines")
        //  2. A scene named "Main" if it's already loaded additively (so its UI survives)
        //  3. The currently active scene
        //  4. ElfForest as a hardcoded fallback
        var candidates = new List<string>(8);
        try
        {
            var ws = WorldState.CurrentMapName;
            if (!string.IsNullOrWhiteSpace(ws))
                candidates.Add(ws);
        }
        catch (Exception ex)
        {
            // Was an empty catch: hid the reason the current map was skipped as a candidate.
            Debug.LogWarning($"[PhotonManager] Could not read WorldState.CurrentMapName: {ex.Message}");
        }

        // Prefer Main if it is already loaded additively so Fusion does not single-load
        // a fresh Map scene that would wipe out Main's existing UI / systems.
        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.IsValid() && mainScene.isLoaded)
            candidates.Add("Main");

        candidates.Add(SceneManager.GetActiveScene().name);
        candidates.Add("ElfForest");

        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c))
                continue;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(name, c, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[PhotonManager] Resolved gameplay scene '{c}' at build index {i} " +
                              $"(path={path}, activeScene='{SceneManager.GetActiveScene().name}', " +
                              $"loadedScenes=[{string.Join(",", GetLoadedSceneNames())}])");
                    return SceneRef.FromIndex(i);
                }
            }
        }

        Debug.LogWarning($"[PhotonManager] No preferred gameplay scene found in Build Settings. " +
                         $"Falling back to active scene '{SceneManager.GetActiveScene().name}'.");
        return SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
    }

    private static IEnumerable<string> GetLoadedSceneNames()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.IsValid()) yield return s.name;
        }
    }
}