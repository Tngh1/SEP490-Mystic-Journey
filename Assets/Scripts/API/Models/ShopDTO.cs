namespace MysticJourney.API.Models.Response
{
    // Maps ShopItemResponseDto
    [System.Serializable]
    public class ShopItemResponse
    {
        public int ShopItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemIconUrl { get; set; }
        public string ItemType { get; set; }
        public string Currency { get; set; }        // "Gold" hoặc "Gems"
        public decimal Price { get; set; }
        public int Stock { get; set; }              // -1 = không giới hạn
        public int DailyPurchaseLimit { get; set; }
        public bool IsActive { get; set; }
        public string AvailableFrom { get; set; }
        public string AvailableTo { get; set; }
    }
}
