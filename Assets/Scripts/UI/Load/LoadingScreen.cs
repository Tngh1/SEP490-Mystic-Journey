using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bật/tắt scene "Loading" cho các lần đổi scene lúc đang chơi (qua cổng map, vào/ra dungeon).
/// Dùng lại đúng scene + <see cref="BootstrapLoadingUI"/> mà GameBootstrap đã dùng, nên không có
/// UI riêng phải bảo trì; ở đây chỉ load additive rồi unload.
/// </summary>
public static class LoadingScreen
{
    /// <summary>
    /// Public để các vòng lặp "unload mọi scene lạ" (DungeonManager) biết mà chừa scene này ra —
    /// nếu không nó unload luôn màn hình loading đang che, lộ scene trống.
    /// </summary>
    public const string SceneName = "Loading";

    /// <summary>Scene nội bộ có thể load xong trong 1-2 frame; giữ tối thiểu để không bị nháy.</summary>
    private const float MinSeconds = 0.35f;

    private static float _shownAt;
    private static AsyncOperation _loadOperation;
    private static AsyncOperation _unloadOperation;


    public static IEnumerator Show(string status = "Loading map...")
    {
        LoadingProgress.Reset();
        LoadingProgress.Report(0.05f, status);
        _shownAt = Time.unscaledTime;

        // A new transition may start while the previous caller is still unloading the
        // shared loading scene. Wait before trying to load it again.
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

        // BoatVoyageSequence and MapSceneController can request the overlay in the
        // same frame. Share one AsyncOperation instead of loading the additive scene twice.
        if (_loadOperation == null)
            _loadOperation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);

        var pendingLoad = _loadOperation;
        if (pendingLoad != null)
            yield return pendingLoad;

        if (_loadOperation == pendingLoad)
            _loadOperation = null;
    }

    public static IEnumerator Hide()
    {
        LoadingProgress.Report(1f, "Ready");

        // Hide may be requested while a Show coroutine is still loading the scene.
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

        // Multiple callers must share one unload operation as well.
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
