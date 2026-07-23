using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI loading nằm trong Bootstrap scene. Lắng nghe <see cref="LoadingProgress"/> để cập nhật
/// thanh bar (lerp mượt) và status text. Vòng đời = vòng đời Bootstrap scene: GameBootstrap
/// unload bootstrap scene khi world sẵn sàng -> UI này biến mất đúng lúc, không cần destroy tay.
/// </summary>
public sealed class BootstrapLoadingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text statusText;

    [Header("Tuning")]
    [Tooltip("Tốc độ lerp thanh bar tới giá trị đích. Cao hơn = nhanh hơn.")]
    [SerializeField] private float fillLerpSpeed = 8f;

    private float _target;

    private void OnEnable()
    {
        _target = LoadingProgress.Value;
        ApplyImmediate(_target, LoadingProgress.Status);
        LoadingProgress.OnProgress += HandleProgress;
    }

    private void OnDisable()
    {
        LoadingProgress.OnProgress -= HandleProgress;
    }

    private void HandleProgress(float value, string status)
    {
        _target = value;
        if (statusText != null) statusText.text = status;
    }

    private void Update()
    {
        if (progressFill == null) return;

        progressFill.fillAmount = Mathf.MoveTowards(
            progressFill.fillAmount,
            _target,
            Time.unscaledDeltaTime * Mathf.Max(0.01f, fillLerpSpeed));
    }

    private void ApplyImmediate(float value, string status)
    {
        if (progressFill != null) progressFill.fillAmount = value;
        if (statusText != null) statusText.text = status;
    }
}
