using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.SceneManagement;

// Executes mono behaviour operation.
// Validates input parameters against null or empty values.
public class GameBootstrap : MonoBehaviour
{
    private const string LoadingSceneName = "Loading";

    // Performs startup initialization for GameBootstrap on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private IEnumerator Start()
    {
        Debug.Log("=== GAME BOOTSTRAP START ===");

        LoadingProgress.Reset();
        LoadingProgress.Report(0.05f, "Connecting...");

        var bootstrapScene = gameObject.scene;
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        yield return EnsureSceneLoaded(LoadingSceneName, 0.05f, 0.05f, "Connecting...");

        yield return LoadWorldSession();

        if (string.IsNullOrWhiteSpace(WorldState.CurrentMapName) || WorldState.CurrentMapName == "AbandonedMines")
        {
            WorldState.CurrentMapName = "ElfForest";
            WorldState.LastPosition = new Vector3(11.9f, 17.8f, 0f);
        }

        yield return EnsureSceneLoaded("Main", 0.5f, 0.75f, "Loading interface...");
        yield return EnsureSceneLoaded(WorldState.CurrentMapName, 0.75f, 0.98f, "Loading map...");
        LoadingProgress.Report(1f, "Ready");

        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.IsValid())
            SceneManager.SetActiveScene(mainScene);

        DisableDuplicateAudioListeners();

        SettingsService.Instance.Load();

        var loadingScene = SceneManager.GetSceneByName(LoadingSceneName);
        if (loadingScene.IsValid() && loadingScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(loadingScene);

        if (bootstrapScene.IsValid() && bootstrapScene.isLoaded && bootstrapScene.name != "Main" && bootstrapScene.name != WorldState.CurrentMapName)
            yield return SceneManager.UnloadSceneAsync(bootstrapScene);

        Debug.Log($"=== LOAD DONE | UI=Main | Map={WorldState.CurrentMapName} ===");

        if (ApiClient.Instance.HasToken() && PhotonManager.Instance != null)
        {
            _ = PhotonManager.Instance.JoinSocialLobbyAsync();
            PartyInvitePopup.EnsureExists();
        }

        Destroy(gameObject);
    }

    // Executes disable duplicate audio listeners operation.
    private static void DisableDuplicateAudioListeners()
    {
        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (listeners.Length <= 1)
            return;

        AudioListener keep = null;
        foreach (var listener in listeners)
        {
            if (listener != null && listener.gameObject.scene.name == WorldState.CurrentMapName)
            {
                keep = listener;
                break;
            }
        }

        if (keep == null)
            keep = listeners[0];

        foreach (var listener in listeners)
        {
            if (listener != null && listener != keep)
                listener.enabled = false;
        }
    }

    // Executes ensure scene loaded operation.
    private static IEnumerator EnsureSceneLoaded(string sceneName, float progressFrom, float progressTo, string status)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            LoadingProgress.Report(progressTo, status);
            yield break;
        }

        LoadingProgress.Report(progressFrom, status);
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (op != null && !op.isDone)
        {
            LoadingProgress.Report(Mathf.Lerp(progressFrom, progressTo, op.progress), status);
            yield return null;
        }
        LoadingProgress.Report(progressTo, status);
    }

    // Executes load world session operation.
    private IEnumerator LoadWorldSession()
    {
        var done = false;

        if (ApiClient.Instance.HasToken())
        {
            LoadingProgress.Report(0.15f, "Authenticating...");
            AuthApi.Instance.GetMe(
                _ =>
                {
                    LoadLocalWorldSession();
                    LoadingProgress.Report(0.5f, "Loading world data...");
                    done = true;
                },
                error =>
                {
                    Debug.LogWarning($"[GameBootstrap] GetMe failed, using local world session. {error.Message}");
                    LoadLocalWorldSession();
                    LoadingProgress.Report(0.5f, "Loading world data...");
                    done = true;
                }
            );

            yield return new WaitUntil(() => done);
            yield break;
        }

        LoadLocalWorldSession();
        LoadingProgress.Report(0.5f, "Loading world data...");
        yield return null;
    }

    // Executes load local world session operation.
    private static void LoadLocalWorldSession()
    {
        var mapName = PlayerPrefs.GetString(ApiConfig.LastMapNameKey, "ElfForest");
        var x = PlayerPrefs.GetFloat(ApiConfig.PositionXKey, 0f);
        var y = PlayerPrefs.GetFloat(ApiConfig.PositionYKey, 0f);
        var level = PlayerPrefs.GetInt(ApiConfig.PlayerLevelKey, 1);
        // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
        var playerClass = PlayerPrefs.GetString(ApiConfig.PlayerClassKey, "Knight");
        var profileId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);
        var equippedSkinId = PlayerPrefs.GetInt("mj_equipped_skin_id", 0);

        WorldState.CurrentMapName = string.IsNullOrWhiteSpace(mapName) ? "ElfForest" : mapName.Trim();
        WorldState.LastPosition = new Vector3(x, y, 0f);
        WorldState.PlayerLevel = Mathf.Max(1, level);
        WorldState.PlayerClass = string.IsNullOrWhiteSpace(playerClass) ? "Knight" : playerClass.Trim();
        WorldState.PlayerProfileId = profileId;
        WorldState.EquippedSkinId = Mathf.Max(0, equippedSkinId);
    }
}
