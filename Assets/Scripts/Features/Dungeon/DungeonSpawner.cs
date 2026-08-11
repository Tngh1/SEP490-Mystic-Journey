using System;
using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Data-driven dungeon monster spawner. Placed as a component in the dungeon scene.
///
/// ── Architecture ──────────────────────────────────────────────────────────────
///
///   DungeonManager  (Controller / Orchestrator)
///         │
///         │  SpawnMonstersForDungeon(configId, mapName, callback)
///         ▼
///   DungeonSpawner  (Coordinator — calls API, drives Allocator, drives Instantiate)
///         │                               │
///         │  GetSpawnsForMap()            │  Allocate()
///         ▼                               ▼
///   MonsterApi                      SpawnAllocator
///   (existing API layer)            (pure algorithm — no Unity ops)
///         │                               │
///         ▼                               ▼
///   MonsterSpawnResponse[]      List&lt;SpawnRequest&gt;
///                                         │
///   MonsterDatabaseSO  ──────────────────►│  ResolvePrefab()
///   (ScriptableObject)                    │
///                                         ▼
///                                    Instantiate()
///                                    Set EnemyEntity.SetSpawnData()
///                                         │
///                                         ▼
///                                    callback(List&lt;EnemyEntity&gt;)
///
/// ── Two-Phase Spawning ────────────────────────────────────────────────────────
///   Phase 1 — ALLOCATION (SpawnAllocator):
///     Pure data mapping: which monster type goes to which SpawnPoint.
///     No GameObjects are created yet. Enables future portal animations,
///     multiplayer sync, or editor previews without touching the algorithm.
///
///   Phase 2 — INSTANTIATION (DungeonSpawner):
///     Creates GameObjects from the SpawnRequest list and wires them up.
///
/// ── Boss Exclusion ────────────────────────────────────────────────────────────
///   Boss-type monsters are filtered out here. The BossSpawner handles them
///   separately via BossSpawn. See IsBossType() for detection rules.
/// </summary>
public class DungeonSpawner : MonoBehaviour
{
    // ── Scene-local singleton (NOT DontDestroyOnLoad — lives only in dungeon scene) ──
    public static DungeonSpawner Instance { get; private set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // INSPECTOR REFERENCES
    // ═══════════════════════════════════════════════════════════════════════════

    [Header("Monster Prefab Registry")]
    [Tooltip("ScriptableObject mapping MonsterId → MonsterPrefab. MUST be assigned.")]
    [SerializeField] private MonsterDatabaseSO monsterDatabase;

    [Header("Scene References (auto-discovered if left empty)")]
    [Tooltip("Root GameObject 'MonsterContainer'. All spawned enemies will be parented here.")]
    [SerializeField] private Transform monsterContainer;

    [Tooltip("Child of MonsterContainer named 'SpawnGroups'. Parent of all SpawnGroup_X objects.")]
    [SerializeField] private Transform spawnGroupsRoot;

    // Data for the boss, extracted from the API response and held until boss phase
    private DungeonMonsterSpawnData _bossSpawnData;

    // ═══════════════════════════════════════════════════════════════════════════
    // UNITY LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PUBLIC ENTRY POINT — called by DungeonManager after dungeon scene loads
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Full spawning pipeline (two-phase: allocate → instantiate).
    /// Calls MonsterApi DIRECTLY — does NOT route through MonsterManager.
    /// This keeps the monster gameplay manager (MonsterManager) separate from
    /// dungeon session responsibilities.
    /// </summary>
    /// <param name="dungeonConfigId">
    ///   The dungeon's config ID, passed as the 'dungeonId' filter to the API.
    /// </param>
    /// <param name="mapName">
    ///   Scene/map name, used as the 'mapName' parameter in the spawns API call.
    /// </param>
    /// <param name="onComplete">
    ///   Invoked when all monsters are spawned.
    ///   Delivers the list of EnemyEntity components so DungeonManager can
    ///   subscribe to OnDeath events and track active enemies.
    /// </param>
    public void SpawnMonstersForDungeon(int dungeonConfigId, string mapName, Action<List<EnemyEntity>> onComplete)
    {
        StartCoroutine(SpawnPipeline(dungeonConfigId, mapName, onComplete));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE PIPELINE
    // ═══════════════════════════════════════════════════════════════════════════

    private IEnumerator SpawnPipeline(int dungeonConfigId, string mapName, Action<List<EnemyEntity>> onComplete)
    {
        // Wait one frame so all scene objects have completed their Awake/Start
        yield return null;

        // ── 1. Resolve scene hierarchy references ────────────────────────────────
        ResolveSceneReferences();

        if (monsterContainer == null)
        {
            Debug.LogError("[DungeonSpawner] 'MonsterContainer' not found in scene. " +
                           "Create a GameObject named 'MonsterContainer' with a child 'SpawnGroups'. " +
                           "Aborting spawn.");
            onComplete?.Invoke(new List<EnemyEntity>());
            yield break;
        }

        // ── 2. Collect SpawnGroups — reset each for a fresh run ──────────────────
        List<SpawnGroupController> spawnGroups = CollectAndResetSpawnGroups();

        if (spawnGroups.Count == 0)
        {
            Debug.LogWarning("[DungeonSpawner] No SpawnGroupControllers found. " +
                             "Add SpawnGroup_X GameObjects with the SpawnGroupController component " +
                             "and at least one child SpawnPoint Transform.");
            onComplete?.Invoke(new List<EnemyEntity>());
            yield break;
        }

        // Clear previous boss data for fresh run
        _bossSpawnData = null;

        Debug.Log($"[DungeonSpawner] Starting spawn pipeline | dungeonId={dungeonConfigId} | map='{mapName}' | groups={spawnGroups.Count}");

        // ── 3. Fetch spawn data from backend — calls MonsterApi DIRECTLY ─────────
        bool apiDone = false;
        MonsterSpawnResponse[] apiSpawns = null;

        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[DungeonSpawner] No auth token. Monster spawn API requires authentication. " +
                             "Ensure player is logged in before entering a dungeon.");
            onComplete?.Invoke(new List<EnemyEntity>());
            yield break;
        }

        MonsterApi.Instance.GetSpawnsForMap(
            mapName,
            onSuccess: data =>
            {
                apiSpawns = data ?? System.Array.Empty<MonsterSpawnResponse>();
                apiDone = true;
            },
            onError: err =>
            {
                Debug.LogError($"[DungeonSpawner] MonsterApi.GetSpawnsForMap failed: {err.Message} (code: {err.StatusCode})");
                apiSpawns = System.Array.Empty<MonsterSpawnResponse>();
                apiDone = true;
            },
            dungeonId: dungeonConfigId
        );

        yield return new WaitUntil(() => apiDone);

        if (apiSpawns.Length == 0)
        {
            Debug.LogWarning($"[DungeonSpawner] Backend returned 0 spawn entries for " +
                             $"dungeonId={dungeonConfigId}, map='{mapName}'. " +
                             "Verify MonsterSpawn rows with DungeonId set exist in the database.");
            onComplete?.Invoke(new List<EnemyEntity>());
            yield break;
        }

        // ── 4. Build the typed spawn queue (filter bosses, aggregate by type, resolve prefabs) ──
        List<DungeonMonsterSpawnData> spawnQueue = BuildSpawnQueue(apiSpawns);

        if (spawnQueue.Count == 0)
        {
            Debug.LogWarning("[DungeonSpawner] Spawn queue empty after filtering. " +
                             "Check MonsterDatabaseSO has entries for all non-boss MonsterId values.");
            onComplete?.Invoke(new List<EnemyEntity>());
            yield break;
        }

        // ── 5. PHASE 1 — Allocate: pure algorithm, no Instantiate ───────────────
        List<SpawnRequest> spawnRequests = SpawnAllocator.Allocate(spawnQueue, spawnGroups);

        Debug.Log($"[DungeonSpawner] Allocation: {spawnRequests.Count} spawn slots assigned.");

        // ── 6. PHASE 2 — Instantiate from the allocation plan ───────────────────
        //   (This separation allows a spawn animation coroutine, portal effects,
        //    networked sync, etc. to be inserted here without touching the algorithm.)
        var spawnedEnemies = new List<EnemyEntity>(spawnRequests.Count);
        InstantiateAll(spawnRequests, spawnedEnemies);

        Debug.Log($"[DungeonSpawner] ✓ Spawn complete — {spawnedEnemies.Count} enemies placed.");
        onComplete?.Invoke(spawnedEnemies);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // STEP IMPLEMENTATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    // ── Step 1: Scene reference resolution ──────────────────────────────────────
    private void ResolveSceneReferences()
    {
        if (monsterContainer == null)
        {
            var go = GameObject.Find("MonsterContainer");
            monsterContainer = go != null ? go.transform : null;
            if (monsterContainer != null)
                Debug.Log("[DungeonSpawner] Auto-resolved: MonsterContainer");
        }

        if (spawnGroupsRoot == null && monsterContainer != null)
        {
            spawnGroupsRoot = monsterContainer.Find("SpawnGroups");
            if (spawnGroupsRoot != null)
                Debug.Log("[DungeonSpawner] Auto-resolved: SpawnGroups");
        }
    }

    // ── Step 2: Collect all SpawnGroupControllers and reset them for this run ───
    private List<SpawnGroupController> CollectAndResetSpawnGroups()
    {
        var groups = new List<SpawnGroupController>();
        Transform searchRoot = spawnGroupsRoot != null ? spawnGroupsRoot : monsterContainer;

        for (int i = 0; i < searchRoot.childCount; i++)
        {
            var group = searchRoot.GetChild(i).GetComponent<SpawnGroupController>();
            if (group != null && group.Capacity > 0)
            {
                group.ResetGroup(); // Restore + shuffle free points for this run
                groups.Add(group);
            }
        }

        Debug.Log($"[DungeonSpawner] Found {groups.Count} SpawnGroups, each reset for this run.");
        return groups;
    }

    // ── Step 4: Build spawn queue from API response ──────────────────────────────
    /// <summary>
    /// Aggregates MonsterSpawnResponse[] into a typed list:
    ///   - Filters out boss-type monsters (handled by BossSpawner).
    ///   - Groups entries by MonsterId and sums SpawnCount.
    ///   - Resolves each MonsterId to a Unity prefab via MonsterDatabaseSO.
    ///   - Skips types with no prefab match (logged as warnings).
    /// </summary>
    private List<DungeonMonsterSpawnData> BuildSpawnQueue(MonsterSpawnResponse[] responses)
    {
        var aggregated = new Dictionary<int, DungeonMonsterSpawnData>();

        foreach (var response in responses)
        {
            if (response == null) continue;

            // ── Extract bosses (Boss phase handles those later) ───────────────────────
            if (IsBossType(response))
            {
                Debug.Log($"[DungeonSpawner] Extracted Boss data: '{response.MonsterName}' (id={response.MonsterId})");
                GameObject bossPrefab = ResolvePrefab(response.MonsterId);
                
                if (bossPrefab != null)
                {
                    _bossSpawnData = new DungeonMonsterSpawnData
                    {
                        MonsterId      = response.MonsterId,
                        MonsterSpawnId = response.MonsterSpawnId,
                        MonsterName    = !string.IsNullOrEmpty(response.MonsterName)
                                         ? response.MonsterName
                                         : $"Boss_{response.MonsterId}",
                        Quantity       = 1,
                        Prefab         = bossPrefab
                    };
                }
                else
                {
                    Debug.LogError($"[DungeonSpawner] Boss '{response.MonsterName}' has no prefab in MonsterDatabaseSO.");
                }
                continue;
            }

            if (!aggregated.TryGetValue(response.MonsterId, out var existing))
            {
                GameObject prefab = ResolvePrefab(response.MonsterId);

                if (prefab == null)
                {
                    Debug.LogWarning($"[DungeonSpawner] No prefab for MonsterId={response.MonsterId} " +
                                     $"('{response.MonsterName}'). Skipping. " +
                                     "Add a MonsterClientData entry in MonsterDatabaseSO.");
                    continue;
                }

                existing = new DungeonMonsterSpawnData
                {
                    MonsterId      = response.MonsterId,
                    MonsterSpawnId = response.MonsterSpawnId,
                    MonsterName    = !string.IsNullOrEmpty(response.MonsterName)
                                     ? response.MonsterName
                                     : $"Monster_{response.MonsterId}",
                    Quantity = 0,
                    Prefab   = prefab
                };
                aggregated[response.MonsterId] = existing;
            }

            existing.Quantity += Mathf.Max(1, response.SpawnCount);
        }

        var queue = new List<DungeonMonsterSpawnData>(aggregated.Values);
        foreach (var entry in queue)
            Debug.Log($"[DungeonSpawner]   → Queue: '{entry.MonsterName}' x{entry.Quantity}");

        return queue;
    }

    // ── Step 6: Instantiate all spawn requests ───────────────────────────────────
    private void InstantiateAll(List<SpawnRequest> requests, List<EnemyEntity> spawnedEnemies)
    {
        int index = 0;
        foreach (var request in requests)
        {
            index++;
            GameObject instance = SpawnEnemyObject(
                request.Prefab,
                request.Position,
                Quaternion.identity);

            if (instance == null) continue; // proxy client — enemy arrives replicated

            instance.name = $"{request.MonsterName}_{index}";

            var entity = instance.GetComponent<EnemyEntity>();
            if (entity != null)
            {
                // Inject backend IDs so EnemyEntity can report defeats to the server
                entity.SetSpawnData(request.MonsterId, request.MonsterSpawnId);
                spawnedEnemies.Add(entity);
            }
            else
            {
                Debug.LogWarning($"[DungeonSpawner] Prefab '{request.Prefab.name}' has no EnemyEntity component. " +
                                 "This enemy will not be tracked by DungeonManager.");
            }

            Debug.Log($"[DungeonSpawner]   ✓ '{instance.name}' at {request.Position} (group: {request.GroupName})");
        }
    }

    /// <summary>
    /// Create one enemy GameObject. Online (Photon running) ONLY the master client
    /// spawns it as a networked object via Runner.Spawn — it holds authority, runs
    /// the AI, and Fusion replicates the enemy to every other client (which return
    /// a null here and pick up the replicated NetworkObject instead). Offline it is
    /// a plain Instantiate exactly as before.
    /// </summary>
    private GameObject SpawnEnemyObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var photon = PhotonManager.Instance;
        bool online = photon != null && photon.IsDungeonSession;

        if (online && prefab.GetComponent<Fusion.NetworkObject>() != null)
        {
            // Non-authority clients do not spawn — they receive the replica.
            if (!photon.IsHost)
            {
                Debug.Log($"[DungeonSpawner] Proxy — skipping local spawn of '{prefab.name}', waiting for replica.");
                return null;
            }

            // Runner.Spawn can legitimately fail (runner shutting down, its gameplay
            // SceneRef unloaded by the dungeon transition, prefab not in the Fusion
            // table). Swallowing that silently was why "no monsters" left no trace in
            // the console: the master returned null exactly like a proxy, InstantiateAll
            // skipped it, and DungeonManager registered an empty list — so every client
            // sat on "Loading..." with nothing to explain it.
            Fusion.NetworkObject netObj = null;
            try
            {
                netObj = photon.Runner.Spawn(prefab, position, rotation);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DungeonSpawner] Runner.Spawn THREW for '{prefab.name}': {ex.Message}");
                return null;
            }

            if (netObj == null)
            {
                Debug.LogError($"[DungeonSpawner] Runner.Spawn returned NULL for '{prefab.name}' " +
                               $"(IsHost={photon.IsHost}, runner running={photon.IsConnected}). " +
                               "Enemy will be missing on every client.");
                return null;
            }

            // Do NOT reparent networked enemies: Fusion's NetworkTransform replicates
            // LOCAL position and this prefab has SyncParent disabled, so parenting the
            // authority's enemy under monsterContainer (not at the origin) would make its
            // local position = world - containerPos; the proxy, staying unparented, would
            // then render it offset by containerPos — off-screen (the old "hit but
            // invisible" bug).
            //
            // BUT Runner.Spawn drops the object into the ACTIVE scene, which is "Main" by
            // the time the spawner runs (TransitionToDungeon re-activates Main). Enemies
            // must live in the dungeon scene so they UNLOAD WITH IT — otherwise they leak
            // into Main when the dungeon closes. (NavMesh access is not a reason: the
            // navigation world is global, see the agent comment below.)
            // Move by SCENE MEMBERSHIP only (world position preserved, no transform
            // parent) — this restores what the reparent used to provide, without the
            // local-position offset.
            var dungeonScene = SceneManager.GetSceneByName(WorldState.CurrentMapName);
            if (dungeonScene.IsValid() && dungeonScene.isLoaded)
                SceneManager.MoveGameObjectToScene(netObj.gameObject, dungeonScene);

            // Re-initialise the agent so it binds to the NavMesh.
            //
            // NavMeshes are NOT per-scene: NavMeshSurface.OnEnable calls
            // NavMesh.AddNavMeshData, which registers into ONE global navigation world.
            // So the scene move above does not grant NavMesh access, and an agent
            // instantiated in Main is not disadvantaged by that. What actually decides
            // binding is whether a walkable surface exists at the spawn position.
            // The toggle is kept because Runner.Spawn can instantiate before the dungeon
            // surface has registered; re-enabling re-runs the bind against current data.
            //
            // This path only runs on the host (authority) — the proxy disables its agent
            // in NetworkEnemy.Spawned, so we never fight that here.
            var agent = netObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
                agent.enabled = true;
                // Note: no agent.Warp(hit.position) — that could warp enemies under the floor.
                // Unity snaps the agent to the nearest walkable surface when enabled.

                // Report the bind result instead of asserting it. This used to log
                // "Agent bound to NavMesh" unconditionally, which reported success while
                // Unity was logging "Failed to create agent" for the same object — the
                // reason an empty NavMesh bake stayed hidden. EnemyBehaviour gates all
                // movement on isOnNavMesh, so a false here means the enemy cannot move.
                if (agent.isOnNavMesh)
                {
                    Debug.Log($"[DungeonSpawner] Spawned {prefab.name} at {position}. Agent bound to NavMesh.");
                }
                else
                {
                    Debug.LogError($"[DungeonSpawner] Spawned {prefab.name} at {position} but its agent did " +
                                   "NOT bind to the NavMesh — this enemy will not move. Check that the " +
                                   $"'{WorldState.CurrentMapName}' NavMeshSurface is baked (a stale/empty bake " +
                                   "is the usual cause) and that the spawn point sits on walkable geometry.");
                }
            }

            return netObj.gameObject;
        }

        return Instantiate(prefab, position, rotation, monsterContainer);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BOSS SPAWNING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Spawns the boss that was extracted during the initial API call.
    /// Called by DungeonManager when all normal monsters are dead.
    /// </summary>
    public EnemyEntity SpawnBoss()
    {
        if (_bossSpawnData == null || _bossSpawnData.Prefab == null)
        {
            Debug.LogError("[DungeonSpawner] Cannot spawn boss: No boss data received from API or prefab missing.");
            return null;
        }

        var spawnGO = GameObject.Find("BossSpawn");
        if (spawnGO == null)
        {
            Debug.LogError("[DungeonSpawner] 'BossSpawn' GameObject not found in scene.");
            return null;
        }

        GameObject bossInstance = SpawnEnemyObject(
            _bossSpawnData.Prefab,
            spawnGO.transform.position,
            Quaternion.identity);

        if (bossInstance == null)
        {
            // Proxy client — the boss is spawned by the master client and arrives
            // as a replicated NetworkObject; no local instance to return.
            return null;
        }

        bossInstance.name = $"{_bossSpawnData.MonsterName}(Boss)";

        var entity = bossInstance.GetComponent<EnemyEntity>();
        if (entity != null)
        {
            entity.SetSpawnData(_bossSpawnData.MonsterId, _bossSpawnData.MonsterSpawnId);
            Debug.Log($"[DungeonSpawner] Boss '{bossInstance.name}' successfully spawned at {spawnGO.transform.position}.");
        }
        else
        {
            Debug.LogError($"[DungeonSpawner] Boss prefab '{_bossSpawnData.Prefab.name}' is missing EnemyEntity component.");
        }

        return entity;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Boss detection logic — matches existing DungeonManager convention.
    /// Returns true for monsters that should be spawned by BossSpawner instead.
    /// Rules (applied in order):
    ///   1. MonsterType == "Boss" (case-insensitive) — the flat field the API actually sends.
    ///   2. MonsterName contains "boss" or "ogre" (case-insensitive).
    ///
    /// Rule 1 used to read <c>response.Monster.Type</c>, but GET /api/monsters/spawns maps to
    /// MonsterSpawnResponseDto, which flattens the monster to MonsterName + MonsterType and
    /// carries no nested Monster object — so that check never fired and every boss row was
    /// queued as a normal enemy (no "Extracted Boss data" log, _bossSpawnData stayed null,
    /// SpawnBoss failed). The name fallback does not cover most seeded bosses either
    /// ("SwampDemon", "UnderKing", "OrcWarlord"), so MonsterType is the only reliable signal.
    /// </summary>
    private static bool IsBossType(MonsterSpawnResponse response)
    {
        if (!string.IsNullOrEmpty(response.MonsterType) &&
            response.MonsterType.Equals("Boss", StringComparison.OrdinalIgnoreCase))
            return true;

        string name = (response.MonsterName ?? string.Empty).ToLower();
        return name.Contains("boss") || name.Contains("ogre");
    }

    /// <summary>
    /// Resolves a Unity prefab for the given MonsterId from MonsterDatabaseSO.
    /// Returns null and logs an error if the database is unassigned or the ID is missing.
    /// </summary>
    private GameObject ResolvePrefab(int monsterId)
    {
        if (monsterDatabase == null)
        {
            Debug.LogError("[DungeonSpawner] MonsterDatabaseSO is NOT assigned! " +
                           "Drag the MonsterDatabase ScriptableObject asset into the " +
                           "'Monster Database' field on the DungeonSpawner component.");
            return null;
        }

        return monsterDatabase.GetMonsterData(monsterId)?.MonsterPrefab;
    }
}
