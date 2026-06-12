using System.Collections;
using System.Collections.Generic;
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
                x => x.mapData == mapData);

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

        WorldState.CurrentMapName =
            targetScene;
    }
}