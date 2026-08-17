namespace MysticJourney.API.Models.Request
{
    // Initializes a new default instance of the PurchaseShopItemRequest class.
    [System.Serializable]
    public class PurchaseShopItemRequest
    {
        // Executes shop item id operation.
        public int ShopItemId { get; set; }
        // Executes quantity operation.
        public int Quantity { get; set; } = 1;
    }

    // Executes purchase shop skin request operation.
    [System.Serializable]
    public class PurchaseShopSkinRequest
    {
        // Executes skin id operation.
        public int SkinId { get; set; }
    }
}

namespace MysticJourney.API.Models.Response
{
    // Executes shop item public response operation.
    [System.Serializable]
    public class ShopItemPublicResponse
    {
        // Executes shop item id operation.
        public int ShopItemId { get; set; }
        // Executes item id operation.
        public int ItemId { get; set; }
        // Executes item name operation.
        public string ItemName { get; set; }
        // Executes description operation.
        public string Description { get; set; }
        // Executes item icon url operation.
        public string ItemIconUrl { get; set; }
        // Executes item type operation.
        public string ItemType { get; set; }
        // Supported rarity values: Common, Uncommon, Rare, Epic, Legendary, or Mythic; rarity controls quality, visuals, and sorting priority.
        public string Rarity { get; set; }
        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        public string Slot { get; set; }
        // Executes max stack operation.
        public int MaxStack { get; set; }
        // Executes shop section operation.
        public string ShopSection { get; set; }
        // Supported currencies: Gold or Gems; the selected currency determines which player balance is charged or credited.
        public string Currency { get; set; }
        // Executes original price operation.
        public decimal? OriginalPrice { get; set; }
        // Executes price operation.
        public decimal Price { get; set; }
        // Executes stock operation.
        public int Stock { get; set; }
        // Executes is unlimited stock operation.
        public bool IsUnlimitedStock { get; set; }
        // Executes daily purchase limit operation.
        public int DailyPurchaseLimit { get; set; }
        // Executes weekly purchase limit operation.
        public int WeeklyPurchaseLimit { get; set; }
        // Executes purchased today operation.
        public int PurchasedToday { get; set; }
        // Executes purchased this week operation.
        public int PurchasedThisWeek { get; set; }
        // Executes remaining daily purchases operation.
        public int? RemainingDailyPurchases { get; set; }
        // Executes remaining weekly purchases operation.
        public int? RemainingWeeklyPurchases { get; set; }
        // Executes available from operation.
        public string AvailableFrom { get; set; }
        // Executes available to operation.
        public string AvailableTo { get; set; }
        // Executes can purchase operation.
        public bool CanPurchase { get; set; }
        // Executes unavailable reason operation.
        public string UnavailableReason { get; set; }

        // Executes base hp operation.
        public int BaseHp { get; set; }
        // Executes base atk operation.
        public int BaseAtk { get; set; }
        // Executes base def operation.
        public int BaseDef { get; set; }
        // Executes bonus hp operation.
        public int BonusHp { get; set; }
        // Executes bonus atk operation.
        public int BonusAtk { get; set; }
        // Executes bonus def operation.
        public int BonusDef { get; set; }
        // Executes bonus crit rate operation.
        public float BonusCritRate { get; set; }
        // Executes bonus crit damage operation.
        public float BonusCritDamage { get; set; }
    }

    // Executes shop refresh status response operation.
    [System.Serializable]
    public class ShopRefreshStatusResponse
    {
        // Executes shop date utc operation.
        public string ShopDateUtc { get; set; }
        // Executes next reset utc operation.
        public string NextResetUtc { get; set; }
        // Executes refreshes used today operation.
        public int RefreshesUsedToday { get; set; }
        // Executes refreshes remaining today operation.
        public int RefreshesRemainingToday { get; set; }
        // Executes max daily refreshes operation.
        public int MaxDailyRefreshes { get; set; }
        // Executes can refresh operation.
        public bool CanRefresh { get; set; }
    }

    // Executes shop refresh response operation.
    [System.Serializable]
    public class ShopRefreshResponse
    {
        // Executes success operation.
        public bool Success { get; set; }
        // Executes message operation.
        public string Message { get; set; }
        // Executes refresh status operation.
        public ShopRefreshStatusResponse RefreshStatus { get; set; }
        // Executes shop operation.
        public PagedResultResponse<ShopItemPublicResponse> Shop { get; set; }
    }

    // Executes purchase shop item response operation.
    [System.Serializable]
    public class PurchaseShopItemResponse
    {
        // Executes success operation.
        public bool Success { get; set; }
        // Executes message operation.
        public string Message { get; set; }
        // Executes purchase history id operation.
        public int PurchaseHistoryId { get; set; }
        // Executes shop item id operation.
        public int ShopItemId { get; set; }
        // Executes item id operation.
        public int ItemId { get; set; }
        // Executes item name operation.
        public string ItemName { get; set; }
        // Executes quantity operation.
        public int Quantity { get; set; }
        // Supported currencies: Gold or Gems; the selected currency determines which player balance is charged or credited.
        public string Currency { get; set; }
        // Executes unit price operation.
        public decimal UnitPrice { get; set; }
        // Executes total price operation.
        public decimal TotalPrice { get; set; }
        // Executes balance before operation.
        public decimal BalanceBefore { get; set; }
        // Executes balance after operation.
        public decimal BalanceAfter { get; set; }
        // Executes inventory quantity operation.
        public int InventoryQuantity { get; set; }
        // Executes balance operation.
        public CurrencyBalanceResponse Balance { get; set; }
        // Executes transaction operation.
        public PlayerCurrencyLogResponse Transaction { get; set; }
    }

    // Executes skin shop item response operation.
    [System.Serializable]
    public class SkinShopItemResponse
    {
        // Executes skin id operation.
        public int SkinId { get; set; }
        // Executes skin name operation.
        public string SkinName { get; set; }
        // Executes description operation.
        public string Description { get; set; }
        // Supported skin types include Armor and FullSet; the value identifies how the cosmetic is grouped and equipped.
        public string SkinType { get; set; }
        // Supported rarity values: Common, Uncommon, Rare, Epic, Legendary, or Mythic; rarity controls quality, visuals, and sorting priority.
        public string Rarity { get; set; }
        // Executes icon url operation.
        public string IconUrl { get; set; }
        // Executes preview url operation.
        public string PreviewUrl { get; set; }
        // Supported currencies: Gold or Gems; the selected currency determines which player balance is charged or credited.
        public string Currency { get; set; }
        // Executes price operation.
        public decimal Price { get; set; }
        // Executes is owned operation.
        public bool IsOwned { get; set; }
        // Executes can purchase operation.
        public bool CanPurchase { get; set; }
        // Executes unavailable reason operation.
        public string UnavailableReason { get; set; }
    }

    // Executes purchase shop skin response operation.
    [System.Serializable]
    public class PurchaseShopSkinResponse
    {
        // Executes success operation.
        public bool Success { get; set; }
        // Executes message operation.
        public string Message { get; set; }
        // Executes player skin id operation.
        public int PlayerSkinId { get; set; }
        // Executes skin id operation.
        public int SkinId { get; set; }
        // Executes skin name operation.
        public string SkinName { get; set; }
        // Supported currencies: Gold or Gems; the selected currency determines which player balance is charged or credited.
        public string Currency { get; set; }
        // Executes price operation.
        public decimal Price { get; set; }
        // Executes balance before operation.
        public decimal BalanceBefore { get; set; }
        // Executes balance after operation.
        public decimal BalanceAfter { get; set; }
        // Executes balance operation.
        public CurrencyBalanceResponse Balance { get; set; }
        // Executes transaction operation.
        public PlayerCurrencyLogResponse Transaction { get; set; }
    }
}
