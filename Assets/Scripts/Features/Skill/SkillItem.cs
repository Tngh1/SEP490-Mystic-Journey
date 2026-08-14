using MysticJourney.API.Models.Response; // Thêm namespace của bạn
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class SkillItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static SkillItem CurrentDraggedItem { get; private set; }
    [Header("Data")]
    public SkillData visualData; // Hình ảnh (từ ScriptableObject)
    public PlayerSkillResponse serverData; // Dữ liệu thật (từ API)

    [Header("UI Components")]
    public Image backgroundImage;
    public Image myIcon;
    public TextMeshProUGUI levelText; // (Tùy chọn) Thêm 1 Text góc nhỏ để hiện Level
    public GameObject lockOverlay;

    [Header("Class Background Sprites")]
    public Sprite knightBackground;
    public Sprite archerBackground;
    public Sprite mageBackground;
    public Sprite allClassBackground; // Dynamic/general background for all-class skills

    private SkillPopup popupManager;
    private GameObject dragVisual;
    private CanvasGroup canvasGroup;
    private ScrollRect parentScrollRect;
    private bool isScrollingList;

    private void Awake()
    {
        popupManager = FindFirstObjectByType<SkillPopup>(FindObjectsInactive.Include);
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    // Hàm này sẽ được gọi bởi SkillPanelManager khi nhận dữ liệu từ API
    public void Setup(SkillData vData, PlayerSkillResponse sData)
    {
        visualData = vData;
        serverData = sData;

        bool isUnlocked = (serverData != null);

        // 1. Gán Background theo Class (Knight, Archer, Mage, All/General)
        if (backgroundImage != null)
        {
            Sprite bgSprite = GetBackgroundForSkill(visualData);
            if (bgSprite != null)
            {
                backgroundImage.sprite = bgSprite;
                backgroundImage.enabled = true;
                backgroundImage.color = isUnlocked ? Color.white : new Color(0.6f, 0.6f, 0.6f, 0.8f);
            }
            else
            {
                if (backgroundImage.sprite == null || backgroundImage.sprite.name == "UISprite")
                {
                    backgroundImage.color = new Color(1f, 1f, 1f, 0f);
                }
            }
        }

        Image rootImage = GetComponent<Image>();
        if (rootImage != null && rootImage != backgroundImage)
        {
            if (rootImage.sprite == null || rootImage.sprite.name == "UISprite" || rootImage.sprite.name == "Background")
            {
                rootImage.color = new Color(1f, 1f, 1f, 0f);
            }
            else
            {
                rootImage.color = Color.white;
            }
        }

        // 2. Icon Kỹ năng
        if (myIcon != null && visualData != null)
        {
            myIcon.sprite = visualData.skillIcon;
            myIcon.preserveAspect = true;

            RectTransform iconRect = myIcon.rectTransform;
            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.15f, 0.15f);
                iconRect.anchorMax = new Vector2(0.85f, 0.85f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
            }
        }

        // 3. Trạng thái Đã mở khóa / Chưa mở khóa (Block / Lock overlay / DimBg)
        if (lockOverlay != null)
        {
            lockOverlay.SetActive(!isUnlocked);
        }

        // Tự động tìm và ẩn DimBg / Clock trong child "block" nếu có để bỏ nền đen mờ
        Transform blockTr = transform.Find("block");
        if (blockTr != null)
        {
            Transform dimBg = blockTr.Find("DimBg");
            if (dimBg != null)
            {
                dimBg.gameObject.SetActive(!isUnlocked);
            }

            Transform clock = blockTr.Find("Clock");
            if (clock != null)
            {
                clock.gameObject.SetActive(!isUnlocked);
            }
        }

        Transform rootDimBg = transform.Find("DimBg");
        if (rootDimBg != null)
        {
            rootDimBg.gameObject.SetActive(!isUnlocked);
        }

        if (levelText != null)
        {
            levelText.text = isUnlocked ? "Lv." + serverData.Level : "";
        }

        if (myIcon != null)
        {
            myIcon.color = isUnlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.6f);
        }
    }

    public void SetupRewardPreview(SkillData vData, Sprite fallbackIcon = null)
    {
        visualData = vData;
        serverData = null;

        if (backgroundImage != null)
        {
            var background = GetBackgroundForSkill(vData);
            if (background != null)
                backgroundImage.sprite = background;
            backgroundImage.enabled = backgroundImage.sprite != null;
            backgroundImage.color = Color.white;
        }

        if (myIcon != null)
        {
            myIcon.sprite = vData != null && vData.skillIcon != null ? vData.skillIcon : fallbackIcon;
            myIcon.enabled = myIcon.sprite != null;
            myIcon.preserveAspect = true;
            myIcon.color = Color.white;
        }

        if (levelText != null)
            levelText.text = "Skill";

        if (lockOverlay != null)
            lockOverlay.SetActive(false);

        SetPreviewOverlayActive("block/DimBg", false);
        SetPreviewOverlayActive("block/Clock", false);
        SetPreviewOverlayActive("DimBg", false);
    }

    private void SetPreviewOverlayActive(string path, bool active)
    {
        var target = transform.Find(path);
        if (target != null)
            target.gameObject.SetActive(active);
    }

    private Sprite GetBackgroundForSkill(SkillData skill)
    {
        if (skill == null) return allClassBackground;

        if (skill.customBackground != null) return skill.customBackground;

        string req = skill.classRequirement != null ? skill.classRequirement.Trim() : "";

        if (string.IsNullOrEmpty(req) || req.Equals("All", System.StringComparison.OrdinalIgnoreCase))
        {
            return allClassBackground;
        }

        if (req.Equals("Knight", System.StringComparison.OrdinalIgnoreCase))
        {
            return knightBackground;
        }

        if (req.Equals("Archer", System.StringComparison.OrdinalIgnoreCase))
        {
            return archerBackground;
        }

        if (req.Equals("Mage", System.StringComparison.OrdinalIgnoreCase))
        {
            return mageBackground;
        }

        return allClassBackground;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (popupManager == null)
        {
            popupManager = FindFirstObjectByType<SkillPopup>(FindObjectsInactive.Include);
        }

        if (popupManager != null)
        {
            popupManager.ShowPopup(visualData, serverData);
        }
        else
        {
            Debug.LogError("[SkillItem] Cannot show skill detail popup: SkillPopup reference was not found in the scene!");
        }
    }

public void OnBeginDrag(PointerEventData eventData)
    {
        // A vertical gesture starts by scrolling. If the pointer later leaves the
        // viewport, OnDrag switches to skill dragging so HUD slots remain reachable.
        if (ShouldScrollList(eventData))
        {
            isScrollingList = true;
            parentScrollRect.OnBeginDrag(eventData);
            return;
        }

        BeginSkillDrag(eventData);
    }

public void OnDrag(PointerEventData eventData)
    {
        if (isScrollingList)
        {
            bool leftViewport = serverData != null &&
                !RectTransformUtility.RectangleContainsScreenPoint(
                    parentScrollRect.viewport,
                    eventData.position,
                    eventData.pressEventCamera);

            if (!leftViewport)
            {
                parentScrollRect.OnDrag(eventData);
                return;
            }

            parentScrollRect.OnEndDrag(eventData);
            parentScrollRect.StopMovement();
            isScrollingList = false;
            BeginSkillDrag(eventData);
        }

        if (dragVisual != null)
        {
            dragVisual.transform.position = eventData.position;
        }
    }

public void OnEndDrag(PointerEventData eventData)
    {
        if (isScrollingList)
        {
            parentScrollRect.OnEndDrag(eventData);
            isScrollingList = false;
            return;
        }

        if (serverData == null) return;

        if (dragVisual != null) Destroy(dragVisual);
        dragVisual = null;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        if (CurrentDraggedItem == this) CurrentDraggedItem = null;
    }

    private bool ShouldScrollList(PointerEventData eventData)
    {
        if (parentScrollRect == null || !parentScrollRect.vertical) return false;
        if (parentScrollRect.content == null || parentScrollRect.viewport == null) return false;
        if (parentScrollRect.content.rect.height <= parentScrollRect.viewport.rect.height) return false;

        return Mathf.Abs(eventData.delta.y) >= Mathf.Abs(eventData.delta.x);
    }

private void OnDisable()
    {
        if (dragVisual != null) Destroy(dragVisual);
        dragVisual = null;
        isScrollingList = false;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        if (CurrentDraggedItem == this) CurrentDraggedItem = null;
    }


private void BeginSkillDrag(PointerEventData eventData)
    {
        if (serverData == null || dragVisual != null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Keep the real card inside the GridLayoutGroup. Only a visual clone follows
        // the pointer, so cancelling a drop cannot break the skill list.
        dragVisual = Instantiate(gameObject, canvas.rootCanvas.transform, true);
        dragVisual.name = name + "_DragVisual";

        var cloneItem = dragVisual.GetComponent<SkillItem>();
        if (cloneItem != null) cloneItem.enabled = false;

        var cloneGroup = dragVisual.GetComponent<CanvasGroup>() ?? dragVisual.AddComponent<CanvasGroup>();
        cloneGroup.alpha = 0.85f;
        cloneGroup.blocksRaycasts = false;
        cloneGroup.interactable = false;

        dragVisual.transform.SetAsLastSibling();
        dragVisual.transform.position = eventData.position;

        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        CurrentDraggedItem = this;
    }
}
