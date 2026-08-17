using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Initializes a new default instance of the LoadingScreen class.
public static class LoadingScreen
{
    public const string SceneName = "Loading";

    private const float MinSeconds = 0.35f;

    private static float _shownAt;
    private static AsyncOperation _loadOperation;
    private static AsyncOperation _unloadOperation;


    // Executes show operation.
    public static IEnumerator Show(string status = "Loading map...")
    {
        LoadingProgress.Reset();
        LoadingProgress.Report(0.05f, status);
        _shownAt = Time.unscaledTime;

        if (_unloadOperation != null)
        {
            var pendingUnload = _unloadOperation;
            yield return pendingUnload;
            if (_unloadOperation == pendingUnload)
                _unloadOperation = null;
        }

        var scene = SceneManager.GetSceneByName(SceneName);
        if (scene.IsValid() && scene.isLoaded)
            yield break;

        if (_loadOperation == null)
            _loadOperation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);

        var pendingLoad = _loadOperation;
        if (pendingLoad != null)
            yield return pendingLoad;

        if (_loadOperation == pendingLoad)
            _loadOperation = null;
    }

    // Executes hide operation.
    public static IEnumerator Hide()
    {
        LoadingProgress.Report(1f, "Ready");

        if (_loadOperation != null)
        {
            var pendingLoad = _loadOperation;
            yield return pendingLoad;
            if (_loadOperation == pendingLoad)
                _loadOperation = null;
        }

        float elapsed = Time.unscaledTime - _shownAt;
        if (elapsed < MinSeconds)
            yield return new WaitForSecondsRealtime(MinSeconds - elapsed);

        if (_unloadOperation != null)
        {
            var pendingUnload = _unloadOperation;
            yield return pendingUnload;
            if (_unloadOperation == pendingUnload)
                _unloadOperation = null;
            yield break;
        }

        var scene = SceneManager.GetSceneByName(SceneName);
        if (scene.IsValid() && scene.isLoaded)
        {
            _unloadOperation = SceneManager.UnloadSceneAsync(scene);
            var pendingUnload = _unloadOperation;
            if (pendingUnload != null)
                yield return pendingUnload;

            if (_unloadOperation == pendingUnload)
                _unloadOperation = null;
        }
    }
}
