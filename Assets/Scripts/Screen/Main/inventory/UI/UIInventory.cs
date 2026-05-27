using System.Collections.Generic;
using UnityEngine;

public class UIInventory : MonoBehaviour
{
    public static UIInventory Instance;

    [SerializeField] private UIInventorySlot slotPrefab;

    [SerializeField] private Transform contentParent;

    [SerializeField] private int totalSlots = 64;

    private List<UIInventorySlot> slots =
        new List<UIInventorySlot>();

    private void Awake()
    {
        Instance = this;

        CreateSlots();
    }

    private void CreateSlots()
    {
        for (int i = 0; i < totalSlots; i++)
        {
            UIInventorySlot slot =
                Instantiate(slotPrefab, contentParent);

            slot.Clear();

            slots.Add(slot);
        }
    }

    public void Refresh(List<InventoryItemData> items)
    {
        Debug.Log("REFRESH CALLED");

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < items.Count)
            {
                Debug.Log("SET SLOT: " + i);

                slots[i].SetData(items[i]);
            }
            else
            {
                slots[i].Clear();
            }
        }
    }
}