using System;
using System.Collections.Generic;
using UnityEngine;

public class UIShop : MonoBehaviour
{
    public static UIShop Instance;

    [Header("Shop UI Settings")]
    [Tooltip("Kéo Prefab New_ShopSlot t? th? m?c Project vào ?ây")]
    [SerializeField] private UIShopSlot shopSlotPrefab;

    [Tooltip("Kéo GameObject Content (ch?a Grid Layout Group) vào ?ây")]
    [SerializeField] private Transform contentParent;

    // Kho ch?a Object Pool cho C?a hàng
    private List<UIShopSlot> slots = new List<UIShopSlot>();

    // Tr?m trung chuy?n: Báo ra ngoài khi Mira b?m nút "Mua"
    public Action<UIBaseItemSlot> OnShopItemClicked;

    private void Awake()
    {
        // Thi?t l?p Singleton ?? ShopTest có th? g?i UIShop.Instance
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void RefreshShop(List<UIItemDisplayData> shopItems)
    {
        for (int i = 0; i < shopItems.Count; i++)
        {
            if (i >= slots.Count)
            {
                // N?u thi?u Slot thì t?o m?i
                UIShopSlot newSlot = Instantiate(shopSlotPrefab, contentParent);

                // ??ng ký l?ng nghe s? ki?n Click (B?m nút Buy)
                newSlot.OnSlotClicked += HandleSlotClicked;
                newSlot.transform.localScale = Vector3.one;
                slots.Add(newSlot);
            }

            // B?t ô lên và n?p d? li?u vào
            slots[i].gameObject.SetActive(true);
            slots[i].SetupShop(shopItems[i]);
        }

        // T?t (gi?u ?i) các ô d? th?a n?u danh sách ?? ng?n h?n s? slot ?ang có
        for (int i = shopItems.Count; i < slots.Count; i++)
        {
            slots[i].ClearSlot();
            slots[i].gameObject.SetActive(false);
        }
    }

    private void HandleSlotClicked(UIBaseItemSlot clickedSlot)
    {
        // ?o?n này s? ch?y khi nút BuyButton trong Slot ???c b?m
        // L?y l?i d? li?u hi?n th? (ch?a Tên, Giá...)
        UIItemDisplayData data = (UIItemDisplayData)clickedSlot.RawData;

        Debug.Log("Mira v?a b?m MUA món: " + data.itemName + " v?i giá " + data.price);

        // B?n s? ki?n ra ngoài (dành cho API tr? ti?n sau này)
        OnShopItemClicked?.Invoke(clickedSlot);
    }
}