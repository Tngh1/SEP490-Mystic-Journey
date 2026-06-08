using System.Collections.Generic;
using UnityEngine;

public class InventoryTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("INVENTORY TEST START");

        List<UIItemDisplayData> items = new List<UIItemDisplayData>();

        for (int i = 0; i < 20; i++)
        {
            UIItemDisplayData item = new UIItemDisplayData();

            // 1. Gán ID và S? l??ng ng?u nhiên
            item.itemId = 1;
            item.quantity = Random.Range(1, 99);

            // 2. [QUAN TR?NG] L?y Icon t? Database. N?u null, l??i s? không v? hình.
            item.icon = ItemIconDatabase.Instance.GetIcon(item.itemId);

            // 3. Test ?? màu vi?n (Ph?m ch?t ng?u nhiên)
            string[] rarities = { "common", "rare", "epic", "legendary" };
            item.rarity = rarities[Random.Range(0, rarities.Length)];

            // 4. Test hi?n th? Icon "?ang trang b?" (D?u tích V)
            // C? ô nào chia h?t cho 5 thì gi? v? nh? ?ang ???c m?c
            item.isEquipped = (i % 5 == 0);

            items.Add(item);
        }

        Debug.Log("CALL REFRESH");
        UIInventory.Instance.Refresh(items);
    }
}