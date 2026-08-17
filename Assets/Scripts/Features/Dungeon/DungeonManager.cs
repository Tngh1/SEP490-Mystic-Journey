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

// Executes core business logic for mono behaviour.
public class DungeonManager : MonoBehaviour
{
    // Executes core business logic for instance.
    public static DungeonManager Instance { get; private set; }

    // Executes core business logic for current session id.
    [Header("Runtime State")]
    public int CurrentSessionId { get; private set; } = 0;
    // Executes core business logic for current dungeon config id.
    public int CurrentDungeonConfigId { get; private set; } = 0;
    // Executes core business logic for current dungeon cost.
    public int CurrentDungeonCost { get; private set; } = 0;
    // Executes core business logic for current dungeon name.
    public string CurrentDungeonName { get; private set; } = string.Empty;
    // Executes core business logic for enemies killed count.
    public int EnemiesKilledCount { get; private set; } = 0;
    // Executes core business logic for is in dungeon.
    public bool IsInDungeon { get; private set; } = false;

    // Executes core business logic for total normal enemies.
    public int TotalNormalEnemies => EnemiesKilledCount + _normalEnemies.Count;
    // Executes core business logic for boss count.
    public int BossCount => _bossEnemies.Count;

    // Executes core business logic for is dungeon cleared.
    public bool IsDungeonCleared => _currentPhase == DungeonPhase.Complete;
    // Executes core business logic for enemy progress.
    public Dictionary<string, (int killed, int total)> EnemyProgress { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    // Executes core business logic for previous map scene name.
    public string PreviousMapSceneName { get; private set; } = "AbandonedCastle";
    // Executes core business logic for previous player position.
    public Vector3 PreviousPlayerPosition { get; private set; } = Vector3.zero;
    // Executes core business logic for has previous player position.
    public bool HasPreviousPlayerPosition { get; private set; } = false;

    private readonly List<EnemyEntity> _normalEnemies = new();
    private readonly List<EnemyEntity> _bossEnemies   = new();

    private readonly HashSet<EnemyEntity> _seenEnemies = new();
    private bool bossKilled = false;
    private Vector3 _bossDeathPosition = Vector3.zero;

    private bool _masterSpawnRetried = false;

    private bool _reconcileLoopRunning = false;

    private bool _spawnStarted = false;
    private bool _isReturningToWorld = false;
    private bool _isRestarting = false;

    private List<string> _currentPartyMembers = new();
    private string _currentDungeonSceneName = string.Empty;

    // Executes core business logic for dungeon phase.
    private enum DungeonPhase { Normal, BossSpawning, Boss, Complete }
    private DungeonPhase _currentPhase = DungeonPhase.Normal;

    // Initializes singleton manager, registers scene loaded callback, and persists across scenes.
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this; // Cache singleton instance
            transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject); // Persist across world/dungeon scene loads
            SceneManager.sceneLoaded += OnSceneLoaded; // Listen for scene load events
        }
        else
        {
            Destroy(gameObject); // Prevent duplicate dungeon manager instances
        }
    }

    // Unsubscribes scene change callbacks and releases manager reference.
    private void OnDestroy()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded; // Unsubscribe scene loaded event
        Instance = null;
    }
    // Locates active local or networked player avatar GameObject in the scene.
    private GameObject FindPlayerInstance()
    {
        GameObject found = null;

        if (NetworkPlayer.Local != null)
            found = NetworkPlayer.Local.gameObject; // Priority 1: Networked local player

        if (found == null)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) found = pm.gameObject; // Priority 2: Offline PlayerMovement
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

        return found != null ? found.transform.root.gameObject : null;
    }

    // Safely migrates a GameObject to the target scene avoiding hierarchy exceptions.
    private static bool SafeMoveToScene(GameObject go, Scene scene)
    {
        if (go == null || !scene.IsValid() || !scene.isLoaded) return false;
        if (go.scene == scene) return true;
        try
        {
            var root = go.transform.root.gameObject;
            SceneManager.MoveGameObjectToScene(root, scene); // Move root transform to target scene
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DungeonManager] MoveGameObjectToScene('{go.name}' → '{scene.name}') failed: {ex.Message}");
            return false;
        }
    }
    // Requests dungeon entry from backend API, records overworld return coordinates, and initiates scene transition.
    public void StartDungeon(int configId, string dungeonSceneName, int cost, string dungeonName, List<string> partyMembers = null)
    {
        CurrentDungeonConfigId = configId; // Cache dungeon definition ID
        CurrentDungeonCost = cost; // Cache stamina/energy cost
        CurrentDungeonName = dungeonName; // Cache dungeon name
        _currentPartyMembers = partyMembers ?? new List<string>();
        _currentDungeonSceneName = dungeonSceneName;

        EnemiesKilledCount = 0; // Reset wave kill counters
        bossKilled = false;
        _currentPhase = DungeonPhase.Normal;
        _normalEnemies.Clear();
        _bossEnemies.Clear();
        _seenEnemies.Clear();
        EnemyProgress.Clear();
        _bossDeathPosition = Vector3.zero;
        _masterSpawnRetried = false;
        _spawnStarted = false;

        PreviousMapSceneName = WorldState.CurrentMapName; // Save overworld zone to return to after completion

        var player = FindPlayerInstance();
        if (player != null && player.transform.position != Vector3.zero)
        {
            PreviousPlayerPosition = player.transform.position; // Save overworld spawn position
            HasPreviousPlayerPosition = true;
        }
        else
        {
            PreviousPlayerPosition = WorldState.LastPosition;
            HasPreviousPlayerPosition = true;
        }

        DungeonApi.Instance.Enter(configId, partyMembers ?? new List<string>(),
            onSuccess: response =>
            {
                if (response != null && response.DungeonSessionId > 0)
                {
                    CurrentSessionId = response.DungeonSessionId; // Record live session ID
                    IsInDungeon = true;
                    Debug.Log($"[DungeonManager] Session created: {CurrentSessionId}");

                    StartCoroutine(TransitionToDungeon(dungeonSceneName)); // Load dungeon scene asynchronously
                }
                else
                {
                    Debug.LogWarning("[DungeonManager] Enter API succeeded but returned no session id. Aborting dungeon entry.");
                    NotifyBlocked("Cannot enter dungeon: backend returned no session.");
                }
            },
            onError: error =>
            {
                Debug.LogWarning($"[DungeonManager] Enter API failed: {error.Message}. Aborting dungeon entry.");
                NotifyBlocked($"Cannot enter dungeon: {error.Message}");
            }
        );
    }


// Create party session using config id, dungeon scene name, cost, and dungeon name; it guards invalid or unavailable states.
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

    // Executes core business logic for is party host.
    // Logic details: validates required non-empty string arguments; validates numeric boundary constraints.
    public bool IsPartyHost { get; private set; }

    // Executes core business logic for owns session.
    // Logic details: validates required non-empty string arguments; validates numeric boundary constraints.
    private static bool OwnsSession =>
        PhotonManager.Instance?.IsHost == true || NetworkPlayer.Local == null;

    // Process enter dungeon scene using config id, dungeon scene name, cost, and dungeon name; it loads player instance and starts the timed Unity sequence and guards invalid or unavailable states.
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

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(TransitionToDungeon(dungeonSceneName));
    }

    // Executes core business logic for transition to dungeon.
    private IEnumerator TransitionToDungeon(string dungeonSceneName)
    {
        yield return LoadingScreen.Show("Entering dungeon...");

        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainScene = SceneManager.GetSceneByName("Main");
            if (SafeMoveToScene(player, mainScene))
                Debug.Log("[DungeonManager] Moved player to Main scene defensively.");
        }

        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "Main" && s.name != dungeonSceneName && s.name != LoadingScreen.SceneName && s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        WorldState.LastPosition = Vector3.zero;
        WorldState.CurrentMapName = _currentDungeonSceneName;

        yield return SceneManager.LoadSceneAsync(_currentDungeonSceneName, LoadSceneMode.Additive);

        BeginEnemySpawn(_currentDungeonSceneName);

        if (player == null)
        {
            float waitAvatar = 5f;
            while (waitAvatar > 0f && (player = FindPlayerInstance()) == null)
            {
                waitAvatar -= Time.deltaTime;
                yield return null;
            }
        }

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

        if (player != null)
        {
            BindCameraToPlayer(player, dungeonSceneName);
        }

        var mainSceneObj = SceneManager.GetSceneByName("Main");
        if (mainSceneObj.IsValid())
        {
            SceneManager.SetActiveScene(mainSceneObj);
        }

        PlayerHUDUIManager.Instance?.ToggleDungeonMode(true);
        Debug.Log($"[DungeonManager] Entered dungeon scene: {dungeonSceneName}");

        yield return LoadingScreen.Hide();
    }

    // Executes core business logic for on scene loaded.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsInDungeon || scene.name != _currentDungeonSceneName)
            return;

        BeginEnemySpawn(scene.name);
    }

    // Executes core business logic for begin enemy spawn.
    private void BeginEnemySpawn(string mapName)
    {
        _isRestarting = false;
        if (_spawnStarted) return;
        _spawnStarted = true;

        Debug.Log($"[DungeonManager] Dungeon scene loaded: {mapName}. Starting spawn + registration...");

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(SpawnAndRegisterEnemies(mapName));
    }

    // Executes core business logic for spawn and register enemies.
    private IEnumerator SpawnAndRegisterEnemies(string mapName)
    {
        yield return null;

        var photon = PhotonManager.Instance;
        bool online = photon != null && photon.IsDungeonSession;

        if (online)
        {
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
                    foreach (var enemy in spawnedEnemies)
                    {
                        RegisterNetworkedEnemy(enemy);
                    }
                    Debug.Log($"[DungeonManager] Registered {_normalEnemies.Count} normal enemies.");
                }
                else
                {
                    yield return SweepReplicatedEnemies();
                }

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
            Debug.LogWarning("[DungeonManager] No DungeonSpawner found in scene. " +
                             "Falling back to scanning for pre-placed EnemyEntity objects. " +
                             "Add a DungeonSpawner component to the dungeon scene for data-driven spawning.");
            yield return new WaitForSeconds(0.5f);

            var enemies = FindObjectsByType<EnemyEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Debug.Log($"[DungeonManager] Fallback: found {enemies.Length} pre-placed enemies.");

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

    // Executes core business logic for any enemy in scene.
    // Returns a boolean indicating operation success.
    private static bool AnyEnemyInScene() =>
        FindObjectsByType<EnemyEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length > 0;

    // Executes core business logic for sweep replicated enemies.
    private IEnumerator SweepReplicatedEnemies()
    {
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
                             // Spawn through Fusion so state authority and replication are assigned consistently.
                             "the master client may not have run Runner.Spawn yet (check its " +
                             "console). Reconcile keeps polling, so a late spawn still lands.");
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(ReconcileReplicatedEnemiesLoop());
    }

    // Executes core business logic for reconcile replicated enemies.
    private int ReconcileReplicatedEnemies()
    {
        var found = FindObjectsByType<EnemyEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var enemy in found) RegisterNetworkedEnemy(enemy);
        return found.Length;
    }

    // Executes core business logic for reconcile replicated enemies loop.
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

    // Executes core business logic for register networked enemy.
    public void RegisterNetworkedEnemy(EnemyEntity enemy)
    {
        if (enemy == null || _isRestarting) return;

        if (!_seenEnemies.Add(enemy)) return;
        if (enemy.IsDead) return;

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

    // Executes core business logic for get clean enemy name.
    private string GetCleanEnemyName(EnemyEntity enemy)
    {
        if (enemy == null || enemy.gameObject == null) return "Unknown";
        string cleanName = enemy.gameObject.name.Replace("(Clone)", "").Trim();

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

        cleanName = cleanName.Replace("_", "");

        return cleanName;
    }


    // Executes core business logic for handle normal enemy death.
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
        int percentage = remaining == 0 ? 50
                       : Mathf.Min(49, (EnemiesKilledCount * 50) / Mathf.Max(1, total));

        Debug.Log($"[DungeonManager] Normal enemy killed. Remaining: {remaining}. Progress: {percentage}%");

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

        var photon = PhotonManager.Instance;
        bool isProxy = photon != null && photon.IsDungeonSession && !photon.IsHost;

        if (remaining == 0 && _currentPhase == DungeonPhase.Normal && !isProxy)
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(TriggerBossSequence());
    }

    // Executes core business logic for trigger boss sequence.
    private IEnumerator TriggerBossSequence()
    {
        _currentPhase = DungeonPhase.BossSpawning;
        Debug.Log("[DungeonManager] All normals defeated. Starting boss sequence (shake → spawn).");

        if (PhotonManager.Instance?.IsHost == true && NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.RPC_BossSpawning();
        }

        DungeonScreenShake.Shake(duration: 0.9f, magnitude: 0.28f);
        yield return new WaitForSeconds(1.2f);

        if (DungeonSpawner.Instance == null)
        {
            Debug.LogWarning("[DungeonManager] DungeonSpawner not found. Skipping boss and completing dungeon.");
            _currentPhase = DungeonPhase.Complete;
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
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
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            yield return StartCoroutine(BossDeathSequence(GetFallbackChestPosition()));
            yield break;
        }

        _currentPhase = DungeonPhase.Boss;
        Debug.Log($"[DungeonManager] Boss '{boss.name}' spawned. Phase → Boss.");
    }

    // Executes core business logic for handle boss enemy death.
    private void HandleBossEnemyDeath(object sender, EventArgs e)
    {
        if (sender is not EnemyEntity boss) return;

        boss.OnDeath -= HandleBossEnemyDeath;
        _bossEnemies.Remove(boss);

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
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(BossDeathSequence(_bossDeathPosition));
    }


    // Executes core business logic for client receive boss spawning.
    public void ClientReceiveBossSpawning()
    {
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(ClientBossSequence());
    }

    // Executes core business logic for client boss sequence.
    private IEnumerator ClientBossSequence()
    {
        _currentPhase = DungeonPhase.BossSpawning;
        Debug.Log("[DungeonManager] Client received boss spawning event. Shaking screen...");
        DungeonScreenShake.Shake(duration: 0.9f, magnitude: 0.28f);
        yield return new WaitForSeconds(1.2f);
        _currentPhase = DungeonPhase.Boss;
    }

    // Executes core business logic for client receive boss death.
    public void ClientReceiveBossDeath(Vector3 chestPosition)
    {
        if (_currentPhase == DungeonPhase.Complete) return;

        _currentPhase = DungeonPhase.Complete;
        Debug.Log($"[DungeonManager] Client received boss death event at {chestPosition}. Starting completion sequence.");
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(BossDeathSequence(chestPosition));
    }

    // Executes core business logic for boss death sequence.
    private IEnumerator BossDeathSequence(Vector3 chestPosition)
    {
        bool updateDone = false;
        bool completeDone = false;

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

            yield return new WaitUntil(() => updateDone);

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
            updateDone = true;
            completeDone = true;
        }

        CreditDungeonExploreQuests();

        yield return new WaitForSeconds(1.5f);

        SpawnFinalChestAtPosition(chestPosition);
    }

    // Executes core business logic for notify blocked.
    private static void NotifyBlocked(string message)
    {
        Debug.LogWarning($"[DungeonManager] {message}");
        if (MainQuestPanelRuntime.Instance != null)
            MainQuestPanelRuntime.Instance.ShowPaperPopup(message, UIPaperPopupView.PaperPopupKind.None);
    }

    // Executes core business logic for credit dungeon explore quests.
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
        }

        if (credited)
            QuestUIManager.Instance.FlushPendingProgressNow();
    }


    [Header("Dungeon Rewards")]
    [Tooltip("Kéo Prefab Rương của bạn vào đây (vd: DarkChest)")]
    public GameObject rewardChestPrefab;

    private GameObject _rewardChest;

    // Executes core business logic for despawn reward chest.
    private void DespawnRewardChest()
    {
        if (_rewardChest != null) Destroy(_rewardChest);
        _rewardChest = null;
    }

    // Executes core business logic for spawn final chest at position.
    private void SpawnFinalChestAtPosition(Vector3 targetPosition)
    {
        DespawnRewardChest();

        GameObject chestGO = null;

        if (rewardChestPrefab != null)
        {
            chestGO = Instantiate(rewardChestPrefab, targetPosition + Vector3.up * 6f, Quaternion.identity);
            chestGO.name = "DungeonChest";
            SafeMoveToScene(chestGO, SceneManager.GetSceneByName(WorldState.CurrentMapName));

            var chestScript = chestGO.GetComponent<DungeonChest>();
            if (chestScript == null) chestScript = chestGO.AddComponent<DungeonChest>();
            chestScript.enabled = true;

            Debug.Log("[DungeonManager] Spawned chest from assigned prefab with drop animation.");
        }
        else
        {
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
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(ChestDropAnimation(chestGO, targetPosition));
    }

    // Executes core business logic for chest drop animation.
    private IEnumerator ChestDropAnimation(GameObject chest, Vector3 targetPosition)
    {
        if (chest == null) yield break;

        Vector3 startPos = chest.transform.position;
        float elapsed    = 0f;
        const float duration = 0.55f;

        while (elapsed < duration)
        {
            if (chest == null) yield break;
            float t     = elapsed / duration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            chest.transform.position = Vector3.Lerp(startPos, targetPosition, eased);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (chest != null)
            chest.transform.position = targetPosition;

        Debug.Log($"[DungeonManager] Reward chest landed at {targetPosition}.");
    }

    // Executes core business logic for get fallback chest position.
    private Vector3 GetFallbackChestPosition()
    {
        var player = FindPlayerInstance();
        return player != null ? player.transform.position + Vector3.right * 2f : Vector3.zero;
    }

    [System.Obsolete("Death events are now handled automatically by HandleNormalEnemyDeath and HandleBossEnemyDeath.")]
    // Executes core business logic for update monster kill.
    public void UpdateMonsterKill(bool isBoss)
    {
        Debug.LogWarning("[DungeonManager] UpdateMonsterKill is deprecated and does nothing. " +
                         "Death events are handled automatically.");
    }

    // Executes core business logic for return to world map.
    public void ReturnToWorldMap()
    {
        if (_isReturningToWorld) return;
        _isReturningToWorld = true;
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(TransitionToWorld());
    }

    // Executes core business logic for transition to world.
    private IEnumerator TransitionToWorld()
    {
        yield return LoadingScreen.Show("Returning to world...");

        var photon = PhotonManager.Instance;
        if (photon != null && photon.IsDungeonSession)
        {
            Debug.Log("[DungeonManager] Exiting dungeon room → migrating back to social lobby.");
            var migrate = photon.MigrateToSocialLobbyAsync();
            while (!migrate.IsCompleted) yield return null;
        }

        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainSceneObj = SceneManager.GetSceneByName("Main");
            if (SafeMoveToScene(player, mainSceneObj))
                Debug.Log("[DungeonManager] Moved player to Main scene defensively.");
        }

        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "Main" && s.name != PreviousMapSceneName && s.name != LoadingScreen.SceneName && s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        Vector3 returnPos = HasPreviousPlayerPosition ? PreviousPlayerPosition : WorldState.LastPosition;
        if (!HasPreviousPlayerPosition && returnPos == Vector3.zero) returnPos = new Vector3(11.9f, 17.8f, 0f);
        WorldState.LastPosition = returnPos;
        WorldState.CurrentMapName = PreviousMapSceneName;

        yield return SceneManager.LoadSceneAsync(PreviousMapSceneName, LoadSceneMode.Additive);

        if (player != null)
        {
            var worldScene = SceneManager.GetSceneByName(PreviousMapSceneName);
            if (SafeMoveToScene(player, worldScene))
            {
                player.transform.position = returnPos;
                Debug.Log($"[DungeonManager] Moved player into world scene: {PreviousMapSceneName} at {returnPos}");

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

        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.IsValid())
        {
            SceneManager.SetActiveScene(mainScene);
        }

        PlayerHUDUIManager.Instance?.ToggleDungeonMode(false);
        DespawnRewardChest();
        IsInDungeon = false;
        Debug.Log($"[DungeonManager] Returned to map: {PreviousMapSceneName} at {WorldState.LastPosition}");

        yield return LoadingScreen.Hide();
        _isReturningToWorld = false;
    }

    // Executes core business logic for adopt restart session.
    // Logic details: validates numeric boundary constraints.
    public void AdoptRestartSession(int sessionId)
    {
        if (sessionId <= 0) return;
        if (CurrentSessionId == sessionId) return;
        CurrentSessionId = sessionId;
        IsInDungeon = true;
        Debug.Log($"[DungeonManager] Adopted host restart session: {sessionId}");
    }

    // Executes core business logic for restart dungeon.
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

        DespawnRewardChest();

        var progress = FindFirstObjectByType<UIDungeonProgressPanel>(FindObjectsInactive.Include);
        if (progress != null) progress.ResetProgress();


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

                        if (NetworkPlayer.Local != null)
                            NetworkPlayer.Local.RPC_SetRestartSession(CurrentSessionId);

                        var p = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
                        if (p != null) p.gameObject.SetActive(false);

                        string sceneToLoad = _currentDungeonSceneName;
                        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                        StartCoroutine(TransitionToRestart(sceneToLoad));
                    }
                },
                onError: error =>
                {
                    Debug.LogWarning($"[DungeonManager] Restart API failed: {error.Message}. Proceeding to restart anyway for testing.");
                    NotifyBlocked($"Cannot Restart API: {error.Message}");

                    CurrentSessionId = -1;
                    IsInDungeon = true;
                    var p = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
                    if (p != null) p.gameObject.SetActive(false);

                    string sceneToLoad = _currentDungeonSceneName;
                    // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                    StartCoroutine(TransitionToRestart(sceneToLoad));
                }
            );
        }
        else
        {
            Debug.Log("[DungeonManager] Non-host restarting dungeon scene locally.");
            IsInDungeon = true;
            var p = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
            if (p != null) p.gameObject.SetActive(false);
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(TransitionToRestart(_currentDungeonSceneName));
        }
    }

    // Executes core business logic for transition to restart.
    private IEnumerator TransitionToRestart(string dungeonSceneName)
    {
        yield return LoadingScreen.Show("Restarting dungeon...");

        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainScene = SceneManager.GetSceneByName("Main");
            SafeMoveToScene(player, mainScene);
        }

        var currentDungeonScene = SceneManager.GetSceneByName(dungeonSceneName);
        if (currentDungeonScene.IsValid() && currentDungeonScene.isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(currentDungeonScene);
        }

        yield return SceneManager.LoadSceneAsync(dungeonSceneName, LoadSceneMode.Additive);

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

        var mainActiveScene = SceneManager.GetSceneByName("Main");
        if (mainActiveScene.IsValid())
        {
            SceneManager.SetActiveScene(mainActiveScene);
        }

        Debug.Log($"[DungeonManager] Successfully restarted dungeon: {dungeonSceneName}");

        yield return LoadingScreen.Hide();
    }

    // Executes core business logic for get scene name.
    private string GetSceneName(GameObject go)
    {
        if (go == null) return string.Empty;
        var scene = go.scene;
        return scene.IsValid() ? scene.name : string.Empty;
    }

    // Executes core business logic for bind camera to player.
    private void BindCameraToPlayer(GameObject player, string sceneName)
    {
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
                    cam.Priority = 999;
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
