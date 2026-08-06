using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UISkinInventory : MonoBehaviour
{
    [SerializeField] private UIInventorySkinSlot slotPrefab;
    [SerializeField] private Transform contentParent;
    [SerializeField] private int totalSlots = 8;

    private readonly List<UIInventorySkinSlot> slots = new List<UIInventorySkinSlot>();

    public Action<UIBaseItemSlot> OnInventorySlotClicked;

    private void Awake()
    {
        BindReferences();
        CreateSlots(totalSlots);
    }

    public void Refresh(List<UIItemDisplayData> items)
    {
        BindReferences();
        items ??= new List<UIItemDisplayData>();

        if (slotPrefab == null || contentParent == null)
        {
            Debug.LogWarning("[UISkinInventory] Slot prefab or content parent is missing.", this);
            return;
        }

        CreateSlots(Mathf.Max(totalSlots, items.Count));

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < items.Count)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].SetupSkin(items[i]);
            }
            else
            {
                // Skins are a variable-length list, not a fixed bag: leaving surplus slots
                // active padded Content to 20 cells and scrolled the real ones out of view.
                slots[i].ClearSlot();
                slots[i].gameObject.SetActive(false);
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
            slotPrefab = GetComponentInChildren<UIInventorySkinSlot>(true);
            if (slotPrefab != null && contentParent == transform && slotPrefab.transform.parent != null)
                contentParent = slotPrefab.transform.parent;
        }
    }

    private void CreateSlots(int desiredCount)
    {
        if (slotPrefab == null || contentParent == null)
            return;

        AdoptExistingSlots();

        desiredCount = Mathf.Max(0, desiredCount);
        while (slots.Count < desiredCount)
        {
            UIInventorySkinSlot slot = Instantiate(slotPrefab, contentParent);
            slot.transform.localScale = Vector3.one;
            slot.ClearSlot();
            slot.OnSlotClicked += HandleSlotClicked;
            slots.Add(slot);
        }
    }

    // Slots placed in the scene at design time still show the prefab's placeholder
    // name/icon unless we take them over, so adopt them before instantiating more.
    private void AdoptExistingSlots()
    {
        if (contentParent == null)
            return;

        for (int i = 0; i < contentParent.childCount; i++)
        {
            var existing = contentParent.GetChild(i).GetComponent<UIInventorySkinSlot>();
            if (existing == null || existing == slotPrefab || slots.Contains(existing))
                continue;

            existing.ClearSlot();
            existing.OnSlotClicked += HandleSlotClicked;
            slots.Add(existing);
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
