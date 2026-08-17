namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the AchievementResponse class.
    [System.Serializable]
    public class AchievementResponse
    {
        // Executes achievement id operation.
        public int AchievementId { get; set; }
        // Executes name operation.
        public string Name { get; set; }
        // Executes description operation.
        public string Description { get; set; }
        // Executes buff description operation.
        public string BuffDescription { get; set; }
        // Executes type operation.
        public string Type { get; set; }
        // Executes icon url operation.
        public string IconUrl { get; set; }
        // Executes required value operation.
        public int RequiredValue { get; set; }
        // Executes is active operation.
        public bool IsActive { get; set; }
        // Executes created at operation.
        public string CreatedAt { get; set; }
        // Executes reward item id operation.
        public int? RewardItemId { get; set; }
        // Executes reward item name operation.
        public string RewardItemName { get; set; }
        // Executes reward quantity operation.
        public int RewardQuantity { get; set; }
        // Executes reward gold operation.
        public decimal RewardGold { get; set; }
        // Executes reward gem operation.
        public int RewardGem { get; set; }
        // Executes reward gems operation.
        public int RewardGems
        {
            get => RewardGem;
            set => RewardGem = value;
        }
        // Executes point operation.
        public int Point { get; set; }
    }
}
