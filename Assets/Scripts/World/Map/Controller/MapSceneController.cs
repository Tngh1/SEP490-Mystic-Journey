using System.Collections;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MapSceneController : MonoBehaviour
{
    [SerializeField]
    private List<MapSceneConfig> mapConfigs;

    public void EnterMap(MapData mapData, bool useCache = true, Vector3? specificSpawnPos = null)
    {
        MapSceneConfig config =
            mapConfigs.Find(
                x => x != null && x.mapData == mapData);

        if (config == null)
        {
            Debug.LogError(
                $"Config not found for {mapData.mapName}");
            return;
        }

        StartCoroutine(
            ChangeMap(config.sceneName, useCache, specificSpawnPos));
    }

    private IEnumerator ChangeMap(
        string targetScene, bool useCache = true, Vector3? specificSpawnPos = null)
    {
        if (targetScene ==
            WorldState.CurrentMapName)
        {
            yield break;
        }

        var previousScene = WorldState.CurrentMapName;
        if (!string.IsNullOrWhiteSpace(previousScene) && SceneManager.GetSceneByName(previousScene).isLoaded)
        {
            yield return SceneManager
                .UnloadSceneAsync(previousScene);
        }

        yield return SceneManager
            .LoadSceneAsync(
                targetScene,
                LoadSceneMode.Additive);

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
            // First, try to find a PlayerSpawner in the newly loaded scene
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

        WorldRuntimeEvents.RaiseMapChanged(targetScene);
        WorldRuntimeEvents.RaiseQuestsChanged();
        QuestManager.Instance?.LoadMyQuests();

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            var pm = Object.FindFirstObjectByType<PlayerMovement>();
            if (pm != null) player = pm.gameObject;
        }

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

            // Set camera follow target for the newly loaded map scene
            var vcam = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineCamera>();
            if (vcam != null)
            {
                vcam.Follow = player.transform;
            }

            // Re-initialize minimap for the newly loaded map scene
            var minimapCam = Object.FindFirstObjectByType<MinimapCameraController>();
            if (minimapCam != null)
            {
                minimapCam.InitializeMinimap(player.transform);
            }
        }

        if (ApiClient.Instance.HasToken())
        {
            WorldApi.Instance.UpdatePosition(
                targetScene,
                spawnPosition,
                _ =>
                {
                    Debug.Log($"[MapSceneController] Saved: {targetScene} @ {spawnPosition}");
                    WorldRuntimeEvents.RaiseMapChanged(targetScene);
                    WorldRuntimeEvents.RaiseQuestsChanged();
                },
                error => Debug.LogWarning($"[MapSceneController] Save failed: {error.Message}")
            );
        }
    }
}
