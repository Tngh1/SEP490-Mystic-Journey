using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIDailyLogin : MonoBehaviour
{
    public static UIDailyLogin Instance;

    [Header("UI Settings")]
    [SerializeField] private UIDailySlot dailySlotPrefab;
    [SerializeField] private Transform contentParent;

    private readonly List<UIDailySlot> slots = new List<UIDailySlot>();

    public Action<UIBaseItemSlot> OnDailyItemClaimed;

    private void Awake()
    {
        Instance = this;
        BindReferences();
    }

    public void RefreshDaily(List<UIItemDisplayData> dailyItems)
    {
        BindReferences();
        dailyItems ??= new List<UIItemDisplayData>();

        if (dailySlotPrefab == null || contentParent == null)
        {
            Debug.LogWarning("[UIDailyLogin] Daily slot prefab or content parent is missing.", this);
            return;
        }

        // Tự động Instantiate nếu thiếu slot
        for (int i = 0; i < dailyItems.Count; i++)
        {
            if (i >= slots.Count)
            {
                UIDailySlot newSlot = Instantiate(dailySlotPrefab, contentParent);
                newSlot.transform.localScale = Vector3.one;
                newSlot.OnSlotClicked += HandleSlotClicked;
                slots.Add(newSlot);
            }

            slots[i].gameObject.SetActive(true);
            slots[i].SetupDaily(dailyItems[i]);
        }

        // Ẩn các slot thừa
        for (int i = dailyItems.Count; i < slots.Count; i++)
        {
            slots[i].ClearSlot();
            slots[i].gameObject.SetActive(false); 
        }

        var rect = contentParent.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void HandleSlotClicked(UIBaseItemSlot clickedSlot)
    {
        OnDailyItemClaimed?.Invoke(clickedSlot);
    }

    private void BindReferences()
    {
        // Nếu danh sách slots chưa có gì, tiến hành tìm kiếm toàn bộ con có chứa UIDailySlot
        if (slots.Count == 0)
        {
            var foundSlots = GetComponentsInChildren<UIDailySlot>(true);
            foreach (var slot in foundSlots)
            {
                // Tránh add trùng lặp và gắn sự kiện click
                if (!slots.Contains(slot))
                {
                    slot.OnSlotClicked -= HandleSlotClicked;
                    slot.OnSlotClicked += HandleSlotClicked;
                    slots.Add(slot);
                }
            }
        }
    }

    private Transform FindChild(string objectName)
    {
        var children = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == objectName)
                return children[i];
        }

        return null;
    }
}