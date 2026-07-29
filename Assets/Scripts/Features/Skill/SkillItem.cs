using MysticJourney.API.Models.Response; // Thêm namespace của bạn
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SkillItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Data")]
    public SkillData visualData; // Hình ảnh (từ ScriptableObject)
    public PlayerSkillResponse serverData; // Dữ liệu thật (từ API)

    [Header("UI Components")]
    public Image backgroundImage;
    public Image myIcon;
    public TextMeshProUGUI levelText;// (Tùy chọn) Thêm 1 Text góc nhỏ để hiện Level
    public GameObject lockOverlay;
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

        // 1. Loại bỏ ô nền hình chữ nhật màu trắng
        Image rootImage = GetComponent<Image>();
        if (rootImage != null)
        {
            if (rootImage.sprite == null || rootImage.sprite.name == "UISprite" || rootImage.sprite.name == "Background")
            {
                rootImage.color = new Color(1f, 1f, 1f, 0f); // Làm nền trắng hoàn toàn trong suốt
            }
            else
            {
                rootImage.color = Color.white;
            }
        }

        if (backgroundImage != null)
        {
            if (backgroundImage.sprite == null || backgroundImage.sprite.name == "UISprite")
            {
                backgroundImage.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        // 2. Định dạng Icon Kỹ năng nằm gọn gàng bên trong khung (chống lọt/tràn ra ngoài)
        if (myIcon != null && visualData != null)
        {
            myIcon.sprite = visualData.skillIcon;
            myIcon.preserveAspect = true; // Giữ tỉ lệ chuẩn của skill icon

            // Căn lề icon nằm lùi vào trong khung 15% để không đè đè lên viền vàng
            RectTransform iconRect = myIcon.rectTransform;
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.15f, 0.15f);
                iconRect.anchorMax = new Vector2(0.85f, 0.85f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
            }
        }

        // 3. Trạng thái Đã mở khóa / Chưa sở hữu (Khóa)
        if (serverData == null)
        {
            if (levelText != null) levelText.text = "";
            if (lockOverlay != null) lockOverlay.SetActive(true);
            if (myIcon != null) myIcon.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        }
        else
        {
            if (levelText != null) levelText.text = "Lv." + serverData.Level;
            if (lockOverlay != null) lockOverlay.SetActive(false);
            if (myIcon != null) myIcon.color = Color.white;
        }
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