using System;
using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using UnityEngine;
using UnityEngine.SceneManagement;

// Executes mono behaviour operation.
public class DungeonSpawner : MonoBehaviour
{
    // Executes instance operation.
    public static DungeonSpawner Instance { get; private set; }


    [Header("Monster Prefab Registry")]
    [Tooltip("ScriptableObject mapping MonsterId → MonsterPrefab. MUST be assigned.")]
    [SerializeField] private MonsterDatabaseSO monsterDatabase;

    [Header("Scene References (auto-discovered if left empty)")]
    [Tooltip("Root GameObject 'MonsterContainer'. All spawned enemies will be parented here.")]
    [SerializeField] private Transform monsterContainer;

    [Tooltip("Child of MonsterContainer named 'SpawnGroups'. Parent of all SpawnGroup_X objects.")]
    [SerializeField] private Transform spawnGroupsRoot;

    private DungeonMonsterSpawnData _bossSpawnData;


    // Initializes internal component caches and dependencies for DungeonSpawner upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    // Executes spawn monsters for dungeon operation.
    public void SpawnMonstersForDungeon(int dungeonConfigId, string mapName, Action<List<EnemyEntity>> onComplete)
    {
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(SpawnPipeline(dungeonConfigId, mapName, onComplete));
    }


    // Executes spawn pipeline operation.
    private IEnumerator SpawnPipeline(int dungeonConfigId, string mapName, Action<List<EnemyEntity>> onComplete)
    {
        yield return null;

        ResolveSceneReferences();

        if (monsterContainer == null)
        {
            Debug.LogError("[DungeonSpawner] 'MonsterContainer' not found in scene. " +
                           "Create a GameObject named 'MonsterContainer' with a child 'SpawnGroups'. " +
                           "Aborting spawn.");
            onComplete?.Invoke(new List<EnemyEntity>());
            yield break;
        }

        List<SpawnGroupController> spawnGroups = CollectAndResetSpawnGroups();

        if (spawnGroups.Count == 0)
        {
            Debug.LogWarning("[DungeonSpawner] No SpawnGroupControllers found. " +
                             "Add SpawnGroup_X GameObjects with the SpawnGroupController component " +
                             "and at least one child SpawnPoint Transform.");
            onComplete?.Invoke(new List<EnemyEntity>());
            yield break;
        }

        _bossSpawnData = null;

        Debug.Log($"[DungeonSpawner] Starting spawn pipeline | dungeonId={dungeonConfigId} | map='{mapName}' | groups={spawnGroups.Count}");

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

        List<DungeonMonsterSpawnData> spawnQueue = BuildSpawnQueue(apiSpawns);

        if (spawnQueue.Count == 0)
        {
            Debug.LogWarning("[DungeonSpawner] Spawn queue empty after filtering. " +
                             "Check MonsterDatabaseSO has entries for all non-boss MonsterId values.");
            onComplete?.Invoke(new List<EnemyEntity>());
            yield break;
        }

        List<SpawnRequest> spawnRequests = SpawnAllocator.Allocate(spawnQueue, spawnGroups);

        Debug.Log($"[DungeonSpawner] Allocation: {spawnRequests.Count} spawn slots assigned.");

        var spawnedEnemies = new List<EnemyEntity>(spawnRequests.Count);
        InstantiateAll(spawnRequests, spawnedEnemies);

        Debug.Log($"[DungeonSpawner] ✓ Spawn complete — {spawnedEnemies.Count} enemies placed.");
        onComplete?.Invoke(spawnedEnemies);
    }


    // Executes resolve scene references operation.
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

    // Executes collect and reset spawn groups operation.
    private List<SpawnGroupController> CollectAndResetSpawnGroups()
    {
        var groups = new List<SpawnGroupController>();
        Transform searchRoot = spawnGroupsRoot != null ? spawnGroupsRoot : monsterContainer;

        for (int i = 0; i < searchRoot.childCount; i++)
        {
            var group = searchRoot.GetChild(i).GetComponent<SpawnGroupController>();
            if (group != null && group.Capacity > 0)
            {
                group.ResetGroup();
                groups.Add(group);
            }
        }

        Debug.Log($"[DungeonSpawner] Found {groups.Count} SpawnGroups, each reset for this run.");
        return groups;
    }

    // Executes build spawn queue operation.
    // Validates input parameters against null or empty values.
    private List<DungeonMonsterSpawnData> BuildSpawnQueue(MonsterSpawnResponse[] responses)
    {
        var aggregated = new Dictionary<int, DungeonMonsterSpawnData>();

        foreach (var response in responses)
        {
            if (response == null) continue;

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

    // Executes instantiate all operation.
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

            if (instance == null) continue;

            instance.name = $"{request.MonsterName}_{index}";

            var entity = instance.GetComponent<EnemyEntity>();
            if (entity != null)
            {
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

    // Executes spawn enemy object operation.
    private GameObject SpawnEnemyObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var photon = PhotonManager.Instance;
        bool online = photon != null && photon.IsDungeonSession;

        if (online && prefab.GetComponent<Fusion.NetworkObject>() != null)
        {
            if (!photon.IsHost)
            {
                Debug.Log($"[DungeonSpawner] Proxy — skipping local spawn of '{prefab.name}', waiting for replica.");
                return null;
            }

            Fusion.NetworkObject netObj = null;
            try
            {
                // Spawn through Fusion so state authority and replication are assigned consistently.
                netObj = photon.Runner.Spawn(prefab, position, rotation);
            }
            catch (System.Exception ex)
            {
                // Spawn through Fusion so state authority and replication are assigned consistently.
                Debug.LogError($"[DungeonSpawner] Runner.Spawn THREW for '{prefab.name}': {ex.Message}");
                return null;
            }

            if (netObj == null)
            {
                // Spawn through Fusion so state authority and replication are assigned consistently.
                Debug.LogError($"[DungeonSpawner] Runner.Spawn returned NULL for '{prefab.name}' " +
                               $"(IsHost={photon.IsHost}, runner running={photon.IsConnected}). " +
                               "Enemy will be missing on every client.");
                return null;
            }

            var dungeonScene = SceneManager.GetSceneByName(WorldState.CurrentMapName);
            if (dungeonScene.IsValid() && dungeonScene.isLoaded)
                SceneManager.MoveGameObjectToScene(netObj.gameObject, dungeonScene);

            var agent = netObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
                agent.enabled = true;

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


    // Executes spawn boss operation.
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


    // Executes is boss type operation.
    // Validates input parameters against null or empty values.
    private static bool IsBossType(MonsterSpawnResponse response)
    {
        if (!string.IsNullOrEmpty(response.MonsterType) &&
            response.MonsterType.Equals("Boss", StringComparison.OrdinalIgnoreCase))
            return true;

        string name = (response.MonsterName ?? string.Empty).ToLower();
        return name.Contains("boss") || name.Contains("ogre");
    }

    // Executes resolve prefab operation.
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
