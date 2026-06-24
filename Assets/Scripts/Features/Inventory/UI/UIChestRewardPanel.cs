using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Models.Response;

public class UIChestRewardPanel : MonoBehaviour
{
    public static UIChestRewardPanel Instance { get; private set; }

    [Header("Static References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Transform rewardListContent;
    [SerializeField] private Button confirmButton;
    [SerializeField] private UIQuestRewardSlot rewardSlotPrefab;

    private readonly List<UIQuestRewardSlot> slots = new();
    private Action onConfirmCallback;

    private void Awake()
    {
        Instance = this;
        BindReferences();
        gameObject.SetActive(false);
    }

    private void BindReferences()
    {
        if (titleText == null)
            titleText = transform.Find("TitleText")?.GetComponent<TMP_Text>();

        if (rewardListContent == null)
        {
            // Search in children for the Scroll View Content
            rewardListContent = transform.Find("RewaimList/Scroll View/Viewport/Content");
            if (rewardListContent == null)
                rewardListContent = transform.Find("RewaimList/Content");
            if (rewardListContent == null)
                rewardListContent = transform.Find("RewaimList");
        }

        // Configure Layout Group on Content if it doesn't have one
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

    public void ShowRewards(string chestName, int gold, int xp, DungeonRewardItemResponse[] items, Action onConfirm)
    {
        BindReferences();
        gameObject.SetActive(true);
        onConfirmCallback = onConfirm;

        if (titleText != null)
        {
            titleText.text = string.IsNullOrEmpty(chestName) ? "Rương Phần Thưởng" : chestName;
        }

        // Deactivate old slots
        foreach (var slot in slots)
        {
            if (slot != null)
                slot.gameObject.SetActive(false);
        }

        int slotIndex = 0;

        // Add Gold
        if (gold > 0)
        {
            var slot = GetOrCreateSlot(slotIndex++);
            slot.Setup("Vàng", $"+{gold}", GetDefaultSprite("Gold"));
        }

        // Add XP
        if (xp > 0)
        {
            var slot = GetOrCreateSlot(slotIndex++);
            slot.Setup("Kinh Nghiệm", $"+{xp}", GetDefaultSprite("EXP"));
        }

        // Add Items
        if (items != null)
        {
            foreach (var item in items)
            {
                if (item == null) continue;

                var slot = GetOrCreateSlot(slotIndex++);
                Sprite itemSprite = null;

                // Resolve item icon if icon database is present
                if (ItemIconDatabase.Instance != null && ItemIconDatabase.Instance.TryGetIcon(item.ItemId, out var sprite))
                {
                    itemSprite = sprite;
                }

                slot.Setup(item.ItemName, $"x{item.Quantity}", itemSprite);
            }
        }

        // Refresh layout
        if (rewardListContent != null)
        {
            var rect = rewardListContent.GetComponent<RectTransform>();
            if (rect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
    }

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
            // Fallback find in scene
            rewardSlotPrefab = FindFirstObjectByType<UIQuestRewardSlot>();
        }

        if (rewardSlotPrefab == null)
        {
            // Create a fallback slot UI programmatically
            GameObject fallbackObj = new GameObject("RewardSlotFallback", typeof(RectTransform));
            fallbackObj.transform.SetParent(rewardListContent, false);
            UIQuestRewardSlot slot = fallbackObj.AddComponent<UIQuestRewardSlot>();
            slots.Add(slot);
            return slot;
        }

        UIQuestRewardSlot newSlot = Instantiate(rewardSlotPrefab, rewardListContent);
        newSlot.gameObject.SetActive(true);
        slots.Add(newSlot);
        return newSlot;
    }

    private Sprite GetDefaultSprite(string type)
    {
        // Return resource icon if exists, else null
        return Resources.Load<Sprite>($"Icons/{type}") ?? Resources.Load<Sprite>(type);
    }

    private void OnConfirmClick()
    {
        gameObject.SetActive(false);
        onConfirmCallback?.Invoke();
    }
}
