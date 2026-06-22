using UnityEngine;

[System.Serializable]
public class UIItemDisplayData
{
    // D? li?u b?t bu?c (L�i)
    public int itemId;
    public string itemName; // ---> M?I TH�M: ?? hi?n th? t�n trong Shop
    public Sprite icon;
    public int quantity;
    public string rarity;

    // D? li?u cho T�i ??
    public bool isEquipped;

    // D? li?u cho Shop
    public int price;
    public Sprite currencyIcon;

    // D? li?u cho Quest / Chest / Daily Login
    public bool isClaimed;
    public bool isAvailable = true;
    public int dayNumber;

    // Ch?a d? li?u g?c t? API
    public object rawData;
}