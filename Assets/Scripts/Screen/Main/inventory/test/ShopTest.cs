using System.Collections.Generic;
using UnityEngine;

public class ShopTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("SHOP TEST START");

        List<UIItemDisplayData> shopItems = new List<UIItemDisplayData>();

        // Kho d? li?u gi? ?? test hi?n th?
        string[] rarities = { "common", "rare", "epic", "legendary" };
        string[] fakeNames = {
            "Bình", "Ki", "K? S?i",
            "Già", "M", "Nh?", "B? Làng"
        };

        // Sinh ra 15 món ?? (?? ?? cái Content Size Fitter kéo dài thanh cu?n xu?ng)
        for (int i = 0; i < 15; i++)
        {
            UIItemDisplayData item = new UIItemDisplayData();

            // 1. Thông tin c? b?n
            item.itemId = Random.Range(1, 3); // Gi? s? database ?ang có ID t? 1 ??n 5
            item.itemName = fakeNames[Random.Range(0, fakeNames.Length)] + " +" + Random.Range(1, 9);
           
            item.price = Random.Range(100, 5000); // Giá ti?n t? 100 ??n 5000 vàng
           

            // 2. L?y hình ?nh v?t ph?m t? Database hi?n có
            item.icon = ItemIconDatabase.Instance.GetIcon(item.itemId);

            // Ghi chú: N?u Mira có m?t Sprite icon ??ng Vàng riêng, có th? gán vào ?ây
            // item.currencyIcon = Resources.Load<Sprite>("Icons/GoldCoin");

            shopItems.Add(item);
        }

        Debug.Log("CALL REFRESH SHOP: " + shopItems.Count + " items");

        // ?? toàn b? d? li?u vào l??i Shop
        UIShop.Instance.RefreshShop(shopItems);
    }
}