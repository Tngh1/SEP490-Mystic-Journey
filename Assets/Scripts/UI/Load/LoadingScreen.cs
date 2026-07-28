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

    public static IEnumerator Show(string status = "Loading map...")
    {
        LoadingProgress.Reset();
        LoadingProgress.Report(0.05f, status);
        _shownAt = Time.unscaledTime;

        var scene = SceneManager.GetSceneByName(SceneName);
        if (scene.IsValid() && scene.isLoaded)
            yield break;

        yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
    }

    public static IEnumerator Hide()
    {
        LoadingProgress.Report(1f, "Ready");

        float elapsed = Time.unscaledTime - _shownAt;
        if (elapsed < MinSeconds)
            yield return new WaitForSecondsRealtime(MinSeconds - elapsed);

        var scene = SceneManager.GetSceneByName(SceneName);
        if (scene.IsValid() && scene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(scene);
    }
}
