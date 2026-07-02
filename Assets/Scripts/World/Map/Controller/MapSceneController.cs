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

    public void EnterMap(MapData mapData)
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
            ChangeMap(config.sceneName));
    }

    private IEnumerator ChangeMap(
        string targetScene)
    {
        if (targetScene ==
            WorldState.CurrentMapName)
        {
            yield break;
        }

        yield return SceneManager
            .UnloadSceneAsync(
                WorldState.CurrentMapName);

        yield return SceneManager
            .LoadSceneAsync(
                targetScene,
                LoadSceneMode.Additive);

        // Ưu tiên 1: đã từng vào map này trong session → spawn vị trí cuối
        // Ưu tiên 2: lần đầu vào → tìm PlayerSpawn tag (NPC mốc đầu do team đặt)
        // Ưu tiên 3: không có gì → Vector3.zero (giữ default scene position)
        Vector3 spawnPosition;
        if (MapPositionCache.TryGet(targetScene, out var cachedPos))
        {
            spawnPosition = cachedPos;
            Debug.Log($"[MapSceneController] Returning to {targetScene} → last pos {spawnPosition}");
        }
        else
        {
            var spawnMarker = GameObject.FindGameObjectWithTag("PlayerSpawn");
            spawnPosition = spawnMarker != null ? spawnMarker.transform.position : Vector3.zero;

            if (spawnMarker != null)
                Debug.Log($"[MapSceneController] First visit {targetScene} → PlayerSpawn at {spawnPosition}");
            else
                Debug.LogWarning($"[MapSceneController] No 'PlayerSpawn' in {targetScene}, using default.");
        }

        WorldState.CurrentMapName = targetScene;
        WorldState.LastPosition = spawnPosition;

        if (ApiClient.Instance.HasToken())
        {
            WorldApi.Instance.UpdatePosition(
                targetScene,
                spawnPosition,
                _ => Debug.Log($"[MapSceneController] Saved: {targetScene} @ {spawnPosition}"),
                error => Debug.LogWarning($"[MapSceneController] Save failed: {error.Message}")
            );
        }
    }
}
