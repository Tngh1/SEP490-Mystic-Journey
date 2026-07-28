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
    }
}
