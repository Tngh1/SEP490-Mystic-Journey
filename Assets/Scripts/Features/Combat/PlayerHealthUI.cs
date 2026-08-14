using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private float fillAnimDuration = 0.4f;

    private Coroutine _fillCoroutine;

    private void OnEnable()
    {
        PlayerEntity.OnHealthChanged += UpdateHealthUI;
    }

    private void OnDisable()
    {
        PlayerEntity.OnHealthChanged -= UpdateHealthUI;
    }

    private void UpdateHealthUI(int currentHp, int maxHp)
    {
        float targetFill = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;

        if (hpFillImage != null)
        {
            if (_fillCoroutine != null) StopCoroutine(_fillCoroutine);
            _fillCoroutine = StartCoroutine(AnimateFill(targetFill));
        }

        if (hpText != null)
        {
            hpText.text = $"{currentHp} / {maxHp}";
        }
    }

    private IEnumerator AnimateFill(float targetFill)
    {
        if (hpFillImage == null) yield break;

        float startFill = hpFillImage.fillAmount;
        float elapsed = 0f;

        while (elapsed < fillAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fillAnimDuration);
            float currentFill = Mathf.Lerp(startFill, targetFill, t);

            hpFillImage.fillAmount = currentFill;

            yield return null;
        }

        hpFillImage.fillAmount = targetFill;
    }

}
