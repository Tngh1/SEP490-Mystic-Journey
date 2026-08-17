using System.Collections.Generic;
using UnityEngine;

// Executes mono behaviour operation.
public class InventoryTest : MonoBehaviour
{
    // Performs startup initialization for InventoryTest on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        Debug.Log("INVENTORY TEST START");

        List<UIItemDisplayData> items = new List<UIItemDisplayData>();

        for (int i = 0; i < 20; i++)
        {
            UIItemDisplayData item = new UIItemDisplayData();

            item.itemId = 1;
            item.itemName = "Iron Sword";
            item.category = "Weapon";
            // Randomize the eligible candidates before selecting this gameplay result.
            item.quantity = Random.Range(1, 99);

            item.icon = ItemIconDatabase.Instance.GetIcon(item.itemName, item.category);

            string[] rarities = { "common", "rare", "epic", "legendary" };
            // Randomize the eligible candidates before selecting this gameplay result.
            item.rarity = rarities[Random.Range(0, rarities.Length)];

            item.isEquipped = (i % 5 == 0);

            items.Add(item);
        }

        Debug.Log("CALL REFRESH");
        InventoryPanel.Instance.Refresh(items);
    }
}
