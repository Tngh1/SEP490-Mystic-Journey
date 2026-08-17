using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Executes mono behaviour operation.
public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private float fillAnimDuration = 0.4f;

    private Coroutine _fillCoroutine;

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        PlayerEntity.OnHealthChanged += UpdateHealthUI;
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        PlayerEntity.OnHealthChanged -= UpdateHealthUI;
    }

    // Executes update health ui operation.
    private void UpdateHealthUI(int currentHp, int maxHp)
    {
        // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
        float targetFill = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;

        if (hpFillImage != null)
        {
            if (_fillCoroutine != null) StopCoroutine(_fillCoroutine);
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            _fillCoroutine = StartCoroutine(AnimateFill(targetFill));
        }

        if (hpText != null)
        {
            hpText.text = $"{currentHp} / {maxHp}";
        }
    }

    // Executes animate fill operation.
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
