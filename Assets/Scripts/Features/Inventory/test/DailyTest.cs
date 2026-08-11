using System.Collections.Generic;
using UnityEngine;

public class DailyTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("DAILY LOGIN TEST START");

        List<UIItemDisplayData> dailyItems = new List<UIItemDisplayData>();

        // Kho dữ liệu giả để test hiển thị. Tên phải khớp itemKey trong ItemIconDatabase,
        // nếu không GetIcon trả null và ô quà sẽ trống.
        string[] fakeNames = { "Gold", "Gem", "Small Health Potion", "Skill Upgrade Stone", "Lucky Ticket" };
        string[] fakeTypes = { "Currency", "Currency", "Consumable", "Material", "QuestItem" };
        string[] rarities = { "common", "rare", "epic", "legendary" };

        // Sinh ra chẵn 30 ngày điểm danh
        for (int i = 1; i <= 30; i++)
        {
            UIItemDisplayData item = new UIItemDisplayData();

            // 1. Thông tin cơ bản
            int pick = Random.Range(0, fakeNames.Length);
            item.dayNumber = i;
            item.itemId = pick + 1;
            item.itemName = fakeNames[pick];
            item.category = fakeTypes[pick];
            item.rarity = rarities[Random.Range(0, rarities.Length)];

            // 2. Setup số lượng: Các ngày chia hết cho 7 (cuối tuần) sẽ được nhiều đồ hơn
            if (i % 7 == 0)
            {
                item.quantity = Random.Range(100, 500);
            }
            else
            {
                item.quantity = Random.Range(2, 20);
            }

            // 3. Giả lập tình trạng nhận quà (5 ngày đầu đã nhận, ngày 6 trở đi chưa nhận)
            item.isClaimed = (i <= 5);

            // 4. Gắn hình ảnh từ Database hiện có
            item.icon = ItemIconDatabase.Instance.GetIcon(item.itemName, item.category);

            // 5. Lưu lại object gốc để sau này cái hàm HandleSlotClicked lấy ra đọc dùng Debug.Log
            item.rawData = item;

            dailyItems.Add(item);
        }

        Debug.Log("CALL REFRESH DAILY: " + dailyItems.Count + " days");

        // Đổ toàn bộ dữ liệu vào bảng điểm danh
        UIDailyLogin.Instance.RefreshDaily(dailyItems);
    }
}
