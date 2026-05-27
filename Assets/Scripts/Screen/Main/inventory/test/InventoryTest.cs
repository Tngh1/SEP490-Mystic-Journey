using System.Collections.Generic;
using UnityEngine;

public class InventoryTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("INVENTORY TEST START");

        List<InventoryItemData> items =
            new List<InventoryItemData>();

        for (int i = 0; i < 20; i++)
        {
            InventoryItemData item =
                new InventoryItemData();

            item.itemId = 1;

            item.quantity = Random.Range(1, 99);

            items.Add(item);
        }

        Debug.Log("CALL REFRESH");

        UIInventory.Instance.Refresh(items);
    }
}