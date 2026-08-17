using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
// Executes i end drag handler operation.
public class SkillItem : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // Executes current dragged item operation.
    public static SkillItem CurrentDraggedItem { get; private set; }
    [Header("Data")]
    public SkillData visualData;
    public PlayerSkillResponse serverData;

    [Header("UI Components")]
    public Image backgroundImage;
    public Image myIcon;
    public TextMeshProUGUI levelText;
    public GameObject lockOverlay;

    [Header("Class Background Sprites")]
    public Sprite knightBackground;
    public Sprite archerBackground;
    public Sprite mageBackground;
    public Sprite allClassBackground;

    private SkillPopup popupManager;
    private GameObject dragVisual;
    private CanvasGroup canvasGroup;
    private ScrollRect parentScrollRect;
    private bool isScrollingList;

    // Initializes internal component caches and dependencies for SkillItem upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        popupManager = FindFirstObjectByType<SkillPopup>(FindObjectsInactive.Include);
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        parentScrollRect = GetComponentInParent<ScrollRect>();
    }

    // Executes setup operation.
    public void Setup(SkillData vData, PlayerSkillResponse sData)
    {
        visualData = vData;
        serverData = sData;

        bool isUnlocked = (serverData != null);

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

        if (lockOverlay != null)
        {
            lockOverlay.SetActive(!isUnlocked);
        }

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

    // Executes setup reward preview operation.
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

    // Executes set preview overlay active operation.
    // Validates input parameters against null or empty values.
    private void SetPreviewOverlayActive(string path, bool active)
    {
        var target = transform.Find(path);
        if (target != null)
            target.gameObject.SetActive(active);
    }

    // Executes get background for skill operation.
    // Validates input parameters against null or empty values.
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

    // Executes on pointer click operation.
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

// Executes on begin drag operation.
public void OnBeginDrag(PointerEventData eventData)
    {
        if (ShouldScrollList(eventData))
        {
            isScrollingList = true;
            parentScrollRect.OnBeginDrag(eventData);
            return;
        }

        BeginSkillDrag(eventData);
    }

// Executes on drag operation.
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

// Executes on end drag operation.
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

    // Executes should scroll list operation.
    private bool ShouldScrollList(PointerEventData eventData)
    {
        if (parentScrollRect == null || !parentScrollRect.vertical) return false;
        if (parentScrollRect.content == null || parentScrollRect.viewport == null) return false;
        if (parentScrollRect.content.rect.height <= parentScrollRect.viewport.rect.height) return false;

        return Mathf.Abs(eventData.delta.y) >= Mathf.Abs(eventData.delta.x);
    }

// Unsubscribe this component's event handlers and release its temporary runtime resources.
private void OnDisable()
    {
        if (dragVisual != null) Destroy(dragVisual);
        dragVisual = null;
        isScrollingList = false;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        if (CurrentDraggedItem == this) CurrentDraggedItem = null;
    }


// Executes begin skill drag operation.
private void BeginSkillDrag(PointerEventData eventData)
    {
        if (serverData == null || dragVisual != null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

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
