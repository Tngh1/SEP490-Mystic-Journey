using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Kế thừa các interface để xài tính năng Click và Drag của Unity
public class SkillItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public SkillData mySkillData;
    public Image myIcon;

    private SkillPopup popupManager;
    private Transform originalParent;
    private CanvasGroup canvasGroup;

    void Start()
    {
        popupManager = FindObjectOfType<SkillPopup>(true); // Tìm cái popup trong scene
        myIcon.sprite = mySkillData.skillIcon; // Tự gán hình

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // Khi người chơi CLICK vào skill -> Hiện Popup
    public void OnPointerClick(PointerEventData eventData)
    {
        popupManager.ShowPopup(mySkillData);
    }

    // Khi bắt đầu KÉO -> Đưa skill lên trên cùng để không bị che
    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        transform.SetParent(transform.root); // Bật ra ngoài Canvas
        canvasGroup.blocksRaycasts = false;  // Cho phép chuột xuyên qua hình này để chạm vào Slot ở dưới
    }

    // Đang KÉO -> Hình chạy theo chuột
    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition;
    }

    // Thả tay ra (Dừng KÉO)
    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(originalParent); // Trả về chỗ cũ trong danh sách
        canvasGroup.blocksRaycasts = true;
    }
}