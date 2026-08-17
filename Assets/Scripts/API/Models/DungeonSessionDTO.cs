using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the EnterDungeonResponse class.
    [System.Serializable]
    public class EnterDungeonResponse
    {
        // Executes dungeon session id operation.
        public int DungeonSessionId { get; set; }
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes dungeon config id operation.
        public int DungeonConfigId { get; set; }
        // Executes dungeon name operation.
        public string DungeonName { get; set; }
        // Executes energy cost operation.
        public int EnergyCost { get; set; }
        // Executes player current energy operation.
        public int PlayerCurrentEnergy { get; set; }
        // Executes enter time operation.
        public string EnterTime { get; set; }
        // Supported dungeon session states: Active, Completed, Abandoned, Failed, Expired, or RewardClaimed; transitions control progress and reward eligibility.
        public string Status { get; set; }
        // Executes party members operation.
        public List<string> PartyMembers { get; set; }
        // Executes progress operation.
        public DungeonProgressResponse Progress { get; set; }
    }

    // Executes dungeon progress response operation.
    [System.Serializable]
    public class DungeonProgressResponse
    {
        // Executes dungeon progress id operation.
        public int DungeonProgressId { get; set; }
        // Executes dungeon session id operation.
        public int DungeonSessionId { get; set; }
        // Executes monsters killed operation.
        public int MonstersKilled { get; set; }
        // Executes boss spawned operation.
        public bool BossSpawned { get; set; }
        // Executes boss killed operation.
        public bool BossKilled { get; set; }
        // Executes elapsed time operation.
        public int ElapsedTime { get; set; }
        // Executes completion percentage operation.
        public int CompletionPercentage { get; set; }
        // Executes extra data operation.
        public string ExtraData { get; set; }
        // Executes updated at operation.
        public string UpdatedAt { get; set; }
        // Supported dungeon session states: Active, Completed, Abandoned, Failed, Expired, or RewardClaimed; transitions control progress and reward eligibility.
        public string SessionStatus { get; set; }
    }

    // Executes complete dungeon response operation.
    [System.Serializable]
    public class CompleteDungeonResponse
    {
        // Executes dungeon session id operation.
        public int DungeonSessionId { get; set; }
        // Supported dungeon session states: Active, Completed, Abandoned, Failed, Expired, or RewardClaimed; transitions control progress and reward eligibility.
        public string Status { get; set; }
        // Executes completed time operation.
        public string CompletedTime { get; set; }
        // Executes reward chest operation.
        public ChestPreviewResponse RewardChest { get; set; }
        // Executes message operation.
        public string Message { get; set; }
    }

    // Executes chest preview response operation.
    [System.Serializable]
    public class ChestPreviewResponse
    {
        // Executes chest id operation.
        public int ChestId { get; set; }
        // Executes name operation.
        public string Name { get; set; }
        // Executes description operation.
        public string Description { get; set; }
        // Chest type is a free-form category with Common as the current default; the backend does not enforce a closed allowlist.
        public string Type { get; set; }
        // Executes gold min reward operation.
        public int GoldMinReward { get; set; }
        // Executes gold max reward operation.
        public int GoldMaxReward { get; set; }
        // Executes experience reward operation.
        public int ExperienceReward { get; set; }
        // Executes chest items operation.
        public ChestItemInfo[] ChestItems { get; set; }
    }

    // Executes chest item info operation.
    [System.Serializable]
    public class ChestItemInfo
    {
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

    // Executes claim dungeon reward response operation.
    [System.Serializable]
    public class ClaimDungeonRewardResponse
    {
        // Executes dungeon session id operation.
        public int DungeonSessionId { get; set; }
        // Executes success operation.
        public bool Success { get; set; }
        // Executes message operation.
        public string Message { get; set; }
        // Executes energy consumed operation.
        public int EnergyConsumed { get; set; }
        // Executes gold earned operation.
        public int GoldEarned { get; set; }
        // Executes experience earned operation.
        public int ExperienceEarned { get; set; }
        // Executes time taken seconds operation.
        public float TimeTakenSeconds { get; set; }
        // Executes items operation.
        public DungeonRewardItemResponse[] Items { get; set; }
        // Executes wallet operation.
        public WalletDto Wallet { get; set; }
        // Executes character operation.
        public CharacterDto Character { get; set; }
    }

    // Executes wallet dto operation.
    [System.Serializable]
    public class WalletDto
    {
        // Executes gold operation.
        public decimal Gold { get; set; }
        // Executes gems operation.
        public decimal Gems { get; set; }
    }

    // Executes character dto operation.
    [System.Serializable]
    public class CharacterDto
    {
        // Executes level operation.
        public int Level { get; set; }
        // Executes experience points operation.
        public int ExperiencePoints { get; set; }
        // Executes energy operation.
        public int Energy { get; set; }
        // Executes max energy operation.
        public int MaxEnergy { get; set; }
    }

    // Executes dungeon reward item response operation.
    [System.Serializable]
    public class DungeonRewardItemResponse
    {
        // Executes item id operation.
        public int ItemId { get; set; }
        // Executes item name operation.
        public string ItemName { get; set; }
        // Executes item icon url operation.
        public string ItemIconUrl { get; set; }
        // Executes item type operation.
        public string ItemType { get; set; }
        // Supported rarity values: Common, Uncommon, Rare, Epic, Legendary, or Mythic; rarity controls quality, visuals, and sorting priority.
        public string Rarity { get; set; }
        // Executes quantity operation.
        public int Quantity { get; set; }
    }

}

namespace MysticJourney.API.Models.Request
{
    // Executes update dungeon progress request operation.
    [System.Serializable]
    public class UpdateDungeonProgressRequest
    {
        // Executes monsters killed operation.
        public int MonstersKilled { get; set; }
        // Executes boss spawned operation.
        public bool BossSpawned { get; set; }
        // Executes boss killed operation.
        public bool BossKilled { get; set; }
        // Executes elapsed time operation.
        public int ElapsedTime { get; set; }

        // Executes completion percentage operation.
        public int CompletionPercentage { get; set; }

        // Executes extra data operation.
        public string ExtraData { get; set; }
    }

    // Executes enter dungeon request operation.
    [System.Serializable]
    public class EnterDungeonRequest
    {
        // Executes party members operation.
        public List<string> PartyMembers { get; set; }
    }
}
