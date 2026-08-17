using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
public class FadeInLogo : MonoBehaviour
{
    [Header("Cài đặt chung")]
    [Tooltip("Thời gian xuất hiện (giây)")]
    public float duration = 2f;

    [Header("Cài đặt Zoom")]
    [Tooltip("Có bật hiệu ứng zoom không?")]
    public bool enableZoom = true;
    [Tooltip("Kích thước ban đầu (ví dụ: 0.5 là bằng một nửa kích thước chuẩn, 0 là từ nhỏ xíu)")]
    public float startScaleMultiplier = 0.5f;

    private SpriteRenderer spriteRenderer;
    private Image uiImage;
    private Vector3 originalScale;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        uiImage = GetComponent<Image>();
        originalScale = transform.localScale;

        SetAlpha(0f);
        if (enableZoom)
        {
            transform.localScale = originalScale * startScaleMultiplier;
        }

        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(FadeAndZoomCoroutine());
    }

    // Executes set alpha operation.
    private void SetAlpha(float alpha)
    {
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
        else if (uiImage != null)
        {
            Color color = uiImage.color;
            color.a = alpha;
            uiImage.color = color;
        }
    }

    IEnumerator FadeAndZoomCoroutine()
    {
        float elapsedTime = 0f;
        Vector3 startScale = originalScale * startScaleMultiplier;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            float t = Mathf.Clamp01(elapsedTime / duration);

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            SetAlpha(smoothT);

            if (enableZoom)
            {
                transform.localScale = Vector3.Lerp(startScale, originalScale, smoothT);
            }

            yield return null;
        }

        SetAlpha(1f);
        if (enableZoom) transform.localScale = originalScale;
    }
}
