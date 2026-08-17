using System;

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class MonsterResponse
    {
        public int MonsterId { get; set; }
        public string Name { get; set; }
        // Supported monster types: Normal, Elite, Boss
        public string Type { get; set; }
        public string Description { get; set; }
        public int Level { get; set; }
        public int MaxHp { get; set; }
        public int Atk { get; set; }
        public int Def { get; set; }
        public int MoveSpeed { get; set; }
        public int AttackSpeed { get; set; }
        public int CritRate { get; set; }
        public int CritDamage { get; set; }
        public int ExperienceReward { get; set; }
        public decimal GoldReward { get; set; }
        public string ImageUrl { get; set; }
        public bool IsActive { get; set; }
    }

    [Serializable]
    public class MonsterDropResponse
    {
        public int MonsterDropId { get; set; }
        public int MonsterId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public double DropRate { get; set; }
        public int MinQuantity { get; set; }
        public int MaxQuantity { get; set; }
        public bool IsGuaranteed { get; set; }
        public bool IsActive { get; set; }
    }

    [Serializable]
    public class MonsterDetailResponse : MonsterResponse
    {
        public MonsterDropResponse[] MonsterDrops { get; set; }
    }

    [Serializable]
    public class MonsterSpawnResponse
    {
        public int MonsterSpawnId { get; set; }
        public int MonsterId { get; set; }
        public string MonsterName { get; set; }
        // Supported monster types: Normal, Elite, Boss
        public string MonsterType { get; set; }
        public string MapName { get; set; }
        public string RegionName { get; set; }
        public string Location { get; set; }
        public int SpawnCount { get; set; }
        public int RespawnSeconds { get; set; }
        public int? DungeonId { get; set; }
        public string DungeonName { get; set; }
        public bool IsDungeonRepeatable { get; set; }
        public bool IsActive { get; set; }
        public MonsterResponse Monster { get; set; }
    }

    [Serializable]
    public class PlayerMonsterCatalogItem
    {
        public int MonsterId { get; set; }
        public string Name { get; set; }
        // Supported monster types: Normal, Elite, Boss
        public string Type { get; set; }
        public string Description { get; set; }
        public int Level { get; set; }
        public int MaxHp { get; set; }
        public int Atk { get; set; }
        public int Def { get; set; }
        public int ExperienceReward { get; set; }
        public decimal GoldReward { get; set; }
        public string ImageUrl { get; set; }
        public bool IsDiscovered { get; set; }
        public int TimesDefeated { get; set; }
    }

    [Serializable]
    public class MonsterDroppedItem
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemIconUrl { get; set; }
        public int Quantity { get; set; }
    }

    [Serializable]
    public class MonsterDefeatResponse
    {
        public int MonsterId { get; set; }
        public string MonsterName { get; set; }
        public bool WasDiscovered { get; set; }
        public int ExperienceEarned { get; set; }
        public decimal GoldEarned { get; set; }
        public int PlayerLevel { get; set; }
        public int PlayerExperience { get; set; }
        public decimal PlayerGold { get; set; }
        public MonsterDroppedItem[] DroppedItems { get; set; }
    }
}
