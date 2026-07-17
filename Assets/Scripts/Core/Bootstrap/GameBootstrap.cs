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

        var bootstrapScene = gameObject.scene;
        if (transform.parent != null)
            transform.SetParent(null);

        DontDestroyOnLoad(gameObject);

        yield return LoadWorldSession();

        if (string.IsNullOrWhiteSpace(WorldState.CurrentMapName) || WorldState.CurrentMapName == "AbandonedMines")
        {
            WorldState.CurrentMapName = "ElfForest";
            WorldState.LastPosition = new Vector3(11.9f, 17.8f, 0f);
        }

        yield return EnsureSceneLoaded("Main");
        yield return EnsureSceneLoaded(WorldState.CurrentMapName);

        var mainScene = SceneManager.GetSceneByName("Main");
        if (mainScene.IsValid())
            SceneManager.SetActiveScene(mainScene);

        DisableDuplicateAudioListeners();

        // Apply saved settings (volume, graphics, etc.) when game starts
        SettingsService.Instance.Load();

        if (bootstrapScene.IsValid() && bootstrapScene.isLoaded && bootstrapScene.name != "Main" && bootstrapScene.name != WorldState.CurrentMapName)
            yield return SceneManager.UnloadSceneAsync(bootstrapScene);

        Debug.Log($"=== LOAD DONE | UI=Main | Map={WorldState.CurrentMapName} ===");

        // Join the shared social lobby room (presence + party invites) once we are in
        // Main and know who we are. Fire-and-forget: JoinSocialLobbyAsync swallows its
        // own failures so a Photon outage never blocks the Main scene. Only attempt it
        // for a logged-in player with a real profile id.
        if (ApiClient.Instance.HasToken() && WorldState.PlayerProfileId > 0 && PhotonManager.Instance != null)
        {
            _ = PhotonManager.Instance.JoinSocialLobbyAsync();
            // Listen for incoming party invites while in Main.
            PartyInvitePopup.EnsureExists();
        }

        Destroy(gameObject);
    }

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

    private static IEnumerator EnsureSceneLoaded(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.IsValid() && scene.isLoaded)
            yield break;

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    }

    private IEnumerator LoadWorldSession()
    {
        var done = false;

        if (ApiClient.Instance.HasToken())
        {
            AuthApi.Instance.GetMe(
                _ =>
                {
                    WorldApi.Instance.GetState(
                        state =>
                        {
                            if (state != null)
                                WorldState.PlayerProfileId = state.PlayerProfileId;

                            LoadLocalWorldSession();
                            done = true;
                        },
                        error =>
                        {
                            Debug.LogWarning($"[GameBootstrap] GetWorldState failed, using local world session. {error.Message}");
                            LoadLocalWorldSession();
                            done = true;
                        }
                    );
                },
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
        var level = PlayerPrefs.GetInt(ApiConfig.PlayerLevelKey, 1);
        var playerClass = PlayerPrefs.GetString(ApiConfig.PlayerClassKey, "Knight");
        var profileId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);

        WorldState.CurrentMapName = string.IsNullOrWhiteSpace(mapName) ? "ElfForest" : mapName.Trim();
        WorldState.LastPosition = new Vector3(x, y, 0f);
        WorldState.PlayerLevel = Mathf.Max(1, level);
        WorldState.PlayerClass = string.IsNullOrWhiteSpace(playerClass) ? "Knight" : playerClass.Trim();
        WorldState.PlayerProfileId = profileId;
    }
}
