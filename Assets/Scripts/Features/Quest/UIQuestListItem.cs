using System;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIQuestListItem : MonoBehaviour
{
    public enum QuestTypeSlot
    {
        Kill,
        Collect,
        Talk,
        Explore,
    }

    public static QuestTypeSlot MapObjectiveType(string objectiveType)
    {
        var normalized = string.IsNullOrWhiteSpace(objectiveType)
            ? "explore"
            : objectiveType.Trim().ToLowerInvariant();

        switch (normalized)
        {
            case "defeat":
            case "kill":
                return QuestTypeSlot.Kill;
            case "collect":
            case "gather":
            case "fetch":
                return QuestTypeSlot.Collect;
            case "talk":
                return QuestTypeSlot.Talk;
            default:
                return QuestTypeSlot.Explore;
        }
    }
    [SerializeField] private Image background;
    [SerializeField] private GameObject activeBackground;
    [SerializeField] private TMP_Text titleTMP;
    [SerializeField] private TMP_Text suggestLevelTMP;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private GameObject completeIcon;
    [SerializeField] private Button selectButton;

    [Header("Type Icon (1 image, đổi sprite theo ObjectiveType)")]
    [SerializeField] private Image typeImage;

    [Header("Type Icons (sprite theo ObjectiveType)")]
    [SerializeField] private Sprite killIcon;
    [SerializeField] private Sprite collectIcon;
    [SerializeField] private Sprite talkIcon;
    [SerializeField] private Sprite exploreIcon;

    [Header("Visual")]
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color dimColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color suggestLevelLockedColor = new Color(1f, 0.15f, 0.15f, 1f);

    private PlayerQuestResponse quest;
    private Action<PlayerQuestResponse> onSelected;
    private Color defaultBackgroundColor;
    private bool hasDefaultBackgroundColor;
    private Color suggestLevelNormalColor;
    private bool hasSuggestLevelNormalColor;

    private void Awake()
    {
        Bind();
    }

    public void Setup(PlayerQuestResponse data, bool selected, Action<PlayerQuestResponse> selectedCallback)
    {
        Bind();

        quest = data;
        onSelected = selectedCallback;

        if (titleTMP != null)
            titleTMP.text = data?.QuestTitle ?? "Unknown Quest";

        bool underLeveled = data != null && WorldState.PlayerLevel < data.RequiredLevel;

        if (suggestLevelTMP != null)
        {
            suggestLevelTMP.text = data == null ? "Suggested: Level ?" : $"Suggested: Level {data.RequiredLevel}";
            suggestLevelTMP.color = underLeveled ? suggestLevelLockedColor : suggestLevelNormalColor;
        }

        ApplyTypeIcon(data?.ObjectiveType);

        if (background != null)
            background.color = selected ? Color.white : defaultBackgroundColor;

        if (activeBackground != null)
            activeBackground.SetActive(selected);

        // Icon complete chỉ hiện khi ĐÃ NHẬN THƯỞNG (Claimed). Trạng thái Completed mà chưa
        // claim vẫn coi như đang làm dở → không đóng dấu hoàn thành.
        bool isComplete = data != null && string.Equals(data.Status, "Claimed", StringComparison.OrdinalIgnoreCase);
        // Ổ khóa chỉ nói về điều kiện KHÔNG THỂ làm được (thiếu level). Quest NotStarted nhưng
        // đủ level là quest sắp nhận (đang được tracker chỉ đường) → không được khóa.
        bool isLocked = data == null || underLeveled;

        if (lockIcon != null) lockIcon.SetActive(isLocked);
        if (completeIcon != null) completeIcon.SetActive(isComplete);
    }

    private void ApplyTypeIcon(string objectiveType)
    {
        if (typeImage == null) return;

        var sprite = MapObjectiveType(objectiveType) switch
        {
            QuestTypeSlot.Kill => killIcon,
            QuestTypeSlot.Collect => collectIcon,
            QuestTypeSlot.Talk => talkIcon,
            _ => exploreIcon,
        };

        if (sprite != null)
            typeImage.sprite = sprite;

        bool hasSprite = typeImage.sprite != null;
        typeImage.enabled = hasSprite;
        typeImage.gameObject.SetActive(hasSprite);
    }

    private void Bind()
    {
        if (background == null)
            background = FindImageByName("Background") ?? GetComponent<Image>();
        if (background != null && !hasDefaultBackgroundColor)
        {
            defaultBackgroundColor = background.color;
            hasDefaultBackgroundColor = true;
        }

        if (typeImage == null)
            typeImage = FindImageByName("Type") ?? FindImageByName("QuestType");

        if (activeBackground == null)
            activeBackground = FindChild("ActiveBackground");

        if (titleTMP == null)
            titleTMP = FindTMPByName("TitleQuest") ?? FindTMPByName("QuestTitle") ?? FindTMPByName("Title");
        if (suggestLevelTMP == null)
            suggestLevelTMP = FindTMPByName("SuggestLevel") ?? FindTMPByName("LevelText");
        if (suggestLevelTMP != null && !hasSuggestLevelNormalColor)
        {
            suggestLevelNormalColor = suggestLevelTMP.color;
            hasSuggestLevelNormalColor = true;
        }
        if (lockIcon == null)
            lockIcon = FindChild("Lock");
        if (completeIcon == null)
            completeIcon = FindChild("Complete");

        if (selectButton == null)
            selectButton = GetComponent<Button>();
        if (selectButton == null)
            selectButton = gameObject.AddComponent<Button>();
        if (selectButton == null)
            return;

        if (selectButton.onClick == null)
            selectButton.onClick = new Button.ButtonClickedEvent();

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelected?.Invoke(quest));
    }

    private Image FindImageByName(string objectName)
    {
        var child = FindChild(objectName);
        return child == null ? null : child.GetComponent<Image>();
    }

    private TMP_Text FindTMPByName(string objectName)
    {
        var child = FindChild(objectName);
        return child == null ? null : child.GetComponent<TMP_Text>();
    }

    private GameObject FindChild(string objectName)
    {
        var children = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == objectName)
                return children[i].gameObject;
        }

        return null;
    }
}
