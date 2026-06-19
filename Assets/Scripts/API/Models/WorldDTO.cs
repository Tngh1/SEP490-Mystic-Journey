using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class PlayerWorldPositionResponse
    {
        public string MapName { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }

    [Serializable]
    public class WorldMapProgressResponse
    {
        public string MapName { get; set; }
        public string DisplayName { get; set; }
        public bool IsUnlocked { get; set; }
        public int ExplorationPercent { get; set; }
    }

    [Serializable]
    public class WorldStateResponse
    {
        public int PlayerProfileId { get; set; }
        public PlayerWorldPositionResponse Position { get; set; }
        public List<WorldMapProgressResponse> Maps { get; set; }
        public List<NPCResponse> Npcs { get; set; }
        public List<PlayerQuestResponse> Quests { get; set; }
        public PlayerQuestResponse ActiveQuest { get; set; }
        public PlayerDailyLoginResponse DailyLogin { get; set; }
    }

    [Serializable]
    public class NPCResponse
    {
        public int NPCId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string MapName { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public float InteractionRadius { get; set; }
        public string IconUrl { get; set; }
        public bool IsActive { get; set; }
        public List<NPCDialogueResponse> Dialogues { get; set; }
    }

    [Serializable]
    public class NPCDialogueResponse
    {
        public int NPCDialogueId { get; set; }
        public int NPCId { get; set; }
        public string NPCName { get; set; }
        public string Content { get; set; }
        public string ResponseType { get; set; }
        public int? LinkedQuestId { get; set; }
        public string LinkedQuestTitle { get; set; }
        public int? LinkedShopItemId { get; set; }
        public string LinkedShopItemName { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; }
    }

    [Serializable]
    public class TalkToNpcResponse
    {
        public NPCResponse Npc { get; set; }
        public List<PlayerQuestResponse> LinkedQuests { get; set; }
    }

    [Serializable]
    public class InteractObjectResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public PlayerQuestResponse Quest { get; set; }
    }

    [Serializable]
    public class PlayerDailyLoginResponse
    {
        public int PlayerDailyLoginId { get; set; }
        public int PlayerProfileId { get; set; }
        public int CurrentStreak { get; set; }
        public int TotalDaysClaimed { get; set; }
        public string LastClaimedAt { get; set; }
        public bool IsClaimedToday { get; set; }
    }

    [Serializable]
    public class ClaimDailyRewardResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int CurrentStreak { get; set; }
        public int TotalDaysClaimed { get; set; }
        public string RewardType { get; set; }
        public decimal RewardValue { get; set; }
        public int? RewardItemId { get; set; }
        public string RewardItemName { get; set; }
        public int RewardItemQuantity { get; set; }
    }

    [Serializable]
    public class OpenChestResponse
    {
        public bool Success { get; set; }
        public int GoldEarned { get; set; }
        public int ExperienceEarned { get; set; }
        public List<ChestOpenedItemResponse> Items { get; set; }
    }

    [Serializable]
    public class ChestOpenedItemResponse
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemIconUrl { get; set; }
        public string Rarity { get; set; }
        public int Quantity { get; set; }
    }
}

namespace MysticJourney.API.Models.Request
{
    [Serializable]
    public class UpdateWorldPositionRequest
    {
        public string MapName { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
    }

    [Serializable]
    public class TalkToNpcRequest
    {
        public int NPCId { get; set; }
    }

    [Serializable]
    public class InteractObjectRequest
    {
        public string MapName { get; set; }
        public string ObjectKey { get; set; }
        public string InteractionType { get; set; }
        public int? QuestId { get; set; }
        public int ProgressDelta { get; set; } = 1;
    }

    [Serializable]
    public class OpenWorldChestRequest
    {
        public int? ChestId { get; set; }
        public int? PlayerChestId { get; set; }
    }
}
