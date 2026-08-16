using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using Fusion;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("Runtime State")]
    public int CurrentSessionId { get; private set; } = 0;
    public int CurrentDungeonConfigId { get; private set; } = 0;
    public int CurrentDungeonCost { get; private set; } = 0;
    public string CurrentDungeonName { get; private set; } = string.Empty;
    public int EnemiesKilledCount { get; private set; } = 0;
    public bool IsInDungeon { get; private set; } = false;

    // Added to support UI Progress
    public int TotalNormalEnemies => EnemiesKilledCount + _normalEnemies.Count;
    public int BossCount => _bossEnemies.Count;

    /// <summary>
    /// True once the boss is actually dead. The UI must gate "Cleared!" on this, not on
    /// BossCount: between the last normal dying and the boss object existing there is a
    /// ~1.2s shake window (phase BossSpawning) where BossCount is still 0.
    /// </summary>
    public bool IsDungeonCleared => _currentPhase == DungeonPhase.Complete;
    public Dictionary<string, (int killed, int total)> EnemyProgress { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    // Saved position in world map to return to
    public string PreviousMapSceneName { get; private set; } = "AbandonedCastle";
    public Vector3 PreviousPlayerPosition { get; private set; } = Vector3.zero;
    public bool HasPreviousPlayerPosition { get; private set; } = false;

    // ── Per-run enemy tracking (normal monsters and boss are tracked separately) ──
    private readonly List<EnemyEntity> _normalEnemies = new();
    private readonly List<EnemyEntity> _bossEnemies   = new();

    // Every enemy registered this run, INCLUDING ones already dead and removed from the
    // lists above. Registration is idempotent against this set rather than against
    // _normalEnemies, because ReconcileReplicatedEnemies re-scans the scene repeatedly:
    // a corpse still in the scene (death animation / loot) would otherwise be re-added
    // after HandleNormalEnemyDeath removed it, inflating TotalNormalEnemies so the
    // progress panel could never reach killed == total and the run never completed.
    private readonly HashSet<EnemyEntity> _seenEnemies = new();
    private bool bossKilled = false;
    private Vector3 _bossDeathPosition = Vector3.zero;

    // Guards the one-shot "I became master late, re-run the spawn" recovery so a client
    // that legitimately has no enemies cannot loop the pipeline forever.
    private bool _masterSpawnRetried = false;

    // One reconcile loop at a time. The master-retry above re-enters
    // SpawnAndRegisterEnemies recursively, which would otherwise start a second loop
    // polling the same scene.
    private bool _reconcileLoopRunning = false;

    // One-shot guard so the spawn runs exactly once per dungeon entry. Both the
    // sceneLoaded event and TransitionToDungeon kick it off (the event alone proved
    // unreliable: WorldState.CurrentMapName is written from 12 places — including the
    // WorldApi.GetState hydration that lands mid-load — so the event's name check can
    // reject the very scene we just loaded and no enemies ever spawn).
    private bool _spawnStarted = false;
    private bool _isReturningToWorld = false;
    private bool _isRestarting = false;

    // ── Saved state for RestartDungeon ──
    private List<string> _currentPartyMembers = new();
    private string _currentDungeonSceneName = string.Empty;

    private enum DungeonPhase { Normal, BossSpawning, Boss, Complete }
    private DungeonPhase _currentPhase = DungeonPhase.Normal;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            // Con của "Managers" trong Main.unity: DontDestroyOnLoad chỉ có tác dụng trên root,
            // nên detach trước. Nếu không, manager chết ngay khi load scene dungeon — đúng lúc
            // OnSceneLoaded bên dưới cần chạy.
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // Only the surviving singleton owns the subscription. Awake destroys duplicates,
        // and their OnDestroy must not touch the event.
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }
    private GameObject FindPlayerInstance()
    {
        GameObject found = null;

        // Prefer the local networked avatar in multiplayer — its own PlayerMovement is
        // on the NetworkPlayer root, whereas FindFirstObjectByType<PlayerMovement> can
        // match the class VISUAL child (which briefly carries a PlayerMovement before
        // CharacterFactory strips it). A child is not a scene root, so returning it
        // makes MoveGameObjectToScene throw "Gameobject is not a root in a scene".
        if (NetworkPlayer.Local != null)
            found = NetworkPlayer.Local.gameObject;

        if (found == null)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) found = pm.gameObject;
        }

        if (found == null)
        {
            var pwi = FindFirstObjectByType<PlayerWorldInteractor>();
            if (pwi != null) found = pwi.gameObject;
        }

        found ??= GameObject.FindWithTag("Player") ??
                  GameObject.Find("Knight") ??
                  GameObject.Find("Mage") ??
                  GameObject.Find("Archer") ??
                  GameObject.Find("Knight(Clone)") ??
                  GameObject.Find("Mage(Clone)") ??
                  GameObject.Find("Archer(Clone)");

        // Always return the scene ROOT: SceneManager.MoveGameObjectToScene rejects any
        // non-root object. Fusion spawns network avatars as scene roots, so transform.root
        // resolves to the avatar root even if we matched a child component above.
        return found != null ? found.transform.root.gameObject : null;
    }

    /// <summary>
    /// Move a player into a scene without letting a failure kill the calling coroutine.
    /// MoveGameObjectToScene throws if the object is not a scene root; guarding it here
    /// means a bad move logs an error but the dungeon transition still completes (avoids
    /// the half-loaded "black screen" hang). Returns true on success.
    /// </summary>
    private static bool SafeMoveToScene(GameObject go, Scene scene)
    {
        if (go == null || !scene.IsValid() || !scene.isLoaded) return false;
        if (go.scene == scene) return true; // already there
        try
        {
            // MoveGameObjectToScene requires a scene root; force it defensively.
            var root = go.transform.root.gameObject;
            SceneManager.MoveGameObjectToScene(root, scene);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DungeonManager] MoveGameObjectToScene('{go.name}' → '{scene.name}') failed: {ex.Message}");
            return false;
        }
    }
    public void StartDungeon(int configId, string dungeonSceneName, int cost, string dungeonName, List<string> partyMembers = null)
    {
        CurrentDungeonConfigId = configId;
        CurrentDungeonCost = cost;
        CurrentDungeonName = dungeonName;
        _currentPartyMembers = partyMembers ?? new List<string>();
        _currentDungeonSceneName = dungeonSceneName;

        EnemiesKilledCount = 0;
        bossKilled = false;
        _currentPhase = DungeonPhase.Normal;
        _normalEnemies.Clear();
        _bossEnemies.Clear();
        _seenEnemies.Clear();
        EnemyProgress.Clear();
        _bossDeathPosition = Vector3.zero;
        _masterSpawnRetried = false;
        _spawnStarted = false;

        // Save current map state to return later
        PreviousMapSceneName = WorldState.CurrentMapName;
        
        // Find player position in the scene
        var player = FindPlayerInstance();
        if (player != null && player.transform.position != Vector3.zero)
        {
            PreviousPlayerPosition = player.transform.position;
            HasPreviousPlayerPosition = true;
        }
        else
        {
            // Fallback to the globally synced position if player is disabled or not found
            PreviousPlayerPosition = WorldState.LastPosition;
            HasPreviousPlayerPosition = true; // Even if it's 0,0,0 it's explicitly saved
        }

        // Call Enter API
        DungeonApi.Instance.Enter(configId, partyMembers ?? new List<string>(),
            onSuccess: response =>
            {
                if (response != null && response.DungeonSessionId > 0)
                {
                    CurrentSessionId = response.DungeonSessionId;
                    IsInDungeon = true;
                    Debug.Log($"[DungeonManager] Session created: {CurrentSessionId}");

                    // Transition to target scene
                    StartCoroutine(TransitionToDungeon(dungeonSceneName));
                }
                else
                {
                    // Was: fall through with CurrentSessionId = -1 "for testing". A dummy id
                    // means every later UpdateProgress/Complete/claim-reward call targets a
                    // session that does not exist, so the run ends on +0 / +0 rewards.
                    Debug.LogWarning("[DungeonManager] Enter API succeeded but returned no session id. Aborting dungeon entry.");
                    NotifyBlocked("Cannot enter dungeon: backend returned no session.");
                }
            },
            onError: error =>
            {
                // Was: proceed into the dungeon anyway. That also silently defeated every
                // server-side entry rule — including the level requirement — because the
                // client ignored the rejection and loaded the scene regardless.
                Debug.LogWarning($"[DungeonManager] Enter API failed: {error.Message}. Aborting dungeon entry.");
                NotifyBlocked($"Cannot enter dungeon: {error.Message}");
            }
        );
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTY DUNGEON ENTRY (multiplayer)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// HOST path: create the backend dungeon session (Enter API) ONCE, then hand the
    /// resulting session id back so the host can publish it to the party. The scene
    /// transition itself is driven separately (after every member has migrated) via
    /// <see cref="EnterDungeonScene"/>. Falls back to a dummy session id on API error
    /// (matching the existing solo behaviour).
    /// </summary>
public void CreatePartySession(int configId, string dungeonSceneName, int cost, string dungeonName,
                                  List<string> partyMembers, Action<int> onReady)
    {
        CurrentDungeonConfigId = configId;
        CurrentDungeonCost = cost;
        CurrentDungeonName = dungeonName;
        _currentPartyMembers = partyMembers ?? new List<string>();
        _currentDungeonSceneName = dungeonSceneName;

        DungeonApi.Instance.Enter(configId, _currentPartyMembers,
            onSuccess: response =>
            {
                CurrentSessionId = response != null ? response.DungeonSessionId : 0;
                if (CurrentSessionId <= 0)
                {
                    Debug.LogWarning("[DungeonManager] Party Enter API returned no session id. Aborting dungeon entry.");
                    NotifyBlocked("Cannot start dungeon: backend returned no session.");
                    onReady?.Invoke(0);
                    return;
                }
                onReady?.Invoke(CurrentSessionId);
            },
            onError: error =>
            {
                Debug.LogWarning($"[DungeonManager] Party Enter API failed: {error.Message}. Aborting dungeon entry.");
                CurrentSessionId = 0;
                NotifyBlocked($"Cannot start dungeon: {error.Message}");
                onReady?.Invoke(0);
            }
        );
    }

    /// <summary>True if the local player is the host of the current party dungeon.</summary>
    public bool IsPartyHost { get; private set; }

    /// <summary>
    /// True when this client is responsible for the backend session: the party host, and
    /// also a solo player. Gating backend writes on <see cref="PhotonManager.IsHost"/>
    /// alone excludes solo, because there is no runner offline so IsHost is false —
    /// progress and Complete were never sent, the session stayed InProgress and
    /// claim-reward failed, leaving the complete panel on +0 / +0 / --:--.
    /// </summary>
    private static bool OwnsSession =>
        PhotonManager.Instance?.IsHost == true || NetworkPlayer.Local == null;

    /// <summary>
    /// EVERY client (host + members): perform the actual scene transition into the
    /// dungeon using an already-established session id (from the host). Does NOT call
    /// the Enter API again — members reuse the host's session. Reuses the existing
    /// <see cref="TransitionToDungeon"/> pipeline so camera/scene handling is identical
    /// to the solo flow. The networked avatar (spawned by PhotonManager on migration)
    /// is picked up by FindPlayerInstance just like a local player.
    /// </summary>
    public void EnterDungeonScene(int configId, string dungeonSceneName, int cost, string dungeonName, int sessionId,
                                  bool hasReturnPoint = false, string returnMapName = null, Vector3 returnPosition = default, bool isHost = false)
    {
        if (sessionId <= 0)
        {
            Debug.LogWarning("[DungeonManager] EnterDungeonScene aborted: invalid session id.");
            NotifyBlocked("Cannot enter dungeon: backend session missing.");
            return;
        }

        IsPartyHost = isHost;
        CurrentDungeonConfigId = configId;
        CurrentDungeonCost = cost;
        CurrentDungeonName = dungeonName?.Trim('\0');
        
        // Clean trailing nulls in case it came from a Fusion NetworkString
        _currentDungeonSceneName = dungeonSceneName?.Trim('\0');
        
        CurrentSessionId = sessionId;
        IsInDungeon = true;

        EnemiesKilledCount = 0;
        bossKilled = false;
        _currentPhase = DungeonPhase.Normal;
        _normalEnemies.Clear();
        _bossEnemies.Clear();
        _seenEnemies.Clear();
        EnemyProgress.Clear();
        _bossDeathPosition = Vector3.zero;
        _masterSpawnRetried = false;
        _spawnStarted = false;

        // Return point: the party path migrates the Photon runner FIRST, which destroys
        // the world avatar and spawns a fresh networked one at a different position — so
        // reading the (post-migration) avatar's transform here would save the wrong
        // "previous" position and exit the dungeon to the wrong spot. The caller
        // (PartyManager) captures the true world position BEFORE migrating and passes it
        // in. Solo entry (StartDungeon) captures it there and does not use this method.
        if (hasReturnPoint)
        {
            PreviousMapSceneName = string.IsNullOrEmpty(returnMapName) ? WorldState.CurrentMapName : returnMapName;
            PreviousPlayerPosition = returnPosition;
            HasPreviousPlayerPosition = true;
        }
        else
        {
            PreviousMapSceneName = WorldState.CurrentMapName;
            var player = FindPlayerInstance();
            if (player != null && player.transform.position != Vector3.zero)
            {
                PreviousPlayerPosition = player.transform.position;
                HasPreviousPlayerPosition = true;
            }
            else
            {
                PreviousPlayerPosition = WorldState.LastPosition;
                HasPreviousPlayerPosition = true;
            }
        }

        StartCoroutine(TransitionToDungeon(dungeonSceneName));
    }

    private IEnumerator TransitionToDungeon(string dungeonSceneName)
    {
        // Che màn hình trước khi unload map: đoạn dưới còn chờ tới 5s cho avatar network
        // xuất hiện, không có loading thì người chơi ngồi nhìn scene trống suốt lúc đó.
        yield return LoadingScreen.Show("Entering dungeon...");

        // Find player first before unloading
        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainScene = SceneManager.GetSceneByName("Main");
            if (SafeMoveToScene(player, mainScene))
                Debug.Log("[DungeonManager] Moved player to Main scene defensively.");
        }

        // Unload any active map scenes defensively (excluding "Main" and the target dungeon scene)
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "Main" && s.name != dungeonSceneName && s.name != LoadingScreen.SceneName && s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // Set LastPosition to zero so player spawner falls back to scene spawnPoint
        WorldState.LastPosition = Vector3.zero;
        WorldState.CurrentMapName = _currentDungeonSceneName;

        // Load dungeon scene additively
        yield return SceneManager.LoadSceneAsync(_currentDungeonSceneName, LoadSceneMode.Additive);

        // Kick the spawn off here rather than trusting the sceneLoaded event to have done it.
        // No-ops if the event already fired.
        BeginEnemySpawn(_currentDungeonSceneName);

        // The local (networked) avatar may not exist yet on a client that just migrated
        // — if it is still null here we skip the teleport and the player is left at the
        // spawn position NetworkPlayer chose (world position + fan-out offset), so the
        // two players end up in different spots. Wait briefly for it so EVERY client
        // teleports its own avatar to the shared PlayerSpawn.
        if (player == null)
        {
            float waitAvatar = 5f;
            while (waitAvatar > 0f && (player = FindPlayerInstance()) == null)
            {
                waitAvatar -= Time.deltaTime;
                yield return null;
            }
        }

        // Move player into the dungeon scene
        if (player != null)
        {
            var dungeonScene = SceneManager.GetSceneByName(_currentDungeonSceneName);
            if (SafeMoveToScene(player, dungeonScene))
                Debug.Log($"[DungeonManager] Moved player into dungeon scene: {_currentDungeonSceneName}");
        }
        else
        {
            Debug.LogWarning("[DungeonManager] Local avatar still null after wait — teleport to PlayerSpawn will be skipped.");
        }

        // Teleport player to the PlayerSpawn position
        GameObject targetSpawnPoint = GameObject.Find("PlayerSpawn") ?? GameObject.Find("SceneTransitionGoblinMine");
        if (targetSpawnPoint == null)
        {
            var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in allTransforms)
            {
                if (t != null && (t.gameObject.name == "PlayerSpawn" || t.gameObject.name == "SceneTransitionGoblinMine") && t.gameObject.scene.name == dungeonSceneName)
                {
                    targetSpawnPoint = t.gameObject;
                    break;
                }
            }
        }

        if (targetSpawnPoint != null)
        {
            Vector3 spawnPos = targetSpawnPoint.transform.position;
            Debug.Log($"[DungeonManager] Found {targetSpawnPoint.name} at {spawnPos}. Teleporting player.");
            if (player != null)
            {
                var np = player.GetComponent<NetworkPlayer>();
                var nt = player.GetComponent<Fusion.NetworkTransform>();
                var entity = player.GetComponent<PlayerEntity>();

                // Every client runs this with the SAME PlayerSpawn, so without a per-player
                // offset all party avatars land on the exact same point. They are DYNAMIC
                // Rigidbody2D bodies with non-trigger colliders that collide with each other,
                // so fully-overlapped avatars are stuck in the solver and nobody can move —
                // the "joined the dungeon together and can't walk" bug. Reuses the same
                // fan-out NetworkPlayer.Spawned already applies at world spawn.
                if (np != null && np.Object != null && np.Object.IsValid)
                    spawnPos += NetworkPlayer.FanOutOffset(np.Object.InputAuthority.PlayerId);

                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.position = spawnPos;
                }

                if (np != null && NetworkPlayer.Local == np && np.Object != null && np.Object.IsValid)
                {
                    np.RPC_DungeonRespawn(spawnPos);
                }
                else if (entity != null)
                {
                    entity.DungeonRespawn(spawnPos);
                    if (nt != null) nt.Teleport(spawnPos);
                }
                else
                {
                    if (nt != null) nt.Teleport(spawnPos);
                    else player.transform.position = spawnPos;
                }

                WorldState.LastPosition = spawnPos;
            }
        }
        else
        {
            Debug.LogWarning("[DungeonManager] PlayerSpawn point not found in scene!");
        }

        // Bind camera to player in target scene
        if (player != null)
        {
            BindCameraToPlayer(player, dungeonSceneName);
        }

        // Keep Main active
        var mainSceneObj = SceneManager.GetSceneByName("Main");
        if (mainSceneObj.IsValid())
        {
            SceneManager.SetActiveScene(mainSceneObj);
        }

        PlayerHUDUIManager.Instance?.ToggleDungeonMode(true);
        Debug.Log($"[DungeonManager] Entered dungeon scene: {dungeonSceneName}");

        yield return LoadingScreen.Hide();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Compare against the dungeon we are actually entering, NOT WorldState.CurrentMapName:
        // that field is written from a dozen places (the WorldApi.GetState hydration lands
        // right in the middle of the additive load) and any of them racing us here made the
        // check reject the dungeon scene, so the spawn never started.
        if (!IsInDungeon || scene.name != _currentDungeonSceneName)
            return;

        BeginEnemySpawn(scene.name);
    }

    /// <summary>
    /// Single entry point for the dungeon spawn. Called both from the sceneLoaded event and
    /// directly by <see cref="TransitionToDungeon"/> once the additive load finishes, so a
    /// missed event cannot leave the run with zero enemies. The flag keeps it one-shot.
    /// </summary>
    private void BeginEnemySpawn(string mapName)
    {
        _isRestarting = false;
        if (_spawnStarted) return;
        _spawnStarted = true;

        Debug.Log($"[DungeonManager] Dungeon scene loaded: {mapName}. Starting spawn + registration...");

        // Try to use DungeonSpawner for data-driven spawning.
        // Falls back to scanning existing scene enemies if no spawner is present.
        StartCoroutine(SpawnAndRegisterEnemies(mapName));
    }

    /// <summary>
    /// Primary enemy registration coroutine.
    /// Looks for a DungeonSpawner in the dungeon scene and drives the full
    /// two-phase spawn pipeline (API fetch → allocate → instantiate).
    /// If no DungeonSpawner is found, falls back to registering pre-placed enemies.
    /// </summary>
    private IEnumerator SpawnAndRegisterEnemies(string mapName)
    {
        // Wait one frame so Awake/Start have all completed in the loaded scene
        yield return null;

        var photon = PhotonManager.Instance;
        bool online = photon != null && photon.IsDungeonSession;

        // Spawn authority is Fusion's Shared-Mode master client (PhotonManager.IsHost),
        // and that is decided by ROOM JOIN ORDER — not by who hosted the party. The two
        // can diverge: MigrateToRoomAsync notes the party host's peer drains slower and
        // "tends to lose the race", in which case a member becomes master instead.
        //
        // Fusion also takes a few ticks to assert IsSharedModeMasterClient after
        // migration, so whoever ends up master must let that flag settle before the
        // pipeline runs — otherwise SpawnEnemyObject's `if (!photon.IsHost) return null`
        // skips every Runner.Spawn and nobody gets monsters. Gating this wait on
        // IsPartyHost was the bug: a member-master raced ahead and spawned nothing,
        // while the party host waited, never became master, and also spawned nothing.
        //
        // 2s was also too short: MigrateToRoomAsync retries with backoff (600–2400ms per
        // attempt), so the room's master can be elected several seconds after this client
        // finished loading the dungeon scene. When that happened the one-shot check below
        // said "not master" on EVERY client, so nobody ever called Runner.Spawn and the
        // whole party sat on "Loading...". The window is wider now AND the decision is
        // recoverable — see the re-run below.
        if (online)
        {
            // Also break as soon as replicated enemies exist: that means the master already
            // ran Runner.Spawn, so there is nothing left to wait for. Without it a proxy
            // burned the WHOLE 6s every time — the wait only ends early for whoever becomes
            // master. On a RESTART the master was elected on the first entry and never
            // changes, so those 6s were pure dead time and the progress panel sat on
            // "Loading..." for 6s after pressing Again.
            // Polled at 10Hz, not per frame: AnyEnemyInScene is a full-scene type scan and
            // this loop runs during dungeon load-in, exactly when the frame budget is
            // already tight. A 0.1s reaction delay on "has the master spawned yet" is
            // invisible next to the 6s ceiling it is guarding.
            float waitMaster = 6f;
            while (waitMaster > 0f && !photon.IsHost && !AnyEnemyInScene())
            {
                waitMaster -= 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            Debug.Log($"[DungeonManager] Spawn authority settled: IsHost={photon.IsHost} (IsPartyHost={IsPartyHost}).");
        }

        var spawner = FindFirstObjectByType<DungeonSpawner>();

        if (spawner != null)
        {
            // ── DungeonSpawner path: data-driven, backend-driven spawning ───────
            Debug.Log("[DungeonManager] DungeonSpawner found — running data-driven spawn pipeline.");

            bool spawnDone = false;
            List<EnemyEntity> spawnedEnemies = null;

            spawner.SpawnMonstersForDungeon(
                CurrentDungeonConfigId,
                mapName,
                enemies =>
                {
                    spawnedEnemies = enemies;
                    spawnDone = true;
                }
            );

            yield return new WaitUntil(() => spawnDone);

            if (spawnedEnemies != null)
            {
                photon = PhotonManager.Instance;
                bool isProxy = photon != null && photon.IsDungeonSession && !photon.IsHost;
                
                if (!isProxy)
                {
                    // No Clear() here: NetworkEnemy.Spawned already registered each of
                    // these from inside Runner.Spawn, and wiping the lists without also
                    // wiping _seenEnemies would make the re-registration below a no-op
                    // and leave the authority itself on "Loading...". Registration is
                    // idempotent, so simply re-running it reconciles anything the
                    // callback missed.
                    foreach (var enemy in spawnedEnemies)
                    {
                        RegisterNetworkedEnemy(enemy);
                    }
                    Debug.Log($"[DungeonManager] Registered {_normalEnemies.Count} normal enemies.");
                }
                else
                {
                    // A proxy gets its enemies from Fusion, not from the (empty) spawn
                    // list — NetworkEnemy.Spawned calls RegisterNetworkedEnemy itself.
                    // But that callback races the dungeon transition: replicas that
                    // arrive while the proxy is still migrating/loading get wiped by
                    // EnterDungeonScene's _normalEnemies.Clear(), and replicas that
                    // arrive later were never waited for. Either way the proxy ended up
                    // with 0 tracked enemies, which is exactly what pins the progress
                    // panel on "Loading..." (TotalNormalEnemies == 0).
                    //
                    // So sweep the scene instead of trusting the callback timing, and
                    // keep reconciling afterwards for replicas that are still in flight.
                    yield return SweepReplicatedEnemies();
                }

                // Recovery for the actual multiplayer failure. "Am I the master?" is read
                // ONCE, right after this client loaded the dungeon scene — but Fusion elects
                // the Shared-Mode master asynchronously, and MigrateToRoomAsync retries with
                // backoff, so that read can be too early on EVERY client. When it was, both
                // clients took the proxy path, nobody called Runner.Spawn, there were no
                // replicas to sweep, and the whole party sat on "Loading..." forever.
                //
                // Covers both branches on purpose: a client that became master *during* the
                // pipeline hits the !isProxy branch and registers an empty list, which is
                // just as broken as the proxy case.
                photon = PhotonManager.Instance;
                if (_normalEnemies.Count == 0 && _bossEnemies.Count == 0 &&
                    photon != null && photon.IsDungeonSession && photon.IsHost && !_masterSpawnRetried)
                {
                    _masterSpawnRetried = true;
                    Debug.LogWarning("[DungeonManager] 0 enemies but we ARE the master client — " +
                                     "the authority read was too early. Re-running the spawn pipeline.");
                    yield return SpawnAndRegisterEnemies(mapName);
                }
            }
        }
        else
        {
            // ── Fallback path: scan scene for manually-placed EnemyEntity objects ─
            Debug.LogWarning("[DungeonManager] No DungeonSpawner found in scene. " +
                             "Falling back to scanning for pre-placed EnemyEntity objects. " +
                             "Add a DungeonSpawner component to the dungeon scene for data-driven spawning.");
            yield return new WaitForSeconds(0.5f);

            var enemies = FindObjectsByType<EnemyEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Debug.Log($"[DungeonManager] Fallback: found {enemies.Length} pre-placed enemies.");

            // This path rebuilds from a full scene scan, so it resets _seenEnemies too —
            // otherwise the set would still hold entries whose list rows were just wiped,
            // and any later reconcile would refuse to re-add them.
            _normalEnemies.Clear();
            EnemyProgress.Clear();
            _seenEnemies.Clear();
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                _seenEnemies.Add(enemy);
                _normalEnemies.Add(enemy);
                enemy.OnDeath -= HandleNormalEnemyDeath;
                enemy.OnDeath += HandleNormalEnemyDeath;

                string n = GetCleanEnemyName(enemy);
                if (!EnemyProgress.ContainsKey(n)) EnemyProgress[n] = (0, 0);
                var p = EnemyProgress[n];
                p.total++;
                EnemyProgress[n] = p;
            }
        }
    }

    private static bool AnyEnemyInScene() =>
        FindObjectsByType<EnemyEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length > 0;

    /// <summary>
    /// Proxy-side enemy discovery. Polls the scene for replicated EnemyEntity objects
    /// until some show up (or we give up), then registers them and hands over to the
    /// periodic reconcile. Needed because the authority's Runner.Spawn calls and this
    /// client's dungeon transition run concurrently, so NetworkEnemy.Spawned can fire
    /// either before this client has cleared its lists or well after it finished loading.
    /// </summary>
    private IEnumerator SweepReplicatedEnemies()
    {
        // First pass: wait (bounded) for the first replica so the caller does not return
        // with an empty roster. The authority's own pipeline includes a backend round trip
        // (MonsterApi.GetSpawnsForMap) before it spawns anything, so "nothing yet" here is
        // normal rather than a failure.
        // 10Hz for the same reason as the master-authority wait above: a per-frame
        // full-scene scan during load-in is the one place we cannot afford it.
        float wait = 8f;
        while (wait > 0f && !AnyEnemyInScene())
        {
            wait -= 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        int first = ReconcileReplicatedEnemies();
        if (first > 0)
        {
            Debug.Log($"[DungeonManager] Proxy swept {first} replicated enemies " +
                      $"→ {_normalEnemies.Count} normal, {_bossEnemies.Count} boss.");
        }
        else
        {
            Debug.LogWarning("[DungeonManager] Proxy found NO replicated enemies after 8s — " +
                             "the master client may not have run Runner.Spawn yet (check its " +
                             "console). Reconcile keeps polling, so a late spawn still lands.");
        }

        // Keep reconciling for the rest of the Normal phase. The single 8s window was the
        // remaining hole behind the progress panel sitting on "Loading...": Fusion delivers
        // replicas over several ticks and the authority may still be waiting on its spawn
        // API when the window closes, so whatever had not arrived yet was never counted.
        // Registration is idempotent (_seenEnemies), so this only ever adds what is new.
        StartCoroutine(ReconcileReplicatedEnemiesLoop());
    }

    /// <summary>
    /// Registers every enemy currently in the scene that we have not seen yet, without
    /// clearing anything. Returns how many objects were scanned.
    /// </summary>
    private int ReconcileReplicatedEnemies()
    {
        var found = FindObjectsByType<EnemyEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var enemy in found) RegisterNetworkedEnemy(enemy);
        return found.Length;
    }

    /// <summary>
    /// Periodic top-up for a proxy: picks up replicas that arrive after the initial sweep.
    /// Ends when the boss phase starts (from then on the only new spawn is the boss, which
    /// <see cref="NetworkEnemy.Spawned"/> files correctly on its own) or when the run ends.
    /// </summary>
    private IEnumerator ReconcileReplicatedEnemiesLoop()
    {
        if (_reconcileLoopRunning) yield break;
        _reconcileLoopRunning = true;

        string sceneAtStart = _currentDungeonSceneName;

        while (IsInDungeon &&
               _currentPhase == DungeonPhase.Normal &&
               _currentDungeonSceneName == sceneAtStart)
        {
            yield return new WaitForSeconds(1f);

            // A restart re-runs the whole pipeline; let that one own the reconcile.
            if (!IsInDungeon || _currentPhase != DungeonPhase.Normal ||
                _currentDungeonSceneName != sceneAtStart)
                break;

            int before = _normalEnemies.Count;
            ReconcileReplicatedEnemies();
            if (_normalEnemies.Count != before)
            {
                Debug.Log($"[DungeonManager] Reconcile picked up {_normalEnemies.Count - before} " +
                          $"late replicated enemies (total now {TotalNormalEnemies}).");
            }
        }

        _reconcileLoopRunning = false;
    }

    public void RegisterNetworkedEnemy(EnemyEntity enemy)
    {
        if (enemy == null || _isRestarting) return;

        // One registration per enemy per run, tracked in _seenEnemies rather than in
        // _normalEnemies: the latter has the dead removed from it, so a corpse that is
        // still in the scene would be re-registered by the reconcile sweep and inflate
        // the total. A replica that arrives already dead is skipped for the same reason —
        // it was killed before this client had it, so it belongs to neither list.
        if (!_seenEnemies.Add(enemy)) return;
        if (enemy.IsDead) return;

        // Name test alone is not enough: NetworkEnemy.Spawned() (which calls us) fires
        // from inside Runner.Spawn, i.e. BEFORE DungeonSpawner.SpawnBoss renames the
        // instance to "{MonsterName}(Boss)". Proxies never rename at all — SpawnEnemyObject
        // returns null for non-hosts. So the boss was filed as a normal enemy, _bossEnemies
        // stayed empty and the UI read "Cleared!" with the boss still alive.
        // _currentPhase is the one boss signal set on EVERY client (TriggerBossSequence on
        // the host, ClientBossSequence on proxies), and only the boss spawns after Normal.
        string enemyName = enemy.gameObject.name;
        bool isBoss = _currentPhase != DungeonPhase.Normal
                      || enemyName.EndsWith("(Boss)") || enemyName.EndsWith("_Boss");

        if (isBoss)
        {
            _bossEnemies.Add(enemy);
            enemy.OnDeath -= HandleBossEnemyDeath;
            enemy.OnDeath += HandleBossEnemyDeath;
            Debug.Log($"[DungeonManager] Registered networked BOSS: {enemy.gameObject.name} (phase={_currentPhase})");
        }
        else
        {
            _normalEnemies.Add(enemy);
            enemy.OnDeath -= HandleNormalEnemyDeath;
            enemy.OnDeath += HandleNormalEnemyDeath;

            string n = GetCleanEnemyName(enemy);
            if (!EnemyProgress.ContainsKey(n)) EnemyProgress[n] = (0, 0);
            var p = EnemyProgress[n];
            p.total++;
            EnemyProgress[n] = p;
            Debug.Log($"[DungeonManager] Registered networked enemy: {n} (Total: {p.total})");
        }
    }

    private string GetCleanEnemyName(EnemyEntity enemy)
    {
        if (enemy == null || enemy.gameObject == null) return "Unknown";
        string cleanName = enemy.gameObject.name.Replace("(Clone)", "").Trim();

        // Strip Fusion network ID if present: " [123]"
        int bracketIndex = cleanName.IndexOf(" [");
        if (bracketIndex > 0) cleanName = cleanName.Substring(0, bracketIndex).Trim();
        
        int spaceIndex = cleanName.IndexOf(" (");
        if (spaceIndex > 0) cleanName = cleanName.Substring(0, spaceIndex).Trim();
        
        int lastUnderscore = cleanName.LastIndexOf('_');
        if (lastUnderscore > 0 && lastUnderscore < cleanName.Length - 1)
        {
            string suffix = cleanName.Substring(lastUnderscore + 1);
            if (int.TryParse(suffix, out _))
            {
                cleanName = cleanName.Substring(0, lastUnderscore);
            }
        }

        // Remove any remaining underscores so "slime_ice" matches "SlimeIce" case-insensitively
        cleanName = cleanName.Replace("_", "");
        
        return cleanName;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ENEMY DEATH HANDLERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles death of a normal (non-boss) enemy.
    /// Reports partial progress to the backend.
    /// When the LAST normal enemy dies → initiates the boss spawn sequence.
    /// </summary>
    private void HandleNormalEnemyDeath(object sender, EventArgs e)
    {
        if (sender is not EnemyEntity enemy) return;

        enemy.OnDeath -= HandleNormalEnemyDeath;
        _normalEnemies.Remove(enemy);
        EnemiesKilledCount++;

        string n = GetCleanEnemyName(enemy);
        if (EnemyProgress.ContainsKey(n))
        {
            var p = EnemyProgress[n];
            p.killed++;
            EnemyProgress[n] = p;
        }
        else
        {
            // Fallback for names that might have mutated or mismatch
            foreach (var key in EnemyProgress.Keys)
            {
                if (n.Contains(key) || key.Contains(n))
                {
                    var p = EnemyProgress[key];
                    p.killed++;
                    EnemyProgress[key] = p;
                    break;
                }
            }
        }

        int remaining  = _normalEnemies.Count;
        int total      = EnemiesKilledCount + remaining;
        // Progress stays ≤ 49 % while normals are alive; hits 50 % when all are dead
        int percentage = remaining == 0 ? 50
                       : Mathf.Min(49, (EnemiesKilledCount * 50) / Mathf.Max(1, total));

        Debug.Log($"[DungeonManager] Normal enemy killed. Remaining: {remaining}. Progress: {percentage}%");

        // Fire-and-forget progress update (session owner only: host, or solo)
        if (OwnsSession)
        {
            DungeonApi.Instance.UpdateProgress(
                CurrentSessionId,
                new UpdateDungeonProgressRequest
                {
                    MonstersKilled       = EnemiesKilledCount,
                    BossKilled           = false,
                    CompletionPercentage = percentage
                },
                _ => { },
                err => Debug.LogWarning($"[DungeonManager] UpdateProgress (normal) failed: {err.Message}")
            );
        }

        // Boss sequence is HOST-ONLY when online. DungeonSpawner.SpawnEnemyObject returns
        // null for non-hosts (they receive the boss via replication), so a proxy running
        // this fell into the "boss == null" fallback below and spawned the reward chest
        // while the host's boss was still alive. Proxies wait for RPC_BossSpawning /
        // RPC_BossDied instead. Mirrors the online+!IsHost test in SpawnEnemyObject —
        // IsHost is also false offline (no runner), so gating on IsHost alone would stop
        // single-player from ever spawning a boss.
        var photon = PhotonManager.Instance;
        bool isProxy = photon != null && photon.IsDungeonSession && !photon.IsHost;

        if (remaining == 0 && _currentPhase == DungeonPhase.Normal && !isProxy)
            StartCoroutine(TriggerBossSequence());
    }

    /// <summary>
    /// Screen shake warning → 1-second pause → spawns boss at BossSpawn point.
    /// </summary>
    private IEnumerator TriggerBossSequence()
    {
        _currentPhase = DungeonPhase.BossSpawning;
        Debug.Log("[DungeonManager] All normals defeated. Starting boss sequence (shake → spawn).");

        if (PhotonManager.Instance?.IsHost == true && NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.RPC_BossSpawning();
        }

        // Screen shake to signal the incoming boss
        DungeonScreenShake.Shake(duration: 0.9f, magnitude: 0.28f);
        yield return new WaitForSeconds(1.2f);

        // Find DungeonSpawner and let it spawn the Boss (which was saved from the API call)
        if (DungeonSpawner.Instance == null)
        {
            Debug.LogWarning("[DungeonManager] DungeonSpawner not found. Skipping boss and completing dungeon.");
            // Latch Complete before the chest drops: BossDeathSequence spawns the reward
            // chest, and leaving the phase at BossSpawning left the UI on "Boss Spawned!"
            // with a lootable chest already on the floor.
            _currentPhase = DungeonPhase.Complete;
            yield return StartCoroutine(BossDeathSequence(GetFallbackChestPosition()));
            yield break;
        }

        EnemyEntity boss = DungeonSpawner.Instance.SpawnBoss();
        if (boss != null)
        {
            RegisterNetworkedEnemy(boss);
        }
        
        if (boss == null)
        {
            Debug.LogWarning("[DungeonManager] DungeonSpawner.SpawnBoss returned null. Completing dungeon without boss.");
            _currentPhase = DungeonPhase.Complete;
            yield return StartCoroutine(BossDeathSequence(GetFallbackChestPosition()));
            yield break;
        }

        _currentPhase = DungeonPhase.Boss;
        Debug.Log($"[DungeonManager] Boss '{boss.name}' spawned. Phase → Boss.");
    }

    /// <summary>
    /// Handles boss death. Captures death position, then starts the completion sequence.
    /// </summary>
    private void HandleBossEnemyDeath(object sender, EventArgs e)
    {
        if (sender is not EnemyEntity boss) return;

        boss.OnDeath -= HandleBossEnemyDeath;
        _bossEnemies.Remove(boss);

        // Proxies register the boss too now, so a non-host client can reach completion
        // twice: once from its own replicated boss death, once from the host's
        // RPC_BossDied. Whichever arrives first latches the phase; the second must not
        // re-run the sequence (it would spawn a second chest and re-POST Complete).
        if (_currentPhase == DungeonPhase.Complete) return;

        bossKilled           = true;
        EnemiesKilledCount++;
        _bossDeathPosition   = boss.transform.position;
        _currentPhase        = DungeonPhase.Complete;

        if (PhotonManager.Instance?.IsHost == true && NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.RPC_BossDied(_bossDeathPosition);
        }

        Debug.Log($"[DungeonManager] Boss defeated at {_bossDeathPosition}. Starting completion sequence.");
        StartCoroutine(BossDeathSequence(_bossDeathPosition));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CLIENT EVENT RECEIVERS
    // ═══════════════════════════════════════════════════════════════════════════

    public void ClientReceiveBossSpawning()
    {
        StartCoroutine(ClientBossSequence());
    }

    private IEnumerator ClientBossSequence()
    {
        _currentPhase = DungeonPhase.BossSpawning;
        Debug.Log("[DungeonManager] Client received boss spawning event. Shaking screen...");
        DungeonScreenShake.Shake(duration: 0.9f, magnitude: 0.28f);
        yield return new WaitForSeconds(1.2f);
        _currentPhase = DungeonPhase.Boss;
    }

    public void ClientReceiveBossDeath(Vector3 chestPosition)
    {
        // Same double-entry guard as HandleBossEnemyDeath: this client may already have
        // completed off its own replicated boss death before the host's RPC lands.
        if (_currentPhase == DungeonPhase.Complete) return;

        _currentPhase = DungeonPhase.Complete;
        Debug.Log($"[DungeonManager] Client received boss death event at {chestPosition}. Starting completion sequence.");
        StartCoroutine(BossDeathSequence(chestPosition));
    }

    /// <summary>
    /// Reports 100% completion to the backend, waits 1.5 seconds for the boss death
    /// animation to finish, then spawns the reward chest with a drop-in animation.
    /// </summary>
    private IEnumerator BossDeathSequence(Vector3 chestPosition)
    {
        bool updateDone = false;
        bool completeDone = false;

        // Report final progress FIRST and wait for it (session owner only: host, or solo)
        if (OwnsSession)
        {
            DungeonApi.Instance.UpdateProgress(
                CurrentSessionId,
                new UpdateDungeonProgressRequest
                {
                    MonstersKilled       = EnemiesKilledCount,
                    BossKilled           = bossKilled,
                    CompletionPercentage = 100
                },
                _ => { updateDone = true; },
                err => 
                { 
                    Debug.LogWarning($"[DungeonManager] Final UpdateProgress failed: {err.Message}");
                    updateDone = true; 
                }
            );

            // Wait for the backend to acknowledge the boss kill
            yield return new WaitUntil(() => updateDone);

            // NOW mark session complete on backend
            DungeonApi.Instance.Complete(
                CurrentSessionId,
                response =>
                {
                    Debug.Log("[DungeonManager] Session marked complete on backend.");
                    completeDone = true;
                },
                error =>
                {
                    Debug.LogWarning($"[DungeonManager] Complete API failed: {error.Message}. Spawning chest anyway.");
                    completeDone = true;
                }
            );

            yield return new WaitUntil(() => completeDone);
        }
        else
        {
            // Non-host: mark done immediately
            updateDone = true;
            completeDone = true;
        }

        // Credit any "Explore ... Dungeon" objective (Q14 "Train in the Old Dungeon").
        // NOTHING else in Assets/Scripts/Features/Dungeon touched QuestUIManager, so clearing a
        // dungeon never credited quest progress and Q14 sat InProgress forever — which
        // dead-ended the whole Chapter 2 chain, since it gates on Claimed.
        //
        // Deliberately OUTSIDE the OwnsSession branch above: quest progress is per-player, and
        // every client reaches BossDeathSequence exactly once (both entry points latch
        // _currentPhase = Complete first), so each party member credits their own copy once.
        // Boss death rather than entry because the prose says "Clear his training dungeon".
        // AddProgress clamps at targetAmount, so a re-run of a claimed quest is a no-op, and
        // Explore is in the auto-complete list — the batch loop Completes + Claims from here.
        CreditDungeonExploreQuests();

        // Wait for boss death animation
        yield return new WaitForSeconds(1.5f);

        // Spawn the reward chest with drop-in animation
        SpawnFinalChestAtPosition(chestPosition);
    }

    /// <summary>
    /// Surfaces a "you cannot enter / something failed" message to the player.
    /// These all used to go through WorldRuntimeEvents.RaiseMessage, which has NO subscriber
    /// anywhere in the project — so every one of them was a silent no-op and a rejected
    /// dungeon entry looked to the player like a dead keypress. MainQuestPanelRuntime is the
    /// same channel MapTeleportPortal uses for its blocked-entry message. Kind.None is
    /// explicit: InferKind guesses from keywords and would stamp a green "Completed!" on text
    /// containing words like "complete".
    /// </summary>
    private static void NotifyBlocked(string message)
    {
        Debug.LogWarning($"[DungeonManager] {message}");
        if (MainQuestPanelRuntime.Instance != null)
            MainQuestPanelRuntime.Instance.ShowPaperPopup(message, UIPaperPopupView.PaperPopupKind.None);
    }

    /// <summary>
    /// Advances every in-progress "Explore" quest whose ObjectiveTarget names a dungeon.
    /// Mirrors the portal's matching loop in MapTeleportPortal (Contains("Portal")); the two
    /// targets are disjoint, and Q8/Q14 are the only Explore quests seeded, so neither hook
    /// can credit the other's quest.
    /// </summary>
    private void CreditDungeonExploreQuests()
    {
        var quests = QuestUIManager.Instance?.GetMainQuests();
        if (quests == null) return;

        bool credited = false;

        foreach (var q in quests)
        {
            if (!string.Equals(q.Status, "InProgress", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(q.ObjectiveType, "Explore", StringComparison.OrdinalIgnoreCase)) continue;
            if (q.ObjectiveTarget == null) continue;
            if (!q.ObjectiveTarget.Contains("Dungeon", StringComparison.OrdinalIgnoreCase)) continue;

            Debug.Log($"[DungeonManager] Crediting Explore quest {q.QuestId} for dungeon clear.");
            QuestUIManager.Instance.AddProgress(q.QuestId, 1);
            credited = true;
            // No popup here, same reason as MapTeleportPortal: the batch sync loop
            // Completes + Claims and fires the single "Reward Claimed!" popup.
        }

        // AddProgress only queues into _pendingBatch for BatchSyncLoop's 1s tick. Leaving the
        // dungeon reloads quests, and HandleLoadedQuestResponses calls _pendingBatch.Clear() —
        // so without this flush a fast exit after the boss dies drops the credit and Q14 stays
        // InProgress. Same reason MapTeleportPortal flushes before unloading a scene.
        if (credited)
            QuestUIManager.Instance.FlushPendingProgressNow();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CHEST SPAWNING
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("Dungeon Rewards")]
    [Tooltip("Kéo Prefab Rương của bạn vào đây (vd: DarkChest)")]
    public GameObject rewardChestPrefab;

    private GameObject _rewardChest;

    /// <summary>
    /// Destroy the reward chest of the previous run. Unloading the dungeon scene was NOT
    /// enough to take it with it: the chest is Instantiate()d with no parent so it lands in
    /// the ACTIVE scene, and the active scene is always Main (both TransitionToRestart and
    /// ReturnToWorldMap end with SetActiveScene("Main")). Only the assigned-prefab branch
    /// tries to move it into the dungeon scene, and that move is best-effort — SafeMoveToScene
    /// bails when WorldState.CurrentMapName is not a loaded scene. Main is never unloaded, so
    /// whatever stayed there survived into run 2 as a second, openable chest.
    /// </summary>
    private void DespawnRewardChest()
    {
        if (_rewardChest != null) Destroy(_rewardChest);
        _rewardChest = null;
    }

    private void SpawnFinalChestAtPosition(Vector3 targetPosition)
    {
        // One chest per run, always.
        DespawnRewardChest();

        GameObject chestGO = null;

        // 1. Instantiate from assigned prefab
        if (rewardChestPrefab != null)
        {
            chestGO = Instantiate(rewardChestPrefab, targetPosition + Vector3.up * 6f, Quaternion.identity);
            chestGO.name = "DungeonChest";
            // GetSceneByName returns an INVALID Scene when the name is not a loaded scene
            // (WorldState.CurrentMapName is written from a dozen places and can hold the
            // overworld name here) and MoveGameObjectToScene then throws
            // "ArgumentException: Destination scene is not valid". SafeMoveToScene guards it.
            SafeMoveToScene(chestGO, SceneManager.GetSceneByName(WorldState.CurrentMapName));
            
            // Ensure components are present and active
            var chestScript = chestGO.GetComponent<DungeonChest>();
            if (chestScript == null) chestScript = chestGO.AddComponent<DungeonChest>();
            chestScript.enabled = true; // Force enable in case it was disabled in prefab

            Debug.Log("[DungeonManager] Spawned chest from assigned prefab with drop animation.");
        }
        else
        {
            // 2. Instantiate from Resources (legacy paths)
            var prefab = Resources.Load<GameObject>("Prefabs/Chest")
                      ?? Resources.Load<GameObject>("Chest")
                      ?? Resources.Load<GameObject>("DarkChest")
                      ?? Resources.Load<GameObject>("PixelWorld/Prefabs/Objects/BoxesChests/Chest_1");

            if (prefab != null)
            {
                chestGO = Instantiate(prefab, targetPosition + Vector3.up * 6f, Quaternion.identity);
                chestGO.name = "DungeonChest";
                var chestScript = chestGO.GetComponent<DungeonChest>();
                if (chestScript == null) chestScript = chestGO.AddComponent<DungeonChest>();
                chestScript.enabled = true;
                Debug.Log("[DungeonManager] Spawned chest from Resources prefab.");
            }
        }

        // 3. Hard fallback: 2D Sprite (visible in 2D view)
        if (chestGO == null)
        {
            chestGO = new GameObject("DungeonChest");
            chestGO.transform.position = targetPosition + Vector3.up * 6f;
            
            var sr = chestGO.AddComponent<SpriteRenderer>();
            Sprite defaultSprite = Resources.Load<Sprite>("UI/Skin/UISprite.psd") ?? Resources.Load<Sprite>("Background");
            sr.sprite = defaultSprite;
            sr.color = Color.yellow; 
            
            var col = chestGO.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 1.5f);

            chestGO.AddComponent<DungeonChest>();
            Debug.LogWarning("[DungeonManager] Chest prefab not found — created a fallback yellow 2D Sprite chest.");
        }

        _rewardChest = chestGO;
        StartCoroutine(ChestDropAnimation(chestGO, targetPosition));
    }

    /// <summary>
    /// Animates the chest falling from 6 units above the target to the target position.
    /// Uses ease-out cubic for a natural bouncy landing feel.
    /// </summary>
    private IEnumerator ChestDropAnimation(GameObject chest, Vector3 targetPosition)
    {
        if (chest == null) yield break;

        Vector3 startPos = chest.transform.position; // already offset upward by caller
        float elapsed    = 0f;
        const float duration = 0.55f;

        while (elapsed < duration)
        {
            if (chest == null) yield break;
            float t     = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
            chest.transform.position = Vector3.Lerp(startPos, targetPosition, eased);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (chest != null)
            chest.transform.position = targetPosition;

        Debug.Log($"[DungeonManager] Reward chest landed at {targetPosition}.");
    }

    private Vector3 GetFallbackChestPosition()
    {
        var player = FindPlayerInstance();
        return player != null ? player.transform.position + Vector3.right * 2f : Vector3.zero;
    }

    /// <summary>Kept for external callers — logic now handled by the new two-phase death system.</summary>
    [System.Obsolete("Death events are now handled automatically by HandleNormalEnemyDeath and HandleBossEnemyDeath.")]
    public void UpdateMonsterKill(bool isBoss)
    {
        Debug.LogWarning("[DungeonManager] UpdateMonsterKill is deprecated and does nothing. " +
                         "Death events are handled automatically.");
    }

    public void ReturnToWorldMap()
    {
        if (_isReturningToWorld) return;
        _isReturningToWorld = true;
        StartCoroutine(TransitionToWorld());
    }

    private IEnumerator TransitionToWorld()
    {
        // Che cả đoạn migrate Photon lẫn scene swap — migrate là await mạng, thời gian không
        // đoán được, để lộ scene dungeon đã unload thì rất xấu.
        yield return LoadingScreen.Show("Returning to world...");

        // If we entered the dungeon as a networked party, leave the dungeon Photon room
        // and return to the shared social lobby FIRST. This tears down the dungeon runner
        // so our avatar despawns for the other members (and theirs for us) — otherwise
        // both players keep seeing each other in the world because they're still in the
        // same dungeon room. Done before the scene swap so the networked avatar is gone
        // before PlayerSpawner puts a local one back.
        var photon = PhotonManager.Instance;
        if (photon != null && photon.IsDungeonSession)
        {
            Debug.Log("[DungeonManager] Exiting dungeon room → migrating back to social lobby.");
            var migrate = photon.MigrateToSocialLobbyAsync();
            while (!migrate.IsCompleted) yield return null;
        }

        // Find player first before unloading
        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainSceneObj = SceneManager.GetSceneByName("Main");
            if (SafeMoveToScene(player, mainSceneObj))
                Debug.Log("[DungeonManager] Moved player to Main scene defensively.");
        }

        // Unload any active map scenes defensively (excluding "Main" and the target world scene)
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "Main" && s.name != PreviousMapSceneName && s.name != LoadingScreen.SceneName && s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // Restore position and current map
        Vector3 returnPos = HasPreviousPlayerPosition ? PreviousPlayerPosition : WorldState.LastPosition;
        if (!HasPreviousPlayerPosition && returnPos == Vector3.zero) returnPos = new Vector3(11.9f, 17.8f, 0f); // Final hard fallback
        WorldState.LastPosition = returnPos;
        WorldState.CurrentMapName = PreviousMapSceneName;

        // Load previous map
        yield return SceneManager.LoadSceneAsync(PreviousMapSceneName, LoadSceneMode.Additive);

        // Move player into the world scene and set physical position
        if (player != null)
        {
            var worldScene = SceneManager.GetSceneByName(PreviousMapSceneName);
            if (SafeMoveToScene(player, worldScene))
            {
                player.transform.position = returnPos;
                Debug.Log($"[DungeonManager] Moved player into world scene: {PreviousMapSceneName} at {returnPos}");

                // Save position to backend so logout doesn't get stuck in dungeon
                MysticJourney.API.Endpoints.WorldApi.Instance?.UpdatePosition(PreviousMapSceneName, returnPos, null, null);
            }
        }
        else
        {
            player = FindPlayerInstance();
        }

        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = WorldState.LastPosition;
            }
            player.transform.position = WorldState.LastPosition;
            BindCameraToPlayer(player, PreviousMapSceneName);
        }

        // Keep Main active
        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.IsValid())
        {
            SceneManager.SetActiveScene(mainScene);
        }

        PlayerHUDUIManager.Instance?.ToggleDungeonMode(false);
        // Same leak as the restart path: the chest lives in Main, so Exit carried it out to
        // the world map instead of unloading it with the dungeon.
        DespawnRewardChest();
        IsInDungeon = false;
        Debug.Log($"[DungeonManager] Returned to map: {PreviousMapSceneName} at {WorldState.LastPosition}");

        yield return LoadingScreen.Hide();
        _isReturningToWorld = false;
    }

    /// <summary>
    /// Party member: take over the session id the host created for the restarted run.
    /// Called from <see cref="NetworkPlayer.RPC_SetRestartSession"/>.
    /// </summary>
    public void AdoptRestartSession(int sessionId)
    {
        if (sessionId <= 0) return;
        if (CurrentSessionId == sessionId) return;
        CurrentSessionId = sessionId;
        IsInDungeon = true;
        Debug.Log($"[DungeonManager] Adopted host restart session: {sessionId}");
    }

    public void RestartDungeon()
    {
        Debug.Log("[DungeonManager] Restarting Dungeon...");
        _isRestarting = true;
        EnemiesKilledCount = 0;
        bossKilled = false;
        _currentPhase = DungeonPhase.Normal;
        _normalEnemies.Clear();
        _bossEnemies.Clear();
        _seenEnemies.Clear();
        EnemyProgress.Clear();
        _bossDeathPosition = Vector3.zero;
        _masterSpawnRetried = false;
        _spawnStarted = false;

        // Remove run 1's chest NOW, not when run 2's boss dies — otherwise it stays
        // standing and openable for the whole second run.
        DespawnRewardChest();

        // The progress panel lives in the Main HUD, which is never unloaded, so its
        // OnEnable does not run again on a restart.
        var progress = FindFirstObjectByType<UIDungeonProgressPanel>(FindObjectsInactive.Include);
        if (progress != null) progress.ResetProgress();

        // Note: PreviousMapSceneName and PreviousPlayerPosition are preserved from the FIRST time they entered!

        // Whoever owns the backend session calls Enter: the party host, and also a solo
        // player. Members reuse the host's id via RPC_SetRestartSession below.
        if (OwnsSession)
        {
            DungeonApi.Instance.Enter(CurrentDungeonConfigId, _currentPartyMembers,
                onSuccess: response =>
                {
                    if (response != null)
                    {
                        CurrentSessionId = response.DungeonSessionId;
                        IsInDungeon = true;
                        Debug.Log($"[DungeonManager] Session created for Restart: {CurrentSessionId}");

                        // Members never call Enter, so without this they keep the finished
                        // run's id and their claim-reward on run 2 fails → the complete
                        // panel falls back to +0 / +0 / --:--.
                        if (NetworkPlayer.Local != null)
                            NetworkPlayer.Local.RPC_SetRestartSession(CurrentSessionId);

                        // Close the Dungeon Complete panel since it lives in Main scene
                        var p = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
                        if (p != null) p.gameObject.SetActive(false);

                        string sceneToLoad = _currentDungeonSceneName;
                        StartCoroutine(TransitionToRestart(sceneToLoad));
                    }
                },
                onError: error =>
                {
                    Debug.LogWarning($"[DungeonManager] Restart API failed: {error.Message}. Proceeding to restart anyway for testing.");
                    NotifyBlocked($"Cannot Restart API: {error.Message}");
                    
                    CurrentSessionId = -1;
                    IsInDungeon = true;
                    // Close the Dungeon Complete panel since it lives in Main scene
                    var p = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
                    if (p != null) p.gameObject.SetActive(false);

                    string sceneToLoad = _currentDungeonSceneName;
                    StartCoroutine(TransitionToRestart(sceneToLoad));
                }
            );
        }
        else
        {
            // Non-host: just restart scene locally, don't create new session
            Debug.Log("[DungeonManager] Non-host restarting dungeon scene locally.");
            IsInDungeon = true;
            var p = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
            if (p != null) p.gameObject.SetActive(false);
            StartCoroutine(TransitionToRestart(_currentDungeonSceneName));
        }
    }

    private IEnumerator TransitionToRestart(string dungeonSceneName)
    {
        yield return LoadingScreen.Show("Restarting dungeon...");

        // 1. Find player and move them to Main defensively so they survive the reload
        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainScene = SceneManager.GetSceneByName("Main");
            SafeMoveToScene(player, mainScene);
        }

        // 2. Unload the CURRENT dungeon scene
        var currentDungeonScene = SceneManager.GetSceneByName(dungeonSceneName);
        if (currentDungeonScene.IsValid() && currentDungeonScene.isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(currentDungeonScene);
        }

        // 3. Load the dungeon scene fresh
        yield return SceneManager.LoadSceneAsync(dungeonSceneName, LoadSceneMode.Additive);

        // 4. Move player back in
        if (player != null)
        {
            var newDungeonScene = SceneManager.GetSceneByName(dungeonSceneName);
            if (SafeMoveToScene(player, newDungeonScene))
            {
                GameObject targetSpawnPoint = GameObject.Find("PlayerSpawn") ?? GameObject.Find("SceneTransitionGoblinMine");
                if (targetSpawnPoint == null)
                {
                    var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
                    foreach (var t in allTransforms)
                    {
                        if (t != null && (t.gameObject.name == "PlayerSpawn" || t.gameObject.name == "SceneTransitionGoblinMine") && t.gameObject.scene.name == dungeonSceneName)
                        {
                            targetSpawnPoint = t.gameObject;
                            break;
                        }
                    }
                }

                if (targetSpawnPoint != null)
                {
                    Vector3 spawnPos = targetSpawnPoint.transform.position;

                    // Same per-player fan-out as the entry path: every client restarts to the
                    // same PlayerSpawn, and fully-overlapped dynamic colliders wedge each other.
                    var npRestart = player.GetComponent<NetworkPlayer>();
                    if (npRestart != null && npRestart.Object != null && npRestart.Object.IsValid)
                        spawnPos += NetworkPlayer.FanOutOffset(npRestart.Object.InputAuthority.PlayerId);

                    var nt = player.GetComponent<NetworkTransform>();
                    if (nt != null)
                    {
                        nt.Teleport(spawnPos);
                    }
                    else
                    {
                        player.transform.position = spawnPos;
                    }

                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector2.zero;
                        rb.position = spawnPos;
                    }
                    WorldState.LastPosition = spawnPos;
                }
                else
                {
                    var nt = player.GetComponent<NetworkTransform>();
                    if (nt != null)
                    {
                        nt.Teleport(Vector3.zero);
                    }
                    else
                    {
                        player.transform.position = Vector3.zero;
                    }
                    WorldState.LastPosition = Vector3.zero;
                }
                
                BindCameraToPlayer(player, dungeonSceneName);
            }
        }
        else
        {
            Debug.LogError("[DungeonManager] Restart failed to locate Player! Camera not bound.");
        }

        // Reset all NetworkPlayers for restart (restore HP, IsAlive, IsReadyToRestart)
        Vector3 finalSpawnPos = Vector3.zero;
        GameObject sp = GameObject.Find("PlayerSpawn") ?? GameObject.Find("SceneTransitionGoblinMine");
        if (sp == null)
        {
            var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in allTransforms)
            {
                if (t != null && (t.gameObject.name == "PlayerSpawn" || t.gameObject.name == "SceneTransitionGoblinMine") && t.gameObject.scene.name == dungeonSceneName)
                {
                    sp = t.gameObject;
                    break;
                }
            }
        }
        if (sp != null) finalSpawnPos = sp.transform.position;

        if (NetworkPlayer.All != null && NetworkPlayer.All.Count > 0)
        {
            foreach (var p in NetworkPlayer.All)
            {
                if (p != null)
                {
                    // Per-player offset, not the bare shared spawn: ResetForRestart only
                    // applies to the avatar this client owns, but every client would pass the
                    // identical point and the avatars end up interpenetrating and immobile.
                    Vector3 target = finalSpawnPos;
                    if (p.Object != null && p.Object.IsValid)
                        target += NetworkPlayer.FanOutOffset(p.Object.InputAuthority.PlayerId);
                    p.ResetForRestart(target);
                }
            }
        }
        else
        {
            if (player != null)
            {
                var entity = player.GetComponent<PlayerEntity>();
                if (entity != null)
                {
                    entity.DungeonRespawn(finalSpawnPos);
                }
            }
        }

        // Keep Main active
        var mainActiveScene = SceneManager.GetSceneByName("Main");
        if (mainActiveScene.IsValid())
        {
            SceneManager.SetActiveScene(mainActiveScene);
        }

        Debug.Log($"[DungeonManager] Successfully restarted dungeon: {dungeonSceneName}");

        yield return LoadingScreen.Hide();
    }

    private string GetSceneName(GameObject go)
    {
        if (go == null) return string.Empty;
        var scene = go.scene;
        return scene.IsValid() ? scene.name : string.Empty;
    }

    private void BindCameraToPlayer(GameObject player, string sceneName)
    {
        // 1. Deactivate duplicate Main Camera in "Main" scene to avoid rendering/clearing conflicts
        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.IsValid() && mainScene.isLoaded)
        {
            var rootObjects = mainScene.GetRootGameObjects();
            foreach (var obj in rootObjects)
            {
                if (obj.name == "Main Camera" && obj.CompareTag("MainCamera"))
                {
                    obj.SetActive(false);
                    Debug.Log("[DungeonManager] Deactivated Main Camera in Main scene dynamically.");
                }
            }
        }

        // 2. Find the target main camera in the loaded map scene specifically
        Camera targetCam = null;
        var allCameras = Resources.FindObjectsOfTypeAll<Camera>();
        foreach (var cam in allCameras)
        {
            if (cam != null && cam.gameObject != null && cam.gameObject.scene.name == sceneName && cam.CompareTag("MainCamera"))
            {
                cam.gameObject.SetActive(true);
                cam.enabled = true;
                targetCam = cam;
                break;
            }
        }

        if (targetCam == null)
        {
            targetCam = Camera.main;
        }

        // 3. Ensure the active target camera has CinemachineBrain and snap it to player position
        if (targetCam != null)
        {
            var brain = targetCam.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = targetCam.gameObject.AddComponent<CinemachineBrain>();
                Debug.Log($"[DungeonManager] Dynamically added CinemachineBrain to target camera: {targetCam.name} in scene {sceneName}");
            }

            Vector3 playerPos = player.transform.position;
            targetCam.transform.position = new Vector3(playerPos.x, playerPos.y, targetCam.transform.position.z);
            Debug.Log($"[DungeonManager] Warped target Camera {targetCam.name} to {playerPos}");
        }
        else
        {
            Debug.LogWarning("[DungeonManager] No target camera tagged MainCamera found!");
        }

        // 4. Configure all Cinemachine cameras in the target scene
        var camsList = Resources.FindObjectsOfTypeAll<CinemachineCamera>();
        int boundCount = 0;
        foreach (var cam in camsList)
        {
            if (cam != null && cam.gameObject != null)
            {
                string camSceneName = GetSceneName(cam.gameObject);
                if (camSceneName == sceneName)
                {
                    cam.gameObject.SetActive(true);
                    cam.enabled = true;
                    cam.Priority = 999; // Override other virtual cameras
                    cam.Follow = player.transform;

                    Vector3 playerPos = player.transform.position;
                    cam.transform.position = new Vector3(playerPos.x, playerPos.y, cam.transform.position.z);
                    boundCount++;
                }
                else if (camSceneName == "Main")
                {
                    cam.Priority = 10;
                }
            }
        }
        Debug.Log($"[DungeonManager] Configured {boundCount} CinemachineCamera(s) in target scene.");

        // 5. Update minimap camera
        var minimapCam = FindFirstObjectByType<MinimapCameraController>();
        if (minimapCam != null)
        {
            minimapCam.gameObject.SetActive(true);
            minimapCam.enabled = true;
            minimapCam.InitializeMinimap(player.transform);
            Debug.Log("[DungeonManager] Re-initialized MinimapCameraController successfully.");
        }
    }
}
