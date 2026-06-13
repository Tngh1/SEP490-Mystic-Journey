using System.Collections.Generic;
using UnityEngine;

public class DailyTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("DAILY LOGIN TEST START");

        List<UIItemDisplayData> dailyItems = new List<UIItemDisplayData>();

        // Kho d? li?u gi? ?? test hi?n th?
        string[] fakeNames = { "??ng Vàng", "Kim C??ng", "Bình Máu N??c Lã", "M?nh T??ng", "Vé Gacha" };
        string[] rarities = { "common", "rare", "epic", "legendary" };

        // Sinh ra ch?n 30 ngày ?i?m danh
        for (int i = 1; i <= 30; i++)
        {
            UIItemDisplayData item = new UIItemDisplayData();

            // 1. Thông tin c? b?n
            item.dayNumber = i;
            item.itemId = Random.Range(1, 3); // Random ID t? 1 ??n 5 ?? l?y hình
            item.itemName = fakeNames[Random.Range(0, fakeNames.Length)];
            item.rarity = rarities[Random.Range(0, rarities.Length)];

            // 2. Setup s? l??ng: Các ngày chia h?t cho 7 (cu?i tu?n) s? ???c nhi?u ?? h?n
            if (i % 7 == 0)
            {
                item.quantity = Random.Range(100, 500);
            }
            else
            {
                item.quantity = Random.Range(2, 20);
            }

            // 3. Gi? l?p tình tr?ng nh?n quà (5 ngày ??u ?ã nh?n, ngày 6 tr? ?i ch?a nh?n)
            item.isClaimed = (i <= 5);

            // 4. G?n hình ?nh t? Database hi?n có
            item.icon = ItemIconDatabase.Instance.GetIcon(item.itemId);

            // 5. L?u l?i object g?c ?? sau này cái hàm HandleSlotClicked l?y ra ??c dòng Debug.Log
            item.rawData = item;

            dailyItems.Add(item);
        }

        Debug.Log("CALL REFRESH DAILY: " + dailyItems.Count + " days");

        // ?? toàn b? d? li?u vào b?ng ?i?m danh
        UIDailyLogin.Instance.RefreshDaily(dailyItems);
    }
}