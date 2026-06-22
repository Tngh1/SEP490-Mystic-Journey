using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MysticJourney.API.Models.Response; // Thêm namespace của bạn

public class SkillItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Data")]
    public SkillData visualData; // Hình ảnh (từ ScriptableObject)
    public PlayerSkillResponse serverData; // Dữ liệu thật (từ API)

    [Header("UI Components")]
    public Image myIcon;
    public Text levelText; // (Tùy chọn) Thêm 1 Text góc nhỏ để hiện Level

    private SkillPopup popupManager;
    private Transform originalParent;
    private CanvasGroup canvasGroup;

    void Start()
    {
        popupManager = FindFirstObjectByType<SkillPopup>(FindObjectsInactive.Include);
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    // Hàm này sẽ được gọi bởi SkillPanelManager khi nhận dữ liệu từ API
    public void Setup(SkillData vData, PlayerSkillResponse sData)
    {
        visualData = vData;
        serverData = sData;

        myIcon.sprite = visualData.skillIcon;
        if (levelText != null) levelText.text = "Lv." + serverData.Level;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Truyền CẢ HAI loại dữ liệu sang Popup
        popupManager.ShowPopup(visualData, serverData);
    }

    // 1. Khi bắt đầu KÉO -> Đưa skill lên trên cùng để không bị che
    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        // Bật ra ngoài Canvas (transform.root thường là Canvas cao nhất)
        transform.SetParent(transform.root);

        // Tắt chặn chuột để chuột có thể xuyên qua hình này chạm vào Slot ở dưới
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }
    }

    // 2. Đang KÉO -> Hình chạy theo vị trí chuột
    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition;
    }

    // 3. Thả tay ra (Dừng KÉO)
    public void OnEndDrag(PointerEventData eventData)
    {
        // Trả object về lại chỗ cũ trong danh sách (Scroll View)
        transform.SetParent(originalParent);

        // Bật lại chức năng chặn chuột
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }
    }
}