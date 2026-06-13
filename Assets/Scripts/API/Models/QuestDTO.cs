namespace MysticJourney.API.Models.Response
{
    // Maps QuestResponseDto
    [System.Serializable]
    public class QuestResponse
    {
        public int QuestId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }            // "Main", "Side", "Daily"
        public string DefaultStatus { get; set; }   // "NotStarted", "InProgress"
        public int RequiredLevel { get; set; }
        public int RewardExperience { get; set; }
        public decimal RewardGold { get; set; }
        public decimal RewardGems { get; set; }
        public int? RewardItemId { get; set; }
        public string RewardItemName { get; set; }
        public bool IsActive { get; set; }
    }
}
