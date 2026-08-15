namespace MysticJourney.API.Models.Request
{
    [System.Serializable]
    public class PurchaseShopItemRequest
    {
        public int ShopItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    [System.Serializable]
    public class PurchaseShopSkinRequest
    {
        public int SkinId { get; set; }
    }
}

namespace MysticJourney.API.Models.Response
{
    // Player-facing shop response from /api/shop/items.
    [System.Serializable]
    public class ShopItemPublicResponse
    {
        public int ShopItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public string ItemIconUrl { get; set; }
        public string ItemType { get; set; }
        public string Rarity { get; set; }
        public string Slot { get; set; }
        public int MaxStack { get; set; }
        public string ShopSection { get; set; }
        public string Currency { get; set; }
        public decimal? OriginalPrice { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsUnlimitedStock { get; set; }
        public int DailyPurchaseLimit { get; set; }
        public int WeeklyPurchaseLimit { get; set; }
        public int PurchasedToday { get; set; }
        public int PurchasedThisWeek { get; set; }
        public int? RemainingDailyPurchases { get; set; }
        public int? RemainingWeeklyPurchases { get; set; }
        public string AvailableFrom { get; set; }
        public string AvailableTo { get; set; }
        public bool CanPurchase { get; set; }
        public string UnavailableReason { get; set; }

        // Equipment Stats
        public int BaseHp { get; set; }
        public int BaseAtk { get; set; }
        public int BaseDef { get; set; }
        public int BonusHp { get; set; }
        public int BonusAtk { get; set; }
        public int BonusDef { get; set; }
        public float BonusCritRate { get; set; }
        public float BonusCritDamage { get; set; }
    }

    [System.Serializable]
    public class ShopRefreshStatusResponse
    {
        public string ShopDateUtc { get; set; }
        public string NextResetUtc { get; set; }
        public int RefreshesUsedToday { get; set; }
        public int RefreshesRemainingToday { get; set; }
        public int MaxDailyRefreshes { get; set; }
        public bool CanRefresh { get; set; }
    }

    [System.Serializable]
    public class ShopRefreshResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ShopRefreshStatusResponse RefreshStatus { get; set; }
        public PagedResultResponse<ShopItemPublicResponse> Shop { get; set; }
    }

    [System.Serializable]
    public class PurchaseShopItemResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int PurchaseHistoryId { get; set; }
        public int ShopItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public int Quantity { get; set; }
        public string Currency { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public int InventoryQuantity { get; set; }
        public CurrencyBalanceResponse Balance { get; set; }
        public PlayerCurrencyLogResponse Transaction { get; set; }
    }

    [System.Serializable]
    public class SkinShopItemResponse
    {
        public int SkinId { get; set; }
        public string SkinName { get; set; }
        public string Description { get; set; }
        public string SkinType { get; set; }
        public string Rarity { get; set; }
        public string IconUrl { get; set; }
        public string PreviewUrl { get; set; }
        public string Currency { get; set; }
        public decimal Price { get; set; }
        public bool IsOwned { get; set; }
        public bool CanPurchase { get; set; }
        public string UnavailableReason { get; set; }
    }

    [System.Serializable]
    public class PurchaseShopSkinResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int PlayerSkinId { get; set; }
        public int SkinId { get; set; }
        public string SkinName { get; set; }
        public string Currency { get; set; }
        public decimal Price { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public CurrencyBalanceResponse Balance { get; set; }
        public PlayerCurrencyLogResponse Transaction { get; set; }
    }
}
