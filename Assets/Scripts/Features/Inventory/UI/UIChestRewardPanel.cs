using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Models.Response;

// Executes mono behaviour operation.
public class UIChestRewardPanel : MonoBehaviour
{
    // Executes instance operation.
    public static UIChestRewardPanel Instance { get; private set; }

    [Header("Static References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform rewardListContent;
    [SerializeField] private Button confirmButton;
    [SerializeField] private UIQuestRewardSlot rewardSlotPrefab;

    private readonly List<UIQuestRewardSlot> slots = new();
    private Action onConfirmCallback;

    // Initializes internal component caches and dependencies for UIChestRewardPanel upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        Instance = this;
        BindReferences();
        gameObject.SetActive(false);
    }

    // Executes bind references operation.
    private void BindReferences()
    {
        if (titleText == null)
            titleText = transform.Find("TitleText")?.GetComponent<TMP_Text>();

        if (rewardListContent == null)
        {
            rewardListContent = transform.Find("RewaimList/Scroll View/Viewport/Content");
            if (rewardListContent == null)
                rewardListContent = transform.Find("RewaimList/Content");
            if (rewardListContent == null)
                rewardListContent = transform.Find("RewaimList");
        }

        if (rewardListContent != null)
        {
            var layout = rewardListContent.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = rewardListContent.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(10, 10, 10, 10);
                layout.spacing = 15;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
            }

            var fitter = rewardListContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = rewardListContent.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }

        if (confirmButton == null)
        {
            confirmButton = transform.Find("ActionsButton/ConfirmButton")?.GetComponent<Button>()
                            ?? transform.Find("ActionsButton")?.GetComponentInChildren<Button>();
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClick);
        }

        if (rewardSlotPrefab == null)
        {
            rewardSlotPrefab = Resources.Load<UIQuestRewardSlot>("Quest/QuestRewardSlot_Prefab");
        }
    }

    // Executes show rewards operation.
    // Validates input parameters against null or empty values.
    public void ShowRewards(string chestName, int gold, int xp, DungeonRewardItemResponse[] items, Action onConfirm)
    {
        BindReferences();
        gameObject.SetActive(true);
        onConfirmCallback = onConfirm;

        if (titleText != null)
        {
            titleText.text = (string.IsNullOrEmpty(chestName) || chestName == "Rương Phần Thưởng") ? "Exploration Successful" : chestName;
        }

        if (confirmButton != null)
        {
            var btnText = confirmButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                btnText.text = "Exit";
            }
            else
            {
                var legacyText = confirmButton.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    legacyText.text = "Exit";
                }
            }
        }

        foreach (var slot in slots)
        {
            if (slot != null)
                slot.gameObject.SetActive(false);
        }

        int slotIndex = 0;

        if (gold > 0)
        {
            // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
            var slot = GetOrCreateSlot(slotIndex++);
            slot.Setup("Gold", $"+{gold}", GetDefaultSprite("Gold"));
        }

        if (xp > 0)
        {
            // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
            var slot = GetOrCreateSlot(slotIndex++);
            slot.Setup("Experience", $"+{xp}", GetDefaultSprite("EXP"));
        }

        if (items != null)
        {
            foreach (var item in items)
            {
                if (item == null) continue;

                // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
                var slot = GetOrCreateSlot(slotIndex++);
                Sprite itemSprite = null;

                if (ItemIconDatabase.Instance != null)
                {
                    itemSprite = ItemIconDatabase.Instance.GetIcon(item.ItemName, item.ItemType);
                }

                slot.Setup(item.ItemName, $"x{item.Quantity}", itemSprite);
            }
        }

        if (rewardListContent != null)
        {
            var rect = rewardListContent.GetComponent<RectTransform>();
            if (rect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
    }

    // Executes get or create slot operation.
    private UIQuestRewardSlot GetOrCreateSlot(int index)
    {
        if (index < slots.Count)
        {
            if (slots[index] != null)
            {
                slots[index].gameObject.SetActive(true);
                return slots[index];
            }
        }

        if (rewardSlotPrefab == null)
        {
            rewardSlotPrefab = FindFirstObjectByType<UIQuestRewardSlot>();
        }

        if (rewardSlotPrefab == null)
        {
            GameObject fallbackObj = new GameObject("RewardSlotFallback", typeof(RectTransform));
            fallbackObj.transform.SetParent(rewardListContent, false);
            // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
            UIQuestRewardSlot slot = fallbackObj.AddComponent<UIQuestRewardSlot>();
            slots.Add(slot);
            // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
            return slot;
        }

        UIQuestRewardSlot newSlot = Instantiate(rewardSlotPrefab, rewardListContent);
        newSlot.gameObject.SetActive(true);
        slots.Add(newSlot);
        return newSlot;
    }

    // Executes get default sprite operation.
    private Sprite GetDefaultSprite(string type)
    {
        return Resources.Load<Sprite>($"Icons/{type}") ?? Resources.Load<Sprite>(type);
    }

    // Executes on confirm click operation.
    private void OnConfirmClick()
    {
        gameObject.SetActive(false);
        onConfirmCallback?.Invoke();
    }
}
