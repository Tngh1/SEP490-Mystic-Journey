using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Image hpGlowImage;
    [SerializeField] private Color healGlowColor = new Color(0.96f, 0.42f, 0.52f, 0.40f);
    [SerializeField] private float fillAnimDuration = 0.4f;

    private int _lastHp = -1;
    private Coroutine _fillCoroutine;
    private Coroutine _glowCoroutine;
    private Transform _hpBarContainer;
    private Vector3 _originalScale = Vector3.one;

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

        if (_lastHp >= 0 && currentHp > _lastHp)
        {
            TriggerHealGlow();
        }
        _lastHp = currentHp;

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

    private void SetupGlowOverlay()
    {
        if (hpFillImage == null) return;
        if (_hpBarContainer == null)
        {
            _hpBarContainer = hpFillImage.transform.parent;
            if (_hpBarContainer != null && _originalScale == Vector3.one)
            {
                _originalScale = _hpBarContainer.localScale;
            }
        }

        if (hpGlowImage == null && _hpBarContainer != null)
        {
            var existingGlow = _hpBarContainer.Find("HPGlowOverlay");
            if (existingGlow != null)
            {
                hpGlowImage = existingGlow.GetComponent<Image>();
            }
            else
            {
                GameObject glowObj = new GameObject("HPGlowOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                glowObj.transform.SetParent(_hpBarContainer, false);

                RectTransform rect = glowObj.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;

                hpGlowImage = glowObj.GetComponent<Image>();
                hpGlowImage.sprite = hpFillImage.sprite;
                hpGlowImage.type = hpFillImage.type;
                hpGlowImage.fillMethod = hpFillImage.fillMethod;
                hpGlowImage.fillOrigin = hpFillImage.fillOrigin;
                hpGlowImage.raycastTarget = false;
                hpGlowImage.color = new Color(healGlowColor.r, healGlowColor.g, healGlowColor.b, 0f);

                glowObj.transform.SetAsLastSibling();
            }
        }
    }

    public void TriggerHealGlow()
    {
        if (hpFillImage == null) return;
        SetupGlowOverlay();

        if (_glowCoroutine != null) StopCoroutine(_glowCoroutine);
        _glowCoroutine = StartCoroutine(GlowRoutine());
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
            if (hpGlowImage != null) hpGlowImage.fillAmount = currentFill;

            yield return null;
        }

        hpFillImage.fillAmount = targetFill;
        if (hpGlowImage != null) hpGlowImage.fillAmount = targetFill;
    }

    private IEnumerator GlowRoutine()
    {
        Color originalColor = hpFillImage.color;
        Color redGlowTint = new Color(1f, 0.35f, 0.4f, 1f);
        float duration = 0.65f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float norm = elapsed / duration;
            float glowAlpha = Mathf.Sin(norm * Mathf.PI) * healGlowColor.a;
            float scaleMultiplier = 1f + (Mathf.Sin(norm * Mathf.PI) * 0.06f);

            if (hpGlowImage != null)
            {
                hpGlowImage.color = new Color(healGlowColor.r, healGlowColor.g, healGlowColor.b, glowAlpha);
            }

            hpFillImage.color = Color.Lerp(originalColor, redGlowTint, glowAlpha * 0.5f);

            if (_hpBarContainer != null && _originalScale != Vector3.zero)
            {
                _hpBarContainer.localScale = _originalScale * scaleMultiplier;
            }

            yield return null;
        }

        if (hpGlowImage != null)
        {
            hpGlowImage.color = new Color(healGlowColor.r, healGlowColor.g, healGlowColor.b, 0f);
        }
        hpFillImage.color = originalColor;

        if (_hpBarContainer != null && _originalScale != Vector3.zero)
        {
            _hpBarContainer.localScale = _originalScale;
        }
    }
}