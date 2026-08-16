using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryPanel : MonoBehaviour
{
    public static InventoryPanel Instance;

    [SerializeField] private UIInventorySlot slotPrefab;
    [SerializeField] private Transform contentParent;
    [SerializeField] private int totalSlots = 64;

    private readonly List<UIInventorySlot> slots = new List<UIInventorySlot>();

    public Action<UIBaseItemSlot> OnInventorySlotClicked;

    private void Awake()
    {
        Instance = this;
        BindReferences();
        CreateSlots(totalSlots);
    }

    public void Refresh(List<UIItemDisplayData> items)
    {
        BindReferences();
        items ??= new List<UIItemDisplayData>();

        if (slotPrefab == null || contentParent == null)
        {
            Debug.LogWarning("[InventoryPanel] Slot prefab or content parent is missing.", this);
            return;
        }

        CreateSlots(Mathf.Max(totalSlots, items.Count));

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < items.Count)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].ClearSlot();
                slots[i].SetupInventory(items[i]);
            }
            else
            {
                slots[i].ClearSlot();
                slots[i].gameObject.SetActive(true);
            }
        }

        var rect = contentParent.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void BindReferences()
    {
        if (contentParent == null)
            contentParent = FindChild("Content") ?? transform;

        if (slotPrefab == null)
        {
            slotPrefab = GetComponentInChildren<UIInventorySlot>(true);
            if (slotPrefab != null && contentParent == transform && slotPrefab.transform.parent != null)
                contentParent = slotPrefab.transform.parent;
        }
    }

    private void CreateSlots(int desiredCount)
    {
        if (slotPrefab == null || contentParent == null)
            return;

        desiredCount = Mathf.Max(0, desiredCount);
        while (slots.Count < desiredCount)
        {
            UIInventorySlot slot = Instantiate(slotPrefab, contentParent);
            slot.transform.localScale = Vector3.one;
            slot.ClearSlot();
            slot.OnSlotClicked += HandleSlotClicked;
            slots.Add(slot);
        }
    }

    private void HandleSlotClicked(UIBaseItemSlot clickedSlot)
    {
        OnInventorySlotClicked?.Invoke(clickedSlot);
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