namespace MysticJourney.API.Models.Response
{
    // Maps AchievementResponseDto
    [System.Serializable]
    public class AchievementResponse
    {
        public int AchievementId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }            // "Combat", "Exploration", "Social"
        public string IconUrl { get; set; }
        public int RequiredValue { get; set; }      // Ngưỡng để đạt thành tích
        public bool IsActive { get; set; }
        public string CreatedAt { get; set; }
        public int? RewardItemId { get; set; }
        public string RewardItemName { get; set; }
        public int RewardQuantity { get; set; }
        public decimal RewardGold { get; set; }
        public int RewardGem { get; set; }          // Chú ý: "Gem" không phải "Gems"
        public int Point { get; set; }
    }
}
