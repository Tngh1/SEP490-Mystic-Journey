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

    // Tr?m trung chuy?n s? ki?n click ra ngo�i
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

            // ??ng k� l?ng nghe s? ki?n Click t? Slot n�y
            slot.OnSlotClicked += HandleSlotClicked;

            slots.Add(slot);
        }
    }

    private void HandleSlotClicked(UIBaseItemSlot clickedSlot)
    {
        // Ph�ng lu?ng s? ki?n ra ngo�i (Cho Manager h?ng)
        OnInventorySlotClicked?.Invoke(clickedSlot);
    }

    public void Refresh(List<UIItemDisplayData> items)
    {
        Debug.Log("REFRESH CALLED: " + items.Count + " items");

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < items.Count)
            {
                // C� data -> ?? data v�o
                slots[i].gameObject.SetActive(true); // ??m b?o � ?ang b?t
                slots[i].SetupInventory(items[i]);
            }
            else
            {
                // H?t data -> D?n d?p � tr?ng
                slots[i].ClearSlot();

                // L?u �: T�y thi?t k? c?a Mira. 
                // N?u mu?n T�i ?? lu�n hi?n ?? 64 � (d� tr?ng r?ng) th� GI? NGUY�N d�ng SetActive(true).
                // N?u mu?n T�i ?? t? co l?i v?a kh�t s? l??ng ??, th� th�m d�ng: slots[i].gameObject.SetActive(false);
            }
        }
    }
}
