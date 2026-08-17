using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
public sealed class BootstrapLoadingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text statusText;

    [Header("Tuning")]
    [Tooltip("Tốc độ lerp thanh bar tới giá trị đích. Cao hơn = nhanh hơn.")]
    [SerializeField] private float fillLerpSpeed = 8f;

    private float _target;

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        _target = LoadingProgress.Value;
        ApplyImmediate(_target, LoadingProgress.Status);
        LoadingProgress.OnProgress += HandleProgress;
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        LoadingProgress.OnProgress -= HandleProgress;
    }

    // Executes handle progress operation.
    private void HandleProgress(float value, string status)
    {
        _target = value;
        if (statusText != null) statusText.text = status;
    }

    // Per-frame update loop for BootstrapLoadingUI.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (progressFill == null) return;

        progressFill.fillAmount = Mathf.MoveTowards(
            progressFill.fillAmount,
            _target,
            Time.unscaledDeltaTime * Mathf.Max(0.01f, fillLerpSpeed));
    }

    // Executes apply immediate operation.
    private void ApplyImmediate(float value, string status)
    {
        if (progressFill != null) progressFill.fillAmount = value;
        if (statusText != null) statusText.text = status;
    }
}
