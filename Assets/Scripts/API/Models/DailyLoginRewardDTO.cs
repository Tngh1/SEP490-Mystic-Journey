namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the DailyLoginRewardResponse class.
    [System.Serializable]
    public class DailyLoginRewardResponse
    {
        // Executes daily login reward id operation.
        public int DailyLoginRewardId { get; set; }
        // Executes day number operation.
        public int DayNumber { get; set; }
        // Supported reward types: Gold, Gems, EXP, Energy, or Item; Item rewards also require an item identifier and quantity.
        public string RewardType { get; set; }
        // Executes reward value operation.
        public decimal RewardValue { get; set; }
        // Executes reward item id operation.
        public int? RewardItemId { get; set; }
        // Executes reward item name operation.
        public string RewardItemName { get; set; }
        // Executes reward item rarity operation.
        public string RewardItemRarity { get; set; }
        // Executes reward item type operation.
        public string RewardItemType { get; set; }
        // Executes reward item quantity operation.
        public int RewardItemQuantity { get; set; }
        // Executes is active operation.
        public bool IsActive { get; set; }
    }
}
