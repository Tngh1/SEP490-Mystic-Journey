using System;
using System.Collections.Generic;
using UnityEngine;

public class UIInventory : MonoBehaviour
{
    public static UIInventory Instance;

    [SerializeField] private UIInventorySlot slotPrefab;
    [SerializeField] private Transform contentParent;
    [SerializeField] private int totalSlots = 64;

    private List<UIInventorySlot> slots = new List<UIInventorySlot>();

    // Tr?m trung chuy?n s? ki?n click ra ngoài
    public Action<UIBaseItemSlot> OnInventorySlotClicked;

    private void Awake()
    {
        Instance = this;
        CreateSlots();
    }

    private void CreateSlots()
    {
        for (int i = 0; i < totalSlots; i++)
        {
            UIInventorySlot slot = Instantiate(slotPrefab, contentParent);
            slot.ClearSlot();

            // ??ng ký l?ng nghe s? ki?n Click t? Slot này
            slot.OnSlotClicked += HandleSlotClicked;

            slots.Add(slot);
        }
    }

    private void HandleSlotClicked(UIBaseItemSlot clickedSlot)
    {
        // Phóng lu?ng s? ki?n ra ngoài (Cho Manager h?ng)
        OnInventorySlotClicked?.Invoke(clickedSlot);
    }

    public void Refresh(List<UIItemDisplayData> items)
    {
        Debug.Log("REFRESH CALLED: " + items.Count + " items");

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < items.Count)
            {
                // Có data -> ?? data vào
                slots[i].gameObject.SetActive(true); // ??m b?o ô ?ang b?t
                slots[i].SetupInventory(items[i]);
            }
            else
            {
                // H?t data -> D?n d?p ô tr?ng
                slots[i].ClearSlot();

                // L?u ý: Tùy thi?t k? c?a Mira. 
                // N?u mu?n Túi ?? luôn hi?n ?? 64 ô (dù tr?ng r?ng) thì GI? NGUYÊN dòng SetActive(true).
                // N?u mu?n Túi ?? t? co l?i v?a khít s? l??ng ??, thì thêm dòng: slots[i].gameObject.SetActive(false);
            }
        }
    }
}