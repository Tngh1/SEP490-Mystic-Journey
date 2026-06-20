using System;
using MysticJourney.API.Models.Response;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIQuestListItem : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleTMP;
    [SerializeField] private TMP_Text statusTMP;
    [SerializeField] private TMP_Text levelTMP;
    [SerializeField] private TMP_Text progressTMP;
    [SerializeField] private Text titleText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text progressText;
    [SerializeField] private GameObject lockedGroup;
    [SerializeField] private Button selectButton;

    private PlayerQuestResponse quest;
    private Action<PlayerQuestResponse> onSelected;
    private Color defaultBackgroundColor;
    private bool hasDefaultBackgroundColor;

    private void Awake()
    {
        Bind();
    }

    public void Setup(PlayerQuestResponse data, bool selected, Action<PlayerQuestResponse> selectedCallback, Sprite iconSprite = null)
    {
        Bind();

        quest = data;
        onSelected = selectedCallback;

        SetText(titleTMP, titleText, data?.QuestTitle ?? "Unknown Quest");
        SetText(statusTMP, statusText, StatusLabel(data));
        SetText(levelTMP, levelText, data == null ? "Lv.?" : $"Lv.{data.RequiredLevel}");
        SetText(progressTMP, progressText, data == null ? string.Empty : ProgressLabel(data));

        if (background != null)
            background.color = selected ? new Color(1f, 1f, 1f, 1f) : defaultBackgroundColor;

        if (icon != null && iconSprite != null)
        {
            icon.sprite = iconSprite;
            icon.enabled = true;
        }

        if (lockedGroup != null)
            lockedGroup.SetActive(false);
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

        if (icon == null)
            icon = FindImageByName("Icon") ?? FindImageByName("Image") ?? FindFirstChildImageExcept(background);

        if (titleTMP == null && titleText == null)
        {
            titleTMP = FindTMPByName("TitleQuest") ?? FindTMPByName("QuestTitle") ?? FindTMPByName("TitleText") ?? FindTMPByName("Title");
            titleText = FindTextByName("TitleQuest") ?? FindTextByName("QuestTitle") ?? FindTextByName("TitleText") ?? FindTextByName("Title");
        }

        var allTMP = GetComponentsInChildren<TMP_Text>(true);
        var allText = GetComponentsInChildren<Text>(true);

        if (titleTMP == null && titleText == null && allTMP.Length > 0)
            titleTMP = allTMP[0];
        if (titleTMP == null && titleText == null && allText.Length > 0)
            titleText = allText[0];

        if (statusTMP == null && statusText == null)
        {
            statusTMP = FindTMPByName("StatusText") ?? GetTMPAt(allTMP, 1);
            statusText = FindTextByName("StatusText") ?? GetTextAt(allText, 1);
        }

        if (levelTMP == null && levelText == null)
        {
            levelTMP = FindTMPByName("LevelText") ?? FindTMPByName("LvText") ?? GetTMPAt(allTMP, 2);
            levelText = FindTextByName("LevelText") ?? FindTextByName("LvText") ?? GetTextAt(allText, 2);
        }

        if (progressTMP == null && progressText == null)
        {
            progressTMP = FindTMPByName("ProgressText") ?? GetTMPAt(allTMP, 3);
            progressText = FindTextByName("ProgressText") ?? GetTextAt(allText, 3);
        }

        if (lockedGroup == null)
            lockedGroup = FindChild("LockedGroup");

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

    private Image FindFirstChildImageExcept(Image excluded)
    {
        var images = GetComponentsInChildren<Image>(true);
        for (var i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i] != excluded)
                return images[i];
        }

        return null;
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

    private Text FindTextByName(string objectName)
    {
        var child = FindChild(objectName);
        return child == null ? null : child.GetComponent<Text>();
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

    private static TMP_Text GetTMPAt(TMP_Text[] texts, int index)
    {
        return texts != null && texts.Length > index ? texts[index] : null;
    }

    private static Text GetTextAt(Text[] texts, int index)
    {
        return texts != null && texts.Length > index ? texts[index] : null;
    }

    private static void SetText(TMP_Text tmp, Text text, string value)
    {
        if (tmp != null)
        {
            tmp.text = value ?? string.Empty;
            return;
        }

        if (text != null)
            text.text = value ?? string.Empty;
    }

    private static string StatusLabel(PlayerQuestResponse data)
    {
        if (data == null)
            return "Unknown";

        return data.Status switch
        {
            "NotStarted" => "Available",
            "InProgress" => "In Progress",
            "Completed" => "Completed",
            "Claimed" => "Claimed",
            _ => string.IsNullOrWhiteSpace(data.Status) ? "Unknown" : data.Status
        };
    }

    private static string ProgressLabel(PlayerQuestResponse data)
    {
        var target = Mathf.Max(1, data.TargetAmount);
        var current = Mathf.Clamp(data.Progress, 0, target);
        return $"{current}/{target}";
    }
}

