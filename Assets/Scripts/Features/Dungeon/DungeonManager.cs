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
    public void StartDungeon(int configId, string dungeonSceneName, int cost, string dungeonName)
    {
        CurrentDungeonConfigId = configId;
        CurrentDungeonCost = cost;
        CurrentDungeonName = dungeonName;
        EnemiesKilledCount = 0;

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
        DungeonApi.Instance.Enter(configId,
            onSuccess: response =>
            {
                if (response.Success && response.Data != null)
                {
                    CurrentSessionId = response.Data.DungeonSessionId;
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

        // Teleport player to the SceneTransitionGoblinMine position
        GameObject targetSpawnPoint = GameObject.Find("SceneTransitionGoblinMine");
        if (targetSpawnPoint == null)
        {
            var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in allTransforms)
            {
                if (t != null && t.gameObject.name == "SceneTransitionGoblinMine" && t.gameObject.scene.name == dungeonSceneName)
                {
                    targetSpawnPoint = t.gameObject;
                    break;
                }
            }
        }

        if (targetSpawnPoint != null)
        {
            Vector3 spawnPos = targetSpawnPoint.transform.position;
            Debug.Log($"[DungeonManager] Found SceneTransitionGoblinMine at {spawnPos}. Teleporting player.");
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
            Debug.LogWarning("[DungeonManager] SceneTransitionGoblinMine spawn point not found in scene!");
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

        Debug.Log($"[DungeonManager] Dungeon scene loaded: {scene.name}. Registering enemies...");
        
        // Find all enemies in the loaded scene
        StartCoroutine(RegisterEnemiesDelayed());
    }

    private IEnumerator RegisterEnemiesDelayed()
    {
        // Wait briefly for all spawners/objects to initialize
        yield return new WaitForSeconds(0.5f);

        var enemies = FindObjectsByType<EnemyEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Debug.Log($"[DungeonManager] Found {enemies.Length} enemies in scene.");

        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.OnDeath -= HandleEnemyDeath;
                enemy.OnDeath += HandleEnemyDeath;
            }
        }
    }

    private void HandleEnemyDeath(object sender, EventArgs e)
    {
        if (sender is EnemyEntity enemy)
        {
            enemy.OnDeath -= HandleEnemyDeath;
            
            // Check if this enemy is the Boss (Ogre)
            bool isBoss = enemy.gameObject.name.ToLower().Contains("ogre") || 
                          enemy.gameObject.name.ToLower().Contains("boss") ||
                          enemy.name.ToLower().Contains("ogre");

            UpdateMonsterKill(isBoss);
        }
    }

    public void UpdateMonsterKill(bool isBoss)
    {
        EnemiesKilledCount++;
        int percentage = isBoss ? 100 : Mathf.Min(99, EnemiesKilledCount * 10);

        var request = new UpdateDungeonProgressRequest
        {
            MonstersKilled = EnemiesKilledCount,
            BossKilled = isBoss,
            CompletionPercentage = percentage
        };

        DungeonApi.Instance.UpdateProgress(CurrentSessionId, request,
            onSuccess: response =>
            {
                Debug.Log($"[DungeonManager] Progress updated: Killed={EnemiesKilledCount}, Boss={isBoss}");
                if (isBoss)
                {
                    CompleteDungeon();
                }
            },
            onError: error =>
            {
                Debug.LogWarning($"[DungeonManager] UpdateProgress failed: {error.Message}");
                if (isBoss)
                {
                    CompleteDungeon();
                }
            }
        );
    }

    private void CompleteDungeon()
    {
        DungeonApi.Instance.Complete(CurrentSessionId,
            onSuccess: response =>
            {
                Debug.Log("[DungeonManager] Dungeon completed on backend. Spawning chest...");
                SpawnFinalChest();
            },
            onError: error =>
            {
                Debug.LogWarning($"[DungeonManager] Complete API failed: {error.Message}. Spawning chest anyway.");
                SpawnFinalChest();
            }
        );
    }

    private void SpawnFinalChest()
    {
        // Try to find the boss death position
        Vector3 spawnPos = Vector3.zero;
        var boss = GameObject.Find("Ogre") ?? GameObject.Find("Boss");
        if (boss != null)
        {
            spawnPos = boss.transform.position;
        }
        else
        {
            var player = FindPlayerInstance();
            if (player != null)
            {
                spawnPos = player.transform.position + Vector3.right * 2f;
            }
        }

        // Look for any existing chests in the scene and activate them, or move them
        var chests = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool activatedChest = false;

        foreach (var t in chests)
        {
            if (t != null && (t.name == "Chest" || t.name == "Chest (1)" || t.name == "Chest (2)"))
            {
                t.gameObject.SetActive(true);
                t.position = spawnPos;
                
                // Add DungeonChest component if not present
                if (t.GetComponent<DungeonChest>() == null)
                {
                    t.gameObject.AddComponent<DungeonChest>();
                }
                
                activatedChest = true;
                Debug.Log($"[DungeonManager] Activated existing chest: {t.name} at {spawnPos}");
            }
        }

        // If no chest was found/activated in scene, instantiate a default one from Resources
        if (!activatedChest)
        {
            var chestPrefab = Resources.Load<GameObject>("Prefabs/Chest") ?? 
                              Resources.Load<GameObject>("Chest") ??
                              Resources.Load<GameObject>("PixelWorld/Prefabs/Objects/BoxesChests/Chest_1");

            if (chestPrefab != null)
            {
                var chestObj = Instantiate(chestPrefab, spawnPos, Quaternion.identity);
                chestObj.name = "DungeonChest";
                chestObj.AddComponent<DungeonChest>();
                Debug.Log($"[DungeonManager] Spawned chest prefab at {spawnPos}");
            }
            else
            {
                // Fallback: Create a primitive GameObject chest
                var chestObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chestObj.name = "DungeonChest";
                chestObj.transform.position = spawnPos;
                chestObj.AddComponent<DungeonChest>();
                Debug.LogWarning("[DungeonManager] Prefab not found. Created a fallback cube chest.");
            }
        }
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
