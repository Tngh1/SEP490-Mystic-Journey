using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
        // Lấy component và lưu kích thước chuẩn
        spriteRenderer = GetComponent<SpriteRenderer>();
        uiImage = GetComponent<Image>();
        originalScale = transform.localScale;
        
        // Đặt độ trong suốt về 0 và thu nhỏ ngay khi bắt đầu
        SetAlpha(0f);
        if (enableZoom)
        {
            transform.localScale = originalScale * startScaleMultiplier;
        }
        
        // Kích hoạt hiệu ứng
        StartCoroutine(FadeAndZoomCoroutine());
    }

    // Hàm hỗ trợ để set Alpha
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
            
            // Tính tiến trình (0.0 đến 1.0)
            float t = Mathf.Clamp01(elapsedTime / duration);
            
            // Làm mượt chuyển động bằng SmoothStep (để bắt đầu và kết thúc êm ái hơn)
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            
            // Cập nhật Alpha (Fade In)
            SetAlpha(smoothT);
            
            // Cập nhật kích thước (Zoom In)
            if (enableZoom)
            {
                transform.localScale = Vector3.Lerp(startScale, originalScale, smoothT);
            }
            
            yield return null; // Chờ frame tiếp theo
        }
        
        // Đảm bảo set chính xác các thông số ở cuối cùng
        SetAlpha(1f);
        if (enableZoom) transform.localScale = originalScale;
    }
}
