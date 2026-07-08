namespace MysticJourney.API.Models.Request
{
    [System.Serializable]
    public class PurchaseShopItemRequest
    {
        public int ShopItemId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}

namespace MysticJourney.API.Models.Response
{
    // Admin/catalog response from /api/shopitems.
    [System.Serializable]
    public class ShopItemResponse
    {
        public int ShopItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemIconUrl { get; set; }
        public string ItemType { get; set; }
        public string Currency { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public int DailyPurchaseLimit { get; set; }
        public int WeeklyPurchaseLimit { get; set; }
        public bool IsActive { get; set; }
        public string AvailableFrom { get; set; }
        public string AvailableTo { get; set; }
    }

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
        public string Currency { get; set; }
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
}
