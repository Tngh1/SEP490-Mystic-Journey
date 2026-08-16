using UnityEngine;

/// <summary>
/// Phóng to nhẹ (1.08x) khi rê chuột vào, trả lại scale gốc khi rời ra.
///
/// PHẢI nằm ở file riêng trùng tên class: Unity chỉ serialize được MonoBehaviour
/// khi tên file khớp tên class. Trước đây class này khai báo lồng ở cuối
/// PartyPanel.cs nên không kéo được vào prefab/scene qua Inspector — hệ quả là
/// 15 panel mỗi cái tự viết một vòng AddComponent giống nhau, còn button
/// Instantiate lúc runtime (entry bạn bè, slot guild, ô shop/inventory/daily)
/// thì không ai phủ nên không có hover. Đừng gộp class này trở lại file khác.
/// </summary>
public class UIHoverScaleEffect : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    private Vector3 originalScale;
    private Vector3 targetScale;
    private bool _initialized;

    private void Awake()
    {
        InitScale();
    }

    private void Start()
    {
        InitScale();
    }

    private void InitScale()
    {
        if (!_initialized || originalScale == Vector3.zero)
        {
            originalScale = transform.localScale != Vector3.zero ? transform.localScale : Vector3.one;
            targetScale = originalScale;
            _initialized = true;
        }
    }

    private void Update()
    {
        if (transform.localScale != targetScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 15f);
        }
    }

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
    {
        InitScale();
        targetScale = originalScale * 1.08f;
    }

    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
    {
        InitScale();
        targetScale = originalScale;
    }

    private void OnDisable()
    {
        if (_initialized && originalScale != Vector3.zero)
        {
            transform.localScale = originalScale;
            targetScale = originalScale;
        }
    }
}
