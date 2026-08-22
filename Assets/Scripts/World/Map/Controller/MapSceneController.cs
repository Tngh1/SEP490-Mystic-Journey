using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.SceneManagement;

// Executes mono behaviour operation.
public class MapSceneController : MonoBehaviour
{
    [SerializeField]
    private List<MapSceneConfig> mapConfigs;

    // Executes is travel blocked operation.
    public bool IsTravelBlocked
    {
        get { return IsTravelBlockedNow; }
    }

    // Executes is travel blocked now operation.
    public static bool IsTravelBlockedNow
    {
        get { return DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon; }
    }

    // Executes enter map operation.
    public void EnterMap(MapData mapData, bool useCache = true, Vector3? specificSpawnPos = null)
    {
        if (IsTravelBlocked)
        {
            Debug.Log("[MapSceneController] Travel blocked: player is inside a dungeon.");
            return;
        }

        MapSceneConfig config =
            mapConfigs.Find(
                x => x != null && x.mapData == mapData);

        if (config == null)
        {
            Debug.LogError(
                $"Config not found for {mapData.mapName}");
            return;
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(
            ChangeMap(config.sceneName, useCache, specificSpawnPos));
    }

    // Process the supplied values: normalizes or validates the text before returning the derived result.
    private IEnumerator ChangeMap(
        string targetScene, bool useCache = true, Vector3? specificSpawnPos = null)
    {
        if (targetScene ==
            WorldState.CurrentMapName)
        {
            yield break;
        }

        var positionSync = FindPlayer()?.GetComponent<PlayerWorldPositionSync>();
        positionSync?.BeginMapTransition();

        yield return LoadingScreen.Show();

        var previousScene = WorldState.CurrentMapName;
        if (!string.IsNullOrWhiteSpace(previousScene) && SceneManager.GetSceneByName(previousScene).isLoaded)
        {
            MovePlayerToMainScene();

            yield return SceneManager
                .UnloadSceneAsync(previousScene);
        }

        LoadingProgress.Report(0.3f, "Loading map...");
        var loadOp = SceneManager
            .LoadSceneAsync(
                targetScene,
                LoadSceneMode.Additive);
        while (loadOp != null && !loadOp.isDone)
        {
            LoadingProgress.Report(Mathf.Lerp(0.3f, 0.85f, loadOp.progress), "Loading map...");
            yield return null;
        }

        Vector3 spawnPosition = Vector3.zero;
        bool positionFound = false;

        if (specificSpawnPos.HasValue)
        {
            spawnPosition = specificSpawnPos.Value;
            positionFound = true;
            Debug.Log($"[MapSceneController] Using specific spawn pos {spawnPosition}");
        }
        else if (useCache && MapPositionCache.TryGet(targetScene, out var cachedPos) && cachedPos != Vector3.zero)
        {
            spawnPosition = cachedPos;
            positionFound = true;
            Debug.Log($"[MapSceneController] Returning to {targetScene} at last pos {spawnPosition}");
        }
        else
        {
            var spawners = Object.FindObjectsByType<PlayerSpawner>(FindObjectsSortMode.None);
            foreach (var spawner in spawners)
            {
                if (spawner.gameObject.scene.name == targetScene && spawner.SpawnPoint != null)
                {
                    spawnPosition = spawner.SpawnPoint.position;
                    positionFound = true;
                    Debug.Log($"[MapSceneController] Found PlayerSpawner in {targetScene}. Spawn: {spawnPosition}");
                    break;
                }
            }

            if (!positionFound)
            {
                var spawnMarker = GameObject.FindGameObjectWithTag("PlayerSpawn");
                if (spawnMarker == null) spawnMarker = GameObject.Find("PlayerSpawn");
                if (spawnMarker == null) spawnMarker = GameObject.Find("PlayerSpawnRuntime");

                spawnPosition = spawnMarker != null ? spawnMarker.transform.position : Vector3.zero;

                if (spawnMarker != null)
                    Debug.Log($"[MapSceneController] First visit {targetScene} at PlayerSpawn {spawnPosition}");
                else
                    Debug.LogWarning($"[MapSceneController] No 'PlayerSpawn' in {targetScene}, using default.");
            }
        }

        WorldState.CurrentMapName = targetScene;
        WorldState.LastPosition = spawnPosition;
        WorldState.SaveToPlayerPrefs();
        MapPositionCache.Save(targetScene, spawnPosition);

        var player = FindPlayer();
        if (player == null)
            Debug.LogWarning($"[MapSceneController] No player found after loading {targetScene}.");

        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = spawnPosition;
            }
            player.transform.position = spawnPosition;
            Physics2D.SyncTransforms();

            var vcam = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (vcam != null)
            {
                vcam.Follow = player.transform;
                var composer = vcam.GetComponent<Unity.Cinemachine.CinemachinePositionComposer>();
                if (composer != null)
                {
                    composer.Damping = new Vector3(0.05f, 0.05f, 0.05f);
                }
            }

            var minimapCam = Object.FindFirstObjectByType<MinimapCameraController>();
            if (minimapCam != null)
            {
                minimapCam.InitializeMinimap(player.transform);
            }
        }

        if (ApiClient.Instance.HasToken())
        {
            float pendingSaveDeadline = Time.realtimeSinceStartup + 8f;
            while (positionSync != null
                   && positionSync.HasPendingSave
                   && Time.realtimeSinceStartup < pendingSaveDeadline)
            {
                yield return null;
            }

            if (positionSync != null && positionSync.HasPendingSave)
                Debug.LogWarning("[MapSceneController] Previous position save timed out; committing the destination map.");


            bool saveCompleted = false;
            bool saveSucceeded = false;
            WorldApi.Instance.UpdatePosition(
                targetScene,
                spawnPosition,
                _ =>
                {
                    Debug.Log($"[MapSceneController] Saved: {targetScene} @ {spawnPosition}");
                    saveSucceeded = true;
                    saveCompleted = true;
                },
                error =>
                {
                    Debug.LogWarning($"[MapSceneController] Save failed: {error.Message}");
                    saveCompleted = true;
                }
            );

            float destinationSaveDeadline = Time.realtimeSinceStartup + 12f;
            while (!saveCompleted && Time.realtimeSinceStartup < destinationSaveDeadline)
                yield return null;

            if (!saveCompleted)
                Debug.LogWarning($"[MapSceneController] Saving {targetScene} timed out; releasing loading screen.");


            positionSync?.CompleteMapTransition(spawnPosition);
            if (saveSucceeded)
            {
                yield return RefreshDestinationData(targetScene);
            }
            else
            {
                WorldRuntimeEvents.RaiseMapChanged(targetScene);
                WorldRuntimeEvents.RaiseQuestsChanged();
            }
        }
        else
        {
            positionSync?.CompleteMapTransition(spawnPosition);
            WorldRuntimeEvents.RaiseMapChanged(targetScene);
            WorldRuntimeEvents.RaiseQuestsChanged();
        }

        yield return LoadingScreen.Hide();
    }

    private IEnumerator RefreshDestinationData(string targetScene)
    {
        LoadingProgress.Report(0.9f, "Refreshing quests and NPCs...");

        bool questRefreshCompleted = QuestUIManager.Instance == null;
        var questManager = QuestUIManager.Instance;
        if (questManager != null)
        {
            questManager.LoadMyQuests(
                onSuccess: () => questRefreshCompleted = true,
                onError: error =>
                {
                    Debug.LogWarning($"[MapSceneController] Quest refresh for {targetScene} failed: {error}");
                    questRefreshCompleted = true;
                });
        }

        bool npcRefreshCompleted = true;
        WorldNpcSpawnerRuntime targetNpcSpawner = null;
        var npcSpawners = Object.FindObjectsByType<WorldNpcSpawnerRuntime>(FindObjectsSortMode.None);
        foreach (var candidate in npcSpawners)
        {
            if (candidate != null && candidate.gameObject.scene.name == targetScene)
            {
                targetNpcSpawner = candidate;
                break;
            }
        }

        if (targetNpcSpawner != null)
        {
            npcRefreshCompleted = false;
            targetNpcSpawner.SpawnNpcsForCurrentMap(success =>
            {
                if (!success)
                    Debug.LogWarning($"[MapSceneController] NPC refresh for {targetScene} did not complete successfully.");
                npcRefreshCompleted = true;
            });
        }

        var destinationScene = SceneManager.GetSceneByName(targetScene);
        if (destinationScene.IsValid() && destinationScene.isLoaded)
            WorldSceneInteractableBootstrap.EnsureForScene(destinationScene);

        float refreshDeadline = Time.realtimeSinceStartup + 12f;
        while ((!questRefreshCompleted || !npcRefreshCompleted) && Time.realtimeSinceStartup < refreshDeadline)
            yield return null;

        if (!questRefreshCompleted || !npcRefreshCompleted)
            Debug.LogWarning($"[MapSceneController] Data refresh for {targetScene} timed out; releasing loading screen.");

        WorldRuntimeEvents.RaiseMapChanged(targetScene);
        WorldRuntimeEvents.RaiseQuestsChanged();
    }

    // Executes find player operation.
    private static GameObject FindPlayer()
    {
        GameObject found = NetworkPlayer.Local != null ? NetworkPlayer.Local.gameObject : null;

        found ??= GameObject.FindGameObjectWithTag("Player");

        if (found == null)
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null) found = pm.gameObject;
        }

        return found != null ? found.transform.root.gameObject : null;
    }

    // Executes move player to main scene operation.
    private static void MovePlayerToMainScene()
    {
        var player = FindPlayer();
        if (player == null) return;

        var mainScene = SceneManager.GetSceneByName("Main");
        if (!mainScene.IsValid() || !mainScene.isLoaded || player.scene == mainScene) return;

        try
        {
            SceneManager.MoveGameObjectToScene(player, mainScene);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MapSceneController] Could not move '{player.name}' to Main: {ex.Message}");
        }
    }
}
