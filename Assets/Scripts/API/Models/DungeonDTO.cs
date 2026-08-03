namespace MysticJourney.API.Models.Response
{
    // Maps DungeonConfigResponseDto
    [System.Serializable]
    public class DungeonResponse
    {
        public int DungeonConfigId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }            // "Normal", "Elite", "Boss"
        public int LevelRequirement { get; set; }
        public int MaxMembers { get; set; }
        public int Difficulty { get; set; }         // 1–5
        public int RecommendedPower { get; set; }
        public int EnergyCost { get; set; }         // Energy để enter + claim reward
        public int? ChestId { get; set; }
        public bool IsActive { get; set; }
        public int GoldMinReward { get; set; }
        public int GoldMaxReward { get; set; }
        public int ExperienceReward { get; set; }
        public System.Collections.Generic.List<ChestItemResponse> PossibleDrops { get; set; }
    }

    [System.Serializable]
    public class ChestItemResponse
    {
        public int ChestItemId { get; set; }
        public int ChestId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemIconUrl { get; set; }
        public string ItemRarity { get; set; }
        public int QuantityMin { get; set; }
        public int QuantityMax { get; set; }
        public float DropRate { get; set; }
        public bool IsGuaranteed { get; set; }
    }
}
