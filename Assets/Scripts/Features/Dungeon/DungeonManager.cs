using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

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

    // Saved position in world map to return to
    public string PreviousMapSceneName { get; private set; } = "AbandonedCastle";
    public Vector3 PreviousPlayerPosition { get; private set; } = Vector3.zero;

    // ── Per-run enemy tracking (normal monsters and boss are tracked separately) ──
    private readonly List<EnemyEntity> _normalEnemies = new();
    private readonly List<EnemyEntity> _bossEnemies   = new();
    private bool bossKilled = false;
    private Vector3 _bossDeathPosition = Vector3.zero;

    private enum DungeonPhase { Normal, BossSpawning, Boss, Complete }
    private DungeonPhase _currentPhase = DungeonPhase.Normal;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private GameObject FindPlayerInstance()
    {
        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null)
        {
            return pm.gameObject;
        }

        return GameObject.FindWithTag("Player") ?? 
               GameObject.Find("Knight") ?? 
               GameObject.Find("Mage") ?? 
               GameObject.Find("Archer") ?? 
               GameObject.Find("Knight(Clone)") ?? 
               GameObject.Find("Mage(Clone)") ?? 
               GameObject.Find("Archer(Clone)");
    }
    public void StartDungeon(int configId, string dungeonSceneName, int cost, string dungeonName, List<string> partyMembers = null)
    {
        CurrentDungeonConfigId = configId;
        CurrentDungeonCost = cost;
        CurrentDungeonName = dungeonName;
        EnemiesKilledCount = 0;
        bossKilled = false;
        _currentPhase = DungeonPhase.Normal;
        _normalEnemies.Clear();
        _bossEnemies.Clear();
        _bossDeathPosition = Vector3.zero;

        // Save current map state to return later
        PreviousMapSceneName = WorldState.CurrentMapName;
        
        // Find player position in the scene
        var player = FindPlayerInstance();
        if (player != null)
        {
            PreviousPlayerPosition = player.transform.position;
        }
        else
        {
            PreviousPlayerPosition = Vector3.zero;
        }

        // Call Enter API
        DungeonApi.Instance.Enter(configId, partyMembers ?? new List<string>(),
            onSuccess: response =>
            {
                if (response != null)
                {
                    CurrentSessionId = response.DungeonSessionId;
                    IsInDungeon = true;
                    Debug.Log($"[DungeonManager] Session created: {CurrentSessionId}");
                    
                    // Transition to target scene
                    StartCoroutine(TransitionToDungeon(dungeonSceneName));
                }
                else
                {
                    Debug.LogWarning("[DungeonManager] Enter API succeeded but no session data returned. Proceeding anyway for testing.");
                    CurrentSessionId = -1; // Dummy session ID
                    IsInDungeon = true;
                    StartCoroutine(TransitionToDungeon(dungeonSceneName));
                }
            },
            onError: error =>
            {
                Debug.LogWarning($"[DungeonManager] Enter API failed: {error.Message}. Proceeding to dungeon anyway for testing.");
                CurrentSessionId = -1; // Dummy session ID
                IsInDungeon = true;
                StartCoroutine(TransitionToDungeon(dungeonSceneName));
            }
        );
    }

    private IEnumerator TransitionToDungeon(string dungeonSceneName)
    {
        // Find player first before unloading
        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainScene = SceneManager.GetSceneByName("Main");
            if (mainScene.IsValid() && mainScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(player, mainScene);
                Debug.Log("[DungeonManager] Moved player to Main scene defensively.");
            }
        }

        // Unload any active map scenes defensively (excluding "Main" and the target dungeon scene)
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "Main" && s.name != dungeonSceneName && s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // Set LastPosition to zero so player spawner falls back to scene spawnPoint
        WorldState.LastPosition = Vector3.zero;
        WorldState.CurrentMapName = dungeonSceneName;

        // Load dungeon scene additively
        yield return SceneManager.LoadSceneAsync(dungeonSceneName, LoadSceneMode.Additive);

        // Move player into the dungeon scene
        if (player != null)
        {
            var dungeonScene = SceneManager.GetSceneByName(dungeonSceneName);
            if (dungeonScene.IsValid() && dungeonScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(player, dungeonScene);
                Debug.Log($"[DungeonManager] Moved player into dungeon scene: {dungeonSceneName}");
            }
        }
        else
        {
            // If player was null, try to find it now (in case PlayerSpawner in dungeon spawned a new one)
            player = FindPlayerInstance();
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
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.position = spawnPos;
                }
                player.transform.position = spawnPos;
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

        Debug.Log($"[DungeonManager] Entered dungeon scene: {dungeonSceneName}");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsInDungeon || scene.name != WorldState.CurrentMapName)
            return;

        Debug.Log($"[DungeonManager] Dungeon scene loaded: {scene.name}. Starting spawn + registration...");

        // Try to use DungeonSpawner for data-driven spawning.
        // Falls back to scanning existing scene enemies if no spawner is present.
        StartCoroutine(SpawnAndRegisterEnemies(scene.name));
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

            _normalEnemies.Clear();

            if (spawnedEnemies != null)
            {
                foreach (var enemy in spawnedEnemies)
                {
                    if (enemy == null) continue;
                    _normalEnemies.Add(enemy);
                    enemy.OnDeath -= HandleNormalEnemyDeath;
                    enemy.OnDeath += HandleNormalEnemyDeath;
                }
                Debug.Log($"[DungeonManager] Registered {_normalEnemies.Count} normal enemies.");
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

            _normalEnemies.Clear();
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                _normalEnemies.Add(enemy);
                enemy.OnDeath -= HandleNormalEnemyDeath;
                enemy.OnDeath += HandleNormalEnemyDeath;
            }
        }
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

        int remaining  = _normalEnemies.Count;
        int total      = EnemiesKilledCount + remaining;
        // Progress stays ≤ 49 % while normals are alive; hits 50 % when all are dead
        int percentage = remaining == 0 ? 50
                       : Mathf.Min(49, (EnemiesKilledCount * 50) / Mathf.Max(1, total));

        Debug.Log($"[DungeonManager] Normal enemy killed. Remaining: {remaining}. Progress: {percentage}%");

        // Fire-and-forget progress update
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

        if (remaining == 0 && _currentPhase == DungeonPhase.Normal)
            StartCoroutine(TriggerBossSequence());
    }

    /// <summary>
    /// Screen shake warning → 1-second pause → spawns boss at BossSpawn point.
    /// </summary>
    private IEnumerator TriggerBossSequence()
    {
        _currentPhase = DungeonPhase.BossSpawning;
        Debug.Log("[DungeonManager] All normals defeated. Starting boss sequence (shake → spawn).");

        // Screen shake to signal the incoming boss
        DungeonScreenShake.Shake(duration: 0.9f, magnitude: 0.28f);
        yield return new WaitForSeconds(1.2f);

        // Find DungeonSpawner and let it spawn the Boss (which was saved from the API call)
        if (DungeonSpawner.Instance == null)
        {
            Debug.LogWarning("[DungeonManager] DungeonSpawner not found. Skipping boss and completing dungeon.");
            yield return StartCoroutine(BossDeathSequence(GetFallbackChestPosition()));
            yield break;
        }

        EnemyEntity boss = DungeonSpawner.Instance.SpawnBoss();
        if (boss == null)
        {
            Debug.LogWarning("[DungeonManager] DungeonSpawner.SpawnBoss returned null. Completing dungeon without boss.");
            yield return StartCoroutine(BossDeathSequence(GetFallbackChestPosition()));
            yield break;
        }

        _bossEnemies.Clear();
        _bossEnemies.Add(boss);
        boss.OnDeath -= HandleBossEnemyDeath;
        boss.OnDeath += HandleBossEnemyDeath;

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
        bossKilled           = true;
        EnemiesKilledCount++;
        _bossDeathPosition   = boss.transform.position;
        _currentPhase        = DungeonPhase.Complete;

        Debug.Log($"[DungeonManager] Boss defeated at {_bossDeathPosition}. Starting completion sequence.");
        StartCoroutine(BossDeathSequence(_bossDeathPosition));
    }

    /// <summary>
    /// Reports 100% completion to the backend, waits 1.5 seconds for the boss death
    /// animation to finish, then spawns the reward chest with a drop-in animation.
    /// </summary>
    private IEnumerator BossDeathSequence(Vector3 chestPosition)
    {
        // Report final progress
        DungeonApi.Instance.UpdateProgress(
            CurrentSessionId,
            new UpdateDungeonProgressRequest
            {
                MonstersKilled       = EnemiesKilledCount,
                BossKilled           = bossKilled,
                CompletionPercentage = 100
            },
            _ => { },
            err => Debug.LogWarning($"[DungeonManager] Final UpdateProgress failed: {err.Message}")
        );

        // Mark session complete on backend
        bool completeDone = false;
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

        // Wait for boss death animation
        yield return new WaitForSeconds(1.5f);

        // Spawn the reward chest with drop-in animation
        SpawnFinalChestAtPosition(chestPosition);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CHEST SPAWNING
    // ═══════════════════════════════════════════════════════════════════════════

    private void SpawnFinalChestAtPosition(Vector3 targetPosition)
    {
        GameObject chestGO = null;

        // 1. Activate an existing (hidden) DarkChest/Chest in the scene
        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            string n = t.name;
            if (n == "DarkChest" || n.Contains("DarkChest") ||
                n == "Chest"     || n == "Chest (1)" || n == "Chest (2)")
            {
                // Position above target so the drop animation starts correctly
                t.position = targetPosition + Vector3.up * 6f;
                t.gameObject.SetActive(true);

                if (t.GetComponent<DungeonChest>() == null)
                    t.gameObject.AddComponent<DungeonChest>();

                chestGO = t.gameObject;
                Debug.Log($"[DungeonManager] Activating chest '{t.name}' with drop animation.");
                break;
            }
        }

        // 2. Instantiate from Resources
        if (chestGO == null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Chest")
                      ?? Resources.Load<GameObject>("Chest")
                      ?? Resources.Load<GameObject>("DarkChest")
                      ?? Resources.Load<GameObject>("PixelWorld/Prefabs/Objects/BoxesChests/Chest_1");

            if (prefab != null)
            {
                chestGO = Instantiate(prefab, targetPosition + Vector3.up * 6f, Quaternion.identity);
                chestGO.name = "DungeonChest";
                if (chestGO.GetComponent<DungeonChest>() == null)
                    chestGO.AddComponent<DungeonChest>();
                Debug.Log("[DungeonManager] Spawned chest from prefab with drop animation.");
            }
        }

        // 3. Hard fallback: primitive cube (always visible)
        if (chestGO == null)
        {
            chestGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestGO.name = "DungeonChest";
            chestGO.transform.position = targetPosition + Vector3.up * 6f;
            chestGO.AddComponent<DungeonChest>();
            Debug.LogWarning("[DungeonManager] Chest prefab not found — created a fallback cube chest.");
        }

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
        IsInDungeon = false;
        StartCoroutine(TransitionToWorld());
    }

    private IEnumerator TransitionToWorld()
    {
        // Find player first before unloading
        var player = FindPlayerInstance();
        if (player != null)
        {
            var mainSceneObj = SceneManager.GetSceneByName("Main");
            if (mainSceneObj.IsValid() && mainSceneObj.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(player, mainSceneObj);
                Debug.Log("[DungeonManager] Moved player to Main scene defensively.");
            }
        }

        // Unload any active map scenes defensively (excluding "Main" and the target world scene)
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name != "Main" && s.name != PreviousMapSceneName && s.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(s);
            }
        }

        // Restore position and current map
        WorldState.LastPosition = PreviousPlayerPosition != Vector3.zero ? PreviousPlayerPosition : new Vector3(11.9f, 17.8f, 0f);
        WorldState.CurrentMapName = PreviousMapSceneName;

        // Load previous map
        yield return SceneManager.LoadSceneAsync(PreviousMapSceneName, LoadSceneMode.Additive);

        // Move player into the world scene
        if (player != null)
        {
            var worldScene = SceneManager.GetSceneByName(PreviousMapSceneName);
            if (worldScene.IsValid() && worldScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(player, worldScene);
                Debug.Log($"[DungeonManager] Moved player into world scene: {PreviousMapSceneName}");
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

        Debug.Log($"[DungeonManager] Returned to map: {PreviousMapSceneName} at {WorldState.LastPosition}");
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
