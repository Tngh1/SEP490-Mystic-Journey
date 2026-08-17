namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the DungeonResponse class.
    [System.Serializable]
    public class DungeonResponse
    {
        // Executes dungeon config id operation.
        public int DungeonConfigId { get; set; }
        // Executes name operation.
        public string Name { get; set; }
        // Executes description operation.
        public string Description { get; set; }
        // Dungeon type is a free-form category with Normal as the current default; the backend does not enforce a closed allowlist.
        public string Type { get; set; }
        // Executes level requirement operation.
        public int LevelRequirement { get; set; }
        // Executes max members operation.
        public int MaxMembers { get; set; }
        // Executes difficulty operation.
        public int Difficulty { get; set; }
        // Executes recommended power operation.
        public int RecommendedPower { get; set; }
        // Executes energy cost operation.
        public int EnergyCost { get; set; }
        // Executes chest id operation.
        public int? ChestId { get; set; }
        // Executes is active operation.
        public bool IsActive { get; set; }
        // Executes gold min reward operation.
        public int GoldMinReward { get; set; }
        // Executes gold max reward operation.
        public int GoldMaxReward { get; set; }
        // Executes experience reward operation.
        public int ExperienceReward { get; set; }
        // Executes possible drops operation.
        public System.Collections.Generic.List<ChestItemResponse> PossibleDrops { get; set; }
    }

    // Executes chest item response operation.
    [System.Serializable]
    public class ChestItemResponse
    {
        // Executes chest item id operation.
        public int ChestItemId { get; set; }
        // Executes chest id operation.
        public int ChestId { get; set; }
        // Executes item id operation.
        public int ItemId { get; set; }
        // Executes item name operation.
        public string ItemName { get; set; }
        // Executes item icon url operation.
        public string ItemIconUrl { get; set; }
        // Supported rarity values: Common, Uncommon, Rare, Epic, Legendary, or Mythic; rarity controls quality, visuals, and sorting priority.
        public string ItemRarity { get; set; }
        // Executes quantity min operation.
        public int QuantityMin { get; set; }
        // Executes quantity max operation.
        public int QuantityMax { get; set; }
        // Executes drop rate operation.
        public float DropRate { get; set; }
        // Executes is guaranteed operation.
        public bool IsGuaranteed { get; set; }
    }
}
