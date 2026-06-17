using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameBootstrap : MonoBehaviour
{
    private IEnumerator Start()
    {
        Debug.Log("=== GAME BOOTSTRAP START ===");

        yield return LoadWorldSession();
        yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Additive);

        if (string.IsNullOrWhiteSpace(WorldState.CurrentMapName))
            WorldState.CurrentMapName = "ElfForest";

        yield return SceneManager.LoadSceneAsync(WorldState.CurrentMapName, LoadSceneMode.Additive);

        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.IsValid())
            SceneManager.SetActiveScene(mainScene);

        Debug.Log("=== LOAD DONE ===");
        Destroy(gameObject);
    }

    private IEnumerator LoadWorldSession()
    {
        var done = false;

        if (ApiClient.Instance.HasToken())
        {
            AuthApi.Instance.GetMe(
                _ => done = true,
                error =>
                {
                    Debug.LogWarning($"[GameBootstrap] GetMe failed, using local world session. {error.Message}");
                    LoadLocalWorldSession();
                    done = true;
                }
            );

            yield return new WaitUntil(() => done);
            yield break;
        }

        LoadLocalWorldSession();
        yield return null;
    }

    private static void LoadLocalWorldSession()
    {
        var mapName = PlayerPrefs.GetString(ApiConfig.LastMapNameKey, "ElfForest");
        var x = PlayerPrefs.GetFloat(ApiConfig.PositionXKey, 0f);
        var y = PlayerPrefs.GetFloat(ApiConfig.PositionYKey, 0f);

        WorldState.CurrentMapName = string.IsNullOrWhiteSpace(mapName) ? "ElfForest" : mapName.Trim();
        WorldState.LastPosition = new Vector3(x, y, 0f);
    }
}
