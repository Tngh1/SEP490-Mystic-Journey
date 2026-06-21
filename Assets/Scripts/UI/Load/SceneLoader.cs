using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLoader
{
    private const string LoadingSceneName = "Loading";

    public static string TargetSceneName { get; private set; }

    public static void Load(string targetSceneName)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError("Target scene name is empty.");
            return;
        }

        TargetSceneName = targetSceneName;
        SceneManager.LoadScene(LoadingSceneName);
    }
}