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
        public string MapName { get; set; }
        public string RegionName { get; set; }
        public string ObjectiveType { get; set; }
        public string ObjectiveTarget { get; set; }
        public string ObjectiveLocation { get; set; }
        public string QuestGiverName { get; set; }
        public int RequiredLevel { get; set; }
        public int TargetAmount { get; set; }
        public int RewardExperience { get; set; }
        public decimal RewardGold { get; set; }
        public decimal RewardGems { get; set; }
        public int? RewardItemId { get; set; }
        public string RewardItemName { get; set; }
        public int? RewardSkillId { get; set; }
        public string RewardSkillName { get; set; }
        public bool IsActive { get; set; }
    }
}
