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

            // 1. Gán ID và Số lượng ngẫu nhiên
            item.itemId = 1;
            item.itemName = "Iron Sword";
            item.category = "Weapon";
            item.quantity = Random.Range(1, 99);

            // 2. [QUAN TRỌNG] Lấy Icon từ Database. Nếu null, lưới sẽ không vẽ hình.
            item.icon = ItemIconDatabase.Instance.GetIcon(item.itemName, item.category);

            // 3. Test đổ màu viền (Phẩm chất ngẫu nhiên)
            string[] rarities = { "common", "rare", "epic", "legendary" };
            item.rarity = rarities[Random.Range(0, rarities.Length)];

            // 4. Test hiển thị Icon "đang trang bị" (Dấu tích V)
            // Cứ ô nào chia hết cho 5 thì giả vờ như đang được mặc
            item.isEquipped = (i % 5 == 0);

            items.Add(item);
        }

        Debug.Log("CALL REFRESH");
        UIInventory.Instance.Refresh(items);
    }
}
