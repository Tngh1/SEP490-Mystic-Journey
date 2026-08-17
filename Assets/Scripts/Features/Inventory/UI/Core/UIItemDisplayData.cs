using UnityEngine;

// Initializes a new default instance of the UIItemDisplayData class.
[System.Serializable]
public class UIItemDisplayData
{
    public int itemId;
    public string itemName;
    public Sprite icon;
    public int quantity;
    // Supported rarity values: Common, Uncommon, Rare, Epic, Legendary, or Mythic; rarity controls quality, visuals, and sorting priority.
    public string rarity;
    public string category;

    public bool isEquipped;

    public int price;
    public decimal unitPrice;
    public decimal originalUnitPrice;
    // Supported currencies: Gold or Gems; the selected currency determines which player balance is charged or credited.
    public string currency = "Gold";
    public Sprite currencyIcon;

    public int shopItemId;
    public int skinId;
    public bool isSkin;
    public string shopSection;
    public bool canPurchase = true;
    public string unavailableReason;
    public int stock = -1;
    public bool isUnlimitedStock = true;
    public int dailyPurchaseLimit;
    public int purchasedToday;
    public int remainingDailyPurchases = -1;
    public int weeklyPurchaseLimit;
    public int purchasedThisWeek;
    public int remainingWeeklyPurchases = -1;
    public float corruptionReduction;

    public int baseHp;
    public int baseAtk;
    public int baseDef;
    public int bonusHp;
    public int bonusAtk;
    public int bonusDef;
    public float bonusCritRate;
    public float bonusCritDamage;
    public string description;
    // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
    public string slot;

    public bool isClaimed;
    public bool isAvailable;
    public bool isMissed;
    public int dayNumber;

    public object rawData;

    // Executes effective unit price operation.
    public decimal EffectiveUnitPrice => unitPrice > 0 ? unitPrice : price;
    // Executes has deal price operation.
    public bool HasDealPrice => originalUnitPrice > EffectiveUnitPrice && EffectiveUnitPrice > 0;

    // Executes get max purchase quantity operation.
    public int GetMaxPurchaseQuantity(int hardCap = 99)
    {
        if (isSkin)
            return canPurchase ? 1 : 0;

        int max = Mathf.Max(1, hardCap);

        if (!isUnlimitedStock && stock >= 0)
            max = Mathf.Min(max, stock);

        if (remainingDailyPurchases >= 0)
            max = Mathf.Min(max, remainingDailyPurchases);

        if (remainingWeeklyPurchases >= 0)
            max = Mathf.Min(max, remainingWeeklyPurchases);

        return canPurchase ? Mathf.Max(0, max) : 0;
    }
}
