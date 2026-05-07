using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LoadingScreen : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private TMP_Text tipText;

    [Header("Scene Settings")]
    [SerializeField] private string fallbackSceneName = "DungeonFantasy";
    [SerializeField] private float minimumLoadingTime = 0.6f;

    [Header("Tip Settings")]
    [SerializeField] private LoadingTipDatabase tipDatabase;

    private const float SceneLoadCompleteProgress = 0.9f;
    private const string LoadingPrefix = "Đang tải dữ liệu trò chơi...";

    private void Awake()
    {
        SetProgress(0f);
    }

    private void Start()
    {
        ShowRandomTip();

        string targetScene = string.IsNullOrWhiteSpace(SceneLoader.TargetSceneName)
            ? fallbackSceneName
            : SceneLoader.TargetSceneName;

        StartCoroutine(LoadSceneRoutine(targetScene));
    }

    private void ShowRandomTip()
    {
        if (tipText == null || tipDatabase == null)
            return;

        string tip = tipDatabase.GetRandomTip();

        tipText.text = string.IsNullOrWhiteSpace(tip)
            ? string.Empty
            : $"Mẹo: {tip}";
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Scene name is empty.");
            yield break;
        }

        float startTime = Time.unscaledTime;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);

        if (operation == null)
        {
            Debug.LogError($"Cannot load scene: {sceneName}");
            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < SceneLoadCompleteProgress)
        {
            float normalizedProgress = Mathf.Clamp01(operation.progress / SceneLoadCompleteProgress);
            SetProgress(normalizedProgress);
            yield return null;
        }

        SetProgress(1f);

        float elapsedTime = Time.unscaledTime - startTime;
        float remainingTime = minimumLoadingTime - elapsedTime;

        if (remainingTime > 0f)
            yield return new WaitForSecondsRealtime(remainingTime);

        operation.allowSceneActivation = true;
    }

    private void SetProgress(float value)
    {
        float progress = Mathf.Clamp01(value);

        if (progressFill != null)
            progressFill.fillAmount = progress;

        if (loadingText != null)
            loadingText.text = $"{LoadingPrefix} ({Mathf.RoundToInt(progress * 100f)}%)";
    }
}