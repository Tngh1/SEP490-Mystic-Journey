using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

// Executes core business logic for i network runner callbacks.
[RequireComponent(typeof(LocalInputCollector))]
public class PhotonManager : MonoBehaviour, INetworkRunnerCallbacks
{

    // Executes core business logic for instance.
    public static PhotonManager Instance { get; private set; }


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


    // Executes core business logic for party phase.
    public enum PartyPhase { None, Lobby, Dungeon }

    // Executes core business logic for phase.
    public PartyPhase Phase { get; private set; } = PartyPhase.None;

    public event Action<List<SessionInfo>> OnSessionListChanged;

    // Executes core business logic for known sessions.
    public IReadOnlyList<SessionInfo> KnownSessions => _knownSessions;


    private NetworkRunner _runner;

    private bool _connecting;

    private LocalInputCollector _inputCollector;
    private readonly HashSet<PlayerRef> _spawnedPlayers = new HashSet<PlayerRef>();
    private readonly List<SessionInfo> _knownSessions = new List<SessionInfo>();


    // Executes core business logic for runner.
    public NetworkRunner Runner => _runner;

    // Executes core business logic for is connected.
    public bool IsConnected => _runner != null && _runner.IsRunning;

    // Executes core business logic for is dungeon session.
    public bool IsDungeonSession => IsConnected && Phase == PartyPhase.Dungeon;

    // Executes core business logic for is host.
    public bool IsHost => _runner != null && _runner.IsSharedModeMasterClient;

    // Executes core business logic for local player ref.
    public PlayerRef LocalPlayerRef => _runner != null ? _runner.LocalPlayer : default;

    public event Action<PlayerRef> OnPlayerJoinedNetwork;

    public event Action<PlayerRef> OnPlayerLeftNetwork;

    public event Action OnDisconnected;


    // Initializes persistent network manager singleton and ensures local input collector is attached.
    private void Awake()
    {
        Debug.Log("[PhotonManager.Awake] Entering — Instance check.");
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PhotonManager.Awake] Duplicate PhotonManager instance detected. Destroying self.");
            Destroy(gameObject); // Prevent multiple network managers
            return;
        }

        Instance = this; // Cache singleton
        DontDestroyOnLoad(gameObject); // Persist across scene switches
        Debug.Log($"[PhotonManager.Awake] Instance set. GameObject='{gameObject.name}', scene='{gameObject.scene.name}'");

        _inputCollector = GetComponent<LocalInputCollector>();
        if (_inputCollector == null)
        {
            Debug.LogWarning("[PhotonManager] LocalInputCollector missing on this GameObject. Adding one.");
            _inputCollector = gameObject.AddComponent<LocalInputCollector>(); // Attach local input polling script
        }
    }

    // Shuts down network runner and clears manager reference on destruction.
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Shutdown(notify: false); // Terminate runner cleanly
            Instance = null;
        }
    }


    // Connects to global shared presence room for discovering online players and routing party invites.
    public async Task JoinSocialLobbyAsync()
    {
        if (IsConnected)
        {
            Debug.Log("[PhotonManager] JoinSocialLobbyAsync: already connected, ignored.");
            return; // Ignore if already online
        }

        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (await StartAsync(socialLobbySessionName, PartyPhase.Lobby)) return; // Attempt connection to social lobby
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhotonManager] Social lobby connect attempt {attempt} threw: {ex.Message}");
            }

            if (attempt < maxAttempts) await Task.Delay(1000 * attempt); // Exponential backoff retry
        }

        Debug.LogWarning("[PhotonManager] Could not join the social lobby — party and invites are unavailable.");
        WorldRuntimeEvents.RaiseMessage("Offline: party and invites are unavailable.");
    }

    // Migrates local player connection into isolated dungeon room instance.
    public Task MigrateToDungeonAsync(string dungeonRoomName)
    {
        return MigrateToRoomAsync(dungeonRoomName, PartyPhase.Dungeon); // Connect to dungeon room
    }

    // Migrates local player connection back to shared social lobby room.
    public Task MigrateToSocialLobbyAsync()
    {
        return MigrateToRoomAsync(socialLobbySessionName, PartyPhase.Lobby); // Return to social lobby
    }

    // Shuts down existing Fusion runner and re-initializes in the destination room with exponential backoff.
    private async Task MigrateToRoomAsync(string roomName, PartyPhase phase)
    {
        if (_runner != null)
        {
            var old = _runner;
            old.RemoveCallbacks(this); // Detach callbacks
            _runner = null;
            _spawnedPlayers.Clear();
            _knownSessions.Clear();
            Phase = PartyPhase.None;

            try { await old.Shutdown(destroyGameObject: false); } // Shutdown old runner
            catch (Exception ex) { Debug.LogWarning($"[PhotonManager] Runner shutdown during migrate threw: {ex.Message}"); }

            if (old != null) Destroy(old);

            await Task.Delay(2000); // Allow socket cleanup
        }

        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            Debug.Log($"[PhotonManager] Migrating into room '{roomName}' (phase={phase}, attempt {attempt}/{maxAttempts}).");
            bool ok = await StartAsync(roomName, phase); // Launch new runner in target room
            if (ok)
            {
                Debug.Log($"[PhotonManager] Migration into '{roomName}' succeeded on attempt {attempt}.");
                return; // Success
            }

            if (attempt < maxAttempts)
            {
                int backoff = 600 * attempt;
                Debug.LogWarning($"[PhotonManager] Migration attempt {attempt} failed — retrying in {backoff}ms.");
                await Task.Delay(backoff); // Backoff before retry
            }
        }

        Debug.LogError($"[PhotonManager] Migration into '{roomName}' FAILED after {maxAttempts} attempts.");
    }

    // Executes core business logic for start party lobby async.
    // Completes asynchronously upon successful execution.
    public Task StartPartyLobbyAsync(string sessionName)
    {
        return StartAsync(sessionName, PartyPhase.Lobby);
    }

    // Executes core business logic for start async.
    // Completes asynchronously upon successful execution.
    public Task StartAsync(string sessionName = null)
    {
        return StartAsync(sessionName, PartyPhase.Dungeon);
    }

    // Executes core business logic for start async.
    // Returns the computed bool result asynchronously.
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

            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = phase == PartyPhase.Dungeon;

            var sceneManager = GetComponent<MysticJourney.Networking.PassThroughSceneManager>();
            if (sceneManager == null)
            {
                sceneManager = gameObject.AddComponent<MysticJourney.Networking.PassThroughSceneManager>();
            }

            _runner.AddCallbacks(this);

            var args = new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = string.IsNullOrWhiteSpace(sessionName) ? defaultSessionName : sessionName,
                Scene = ResolveGameplayScene(),
                SceneManager = sceneManager,
                CustomPhotonAppSettings = BuildAppSettings(),
                PlayerCount = phase == PartyPhase.Dungeon ? PartyLobby.MaxMembers : (int?)null,
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
            return true;
        }
        finally
        {
            _connecting = false;
        }
    }

    // Executes core business logic for build app settings.
    // Logic details: validates required non-empty string arguments.
    private Fusion.Photon.Realtime.FusionAppSettings BuildAppSettings()
    {
        var global = Fusion.Photon.Realtime.PhotonAppSettings.Global;
        if (global == null || global.AppSettings == null) return null;

        var settings = global.AppSettings.GetCopy();
        settings.AppVersion = string.IsNullOrWhiteSpace(appVersion) ? string.Empty : appVersion.Trim();
        return settings;
    }

    // Executes core business logic for spawn local presence.
    private void SpawnLocalPresence(NetworkRunner runner)
    {
        if (PlayerPresence.Local != null) return;

        if (presencePrefab == default)
        {
            Debug.LogError("[PhotonManager] presencePrefab is not assigned in Inspector. " +
                           "Social presence / party invites cannot work.");
            return;
        }

        // Spawn through Fusion so state authority and replication are assigned consistently.
        runner.Spawn(
            presencePrefab,
            Vector3.zero,
            Quaternion.identity,
            runner.LocalPlayer,
            (r, obj) => obj.GetComponent<PlayerPresence>()?.ApplyWorldState());
    }

    // Executes core business logic for create party.
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

        // Spawn through Fusion so state authority and replication are assigned consistently.
        var obj = _runner.Spawn(partyLobbyPrefab, Vector3.zero, Quaternion.identity, _runner.LocalPlayer);
        return obj != null ? obj.GetComponent<PartyLobby>() : null;
    }

    // Executes core business logic for shutdown.
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

    // Executes core business logic for shutdown runner async.
    private async void ShutdownRunnerAsync(NetworkRunner runner, bool notify)
    {
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


    // Executes core business logic for on connected to server.
    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[PhotonManager] OnConnectedToServer");
    }

    // Executes core business logic for on disconnected from server.
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"[PhotonManager] OnDisconnectedFromServer: {reason}");
    }

    // Executes core business logic for on connect request.
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        request.Accept();
    }

    // Executes core business logic for on connect failed.
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[PhotonManager] OnConnectFailed: {reason}");
    }

    // Executes core business logic for on shutdown.
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (shutdownReason == ShutdownReason.Ok || shutdownReason == ShutdownReason.GameClosed)
            Debug.Log($"[PhotonManager] OnShutdown: {shutdownReason} (intentional)");
        else
            Debug.LogError($"[PhotonManager] OnShutdown: {shutdownReason}");

        _spawnedPlayers.Clear();
    }

    // Executes core business logic for on session list updated.
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        _knownSessions.Clear();
        if (sessionList != null)
            _knownSessions.AddRange(sessionList);

        Debug.Log($"[PhotonManager] OnSessionListUpdated: {_knownSessions.Count} session(s).");

        OnSessionListChanged?.Invoke(new List<SessionInfo>(_knownSessions));
    }

    // Executes core business logic for on custom authentication response.
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
    }

    // Executes core business logic for on host migration.
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
    }

    // Executes core business logic for on reliable data received.
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data)
    {
    }

    // Executes core business logic for on reliable data progress.
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
    }

    // Executes core business logic for on user simulation message.
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
    }


    // Executes core business logic for on input.
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = _inputCollector.Collect();
        input.Set(data);
    }

    // Executes core business logic for on input missing.
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        input.Set(default(NetworkInputData));
    }


    // Executes core business logic for on scene load start.
    public void OnSceneLoadStart(NetworkRunner runner)
    {
    }

    // Executes core business logic for on scene load done.
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"[PhotonManager] Scene load done. Active scene: {SceneManager.GetActiveScene().name}");
    }


        // Executes core business logic for on player joined.
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[PhotonManager] OnPlayerJoined: {player} (local={runner.LocalPlayer}, phase={Phase})");

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
            // Spawn through Fusion so state authority and replication are assigned consistently.
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
                        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
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

    // Executes core business logic for resolve spawn position.
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
            Debug.LogWarning($"[PhotonManager] Could not read WorldState.LastPosition, spawning at origin: {ex.Message}");
        }
        return Vector3.zero;
    }

    // Executes core business logic for on player left.
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[PhotonManager] OnPlayerLeft: {player}");

        _spawnedPlayers.Remove(player);
        PartyLobby.Local?.HandleNetworkPlayerLeft(player);
        OnPlayerLeftNetwork?.Invoke(player);
    }

    // Executes core business logic for on object enter aoi.
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }

    // Executes core business logic for on object exit aoi.
    // Logic details: validates required non-empty string arguments.
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
    }


    // Executes core business logic for resolve gameplay scene.
    // Logic details: validates required non-empty string arguments.
    private static SceneRef ResolveGameplayScene()
    {
        var candidates = new List<string>(8);
        try
        {
            var ws = WorldState.CurrentMapName;
            if (!string.IsNullOrWhiteSpace(ws))
                candidates.Add(ws);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PhotonManager] Could not read WorldState.CurrentMapName: {ex.Message}");
        }

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

    // Executes core business logic for get loaded scene names.
    private static IEnumerable<string> GetLoadedSceneNames()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.IsValid()) yield return s.name;
        }
    }
}
