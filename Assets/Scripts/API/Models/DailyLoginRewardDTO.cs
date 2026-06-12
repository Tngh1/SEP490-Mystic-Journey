namespace MysticJourney.API.Models.Response
{
    // Maps DailyLoginRewardResponseDto
    [System.Serializable]
    public class DailyLoginRewardResponse
    {
        public int DailyLoginRewardId { get; set; }
        public int DayNumber { get; set; }
        public string RewardType { get; set; }      // "Gold", "Gems", "Item"
        public decimal RewardValue { get; set; }    // Số lượng gold/gems (nếu RewardType không phải Item)
        public int? RewardItemId { get; set; }
        public string RewardItemName { get; set; }
        public int RewardItemQuantity { get; set; }
        public bool IsActive { get; set; }
    }
}
