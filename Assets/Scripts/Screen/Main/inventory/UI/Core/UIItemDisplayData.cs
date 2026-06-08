using UnityEngine;

[System.Serializable]
public class UIItemDisplayData
{
    // D? li?u b?t bu?c (Lõi)
    public int itemId;
    public Sprite icon;
    public int quantity;
    public string rarity;

    // D? li?u cho Túi ??
    public bool isEquipped;

    // D? li?u cho Shop
    public int price;
    public Sprite currencyIcon;

    // D? li?u cho Quest / Chest / Daily Login
    public bool isClaimed;
    public int dayNumber; // Dành riêng cho Daily Login

    // Ch?a d? li?u g?c t? API (.NET) ?? ép ki?u khi x? lý logic
    public object rawData;
}