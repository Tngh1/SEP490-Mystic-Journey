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

    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    private NetworkRunner _runner;
    private LocalInputCollector _inputCollector;
    private readonly HashSet<PlayerRef> _spawnedPlayers = new HashSet<PlayerRef>();

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Underlying NetworkRunner. Null until StartAsync completes.</summary>
    public NetworkRunner Runner => _runner;

    /// <summary>True once the runner has successfully entered a session.</summary>
    public bool IsConnected => _runner != null && _runner.IsRunning;

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
            Debug.LogError("[PhotonManager] LocalInputCollector missing on this GameObject. " +
                           "Add it (RequireComponent should enforce this).");
        }
        else
        {
            Debug.Log("[PhotonManager.Awake] LocalInputCollector wired in.");
        }
    }

    private void Start()
    {
        Debug.Log($"[PhotonManager.Start] scene='{gameObject.scene.name}'. " +
                  $"Has MultiplayerBootstrap in scene? {FindAnyObjectByType<MultiplayerBootstrap>() != null}");
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

    /// <param name="sessionName">Room name. Pass null to use <see cref="defaultSessionName"/>.</param>
    public async Task StartAsync(string sessionName = null)
    {
        if (IsConnected)
        {
            Debug.LogWarning("[PhotonManager] StartAsync called while already connected. Ignored.");
            return;
        }

        if (playerPrefab == default)
        {
            Debug.LogError("[PhotonManager] playerPrefab is not assigned in Inspector. " +
                           "Multiplayer cannot start.");
            return;
        }

        // Create the runner on this GameObject. We never instantiate it from a prefab
        // because we want it to live exactly as long as PhotonManager itself.
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

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
        };

        Debug.Log($"[PhotonManager] Connecting to session '{args.SessionName}' (appVersion={appVersion})...");
        var result = await _runner.StartGame(args);

        if (!result.Ok)
        {
            Debug.LogError($"[PhotonManager] StartGame failed: {result.ShutdownReason}");
            Destroy(_runner);
            _runner = null;
            return;
        }

        Debug.Log("<color=green>[PhotonManager] Connected to Photon.</color>");
        // Note: we DO NOT spawn here. Spawning happens in OnPlayerJoined, gated by StateAuthority.
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
        await runner.Shutdown(destroyGameObject: false);

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
        Debug.Log($"[PhotonManager] OnShutdown: {shutdownReason}");
        _spawnedPlayers.Clear();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
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
        Debug.Log("[Fusion] OnInput");

        var data = _inputCollector.Collect();
        Debug.Log($"[Fusion] Move={data.Move}");

        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        Debug.Log("[Fusion] OnInputMissing");
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
        Debug.Log($"[PhotonManager] OnPlayerJoined: {player} (local={runner.LocalPlayer})");

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
            player);

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
        catch { /* WorldState may not be initialized */ }
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

    [SerializeField] private string[] preferredGameplayScenes = { "ElfForest" };

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
        catch { /* WorldState may not be initialized yet */ }

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