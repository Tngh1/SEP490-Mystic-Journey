using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the PlayerWorldPositionResponse class.
    [Serializable]
    public class PlayerWorldPositionResponse
    {
        // Executes map name operation.
        public string MapName { get; set; }
        // Executes position x operation.
        public double PositionX { get; set; }
        // Executes position y operation.
        public double PositionY { get; set; }
    }

    // Executes world map progress response operation.
    [Serializable]
    public class WorldMapProgressResponse
    {
        // Executes map name operation.
        public string MapName { get; set; }
        // Executes display name operation.
        public string DisplayName { get; set; }
        // Executes is unlocked operation.
        public bool IsUnlocked { get; set; }
        // Executes exploration percent operation.
        public int ExplorationPercent { get; set; }
    }

    // Executes world state response operation.
    [Serializable]
    public class WorldStateResponse
    {
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes position operation.
        public PlayerWorldPositionResponse Position { get; set; }
        // Executes maps operation.
        public List<WorldMapProgressResponse> Maps { get; set; }
        // Executes npcs operation.
        public List<NPCResponse> Npcs { get; set; }
        // Executes quests operation.
        public List<PlayerQuestResponse> Quests { get; set; }
        // Executes active quest operation.
        public PlayerQuestResponse ActiveQuest { get; set; }
        // Executes daily login operation.
        public PlayerDailyLoginResponse DailyLogin { get; set; }
    }

    // Executes npc response operation.
    [Serializable]
    public class NPCResponse
    {
        // Executes npc id operation.
        public int NPCId { get; set; }
        // Executes name operation.
        public string Name { get; set; }
        // Executes description operation.
        public string Description { get; set; }
        // NPC type is a free-form category with Information as the current default; the backend does not enforce a closed allowlist.
        public string Type { get; set; }
        // Executes map name operation.
        public string MapName { get; set; }
        // Executes position x operation.
        public double PositionX { get; set; }
        // Executes position y operation.
        public double PositionY { get; set; }
        // Executes interaction radius operation.
        public float InteractionRadius { get; set; }
        // Executes icon url operation.
        public string IconUrl { get; set; }
        // Executes is active operation.
        public bool IsActive { get; set; }
        // Executes dialogues operation.
        public List<NPCDialogueResponse> Dialogues { get; set; }
    }

    // Executes npc dialogue response operation.
    [Serializable]
    public class NPCDialogueResponse
    {
        // Executes npc dialogue id operation.
        public int NPCDialogueId { get; set; }
        // Executes npc id operation.
        public int NPCId { get; set; }
        // Executes npc name operation.
        public string NPCName { get; set; }
        // Executes content operation.
        public string Content { get; set; }
        // Executes response type operation.
        public string ResponseType { get; set; }
        // Executes linked quest id operation.
        public int? LinkedQuestId { get; set; }
        // Executes linked quest title operation.
        public string LinkedQuestTitle { get; set; }
        // Executes linked shop item id operation.
        public int? LinkedShopItemId { get; set; }
        // Executes linked shop item name operation.
        public string LinkedShopItemName { get; set; }
        // Executes display order operation.
        public int DisplayOrder { get; set; }
        // Executes is active operation.
        public bool IsActive { get; set; }
    }

    // Executes talk to npc response operation.
    [Serializable]
    public class TalkToNpcResponse
    {
        // Executes npc operation.
        public NPCResponse Npc { get; set; }
        // Executes linked quests operation.
        public List<PlayerQuestResponse> LinkedQuests { get; set; }
    }

    // Executes interact object response operation.
    [Serializable]
    public class InteractObjectResponse
    {
        // Executes success operation.
        public bool Success { get; set; }
        // Executes message operation.
        public string Message { get; set; }
        // Executes quest operation.
        public PlayerQuestResponse Quest { get; set; }
        // Executes collected item id operation.
        public int? CollectedItemId { get; set; }
        // Executes collected item name operation.
        public string CollectedItemName { get; set; }
        // Executes collected quantity operation.
        public int CollectedQuantity { get; set; }
    }

    // Executes turn in quest item response operation.
    [Serializable]
    public class TurnInQuestItemResponse
    {
        // Executes success operation.
        public bool Success { get; set; }
        // Executes message operation.
        public string Message { get; set; }
        // Executes quest operation.
        public PlayerQuestResponse Quest { get; set; }
        // Executes consumed item id operation.
        public int? ConsumedItemId { get; set; }
        // Executes consumed item name operation.
        public string ConsumedItemName { get; set; }
        // Executes consumed quantity operation.
        public int ConsumedQuantity { get; set; }
    }
    // Executes player daily login response operation.
    [Serializable]
    public class PlayerDailyLoginResponse
    {
        // Executes player daily login id operation.
        public int PlayerDailyLoginId { get; set; }
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes current streak operation.
        public int CurrentStreak { get; set; }
        // Executes total days claimed operation.
        public int TotalDaysClaimed { get; set; }
        // Executes last claimed at operation.
        public string LastClaimedAt { get; set; }
        // Executes is claimed today operation.
        public bool IsClaimedToday { get; set; }
        // Executes current year operation.
        public int CurrentYear { get; set; }
        // Executes current month operation.
        public int CurrentMonth { get; set; }
        // Executes retro claim count operation.
        public int RetroClaimCount { get; set; }
        // Executes claimed days operation.
        public List<int> ClaimedDays { get; set; }
    }

    // Executes claim daily reward response operation.
    [Serializable]
    public class ClaimDailyRewardResponse
    {
        // Executes success operation.
        public bool Success { get; set; }
        // Executes message operation.
        public string Message { get; set; }
        // Executes current streak operation.
        public int CurrentStreak { get; set; }
        // Executes total days claimed operation.
        public int TotalDaysClaimed { get; set; }
        // Supported reward types: Gold, Gems, EXP, Energy, or Item; Item rewards also require an item identifier and quantity.
        public string RewardType { get; set; }
        // Executes reward value operation.
        public decimal RewardValue { get; set; }
        // Executes reward item id operation.
        public int? RewardItemId { get; set; }
        // Executes reward item name operation.
        public string RewardItemName { get; set; }
        // Executes reward item quantity operation.
        public int RewardItemQuantity { get; set; }
    }

}

namespace MysticJourney.API.Models.Request
{
    // Executes update world position request operation.
    [Serializable]
    public class UpdateWorldPositionRequest
    {
        // Executes map name operation.
        public string MapName { get; set; }
        // Executes position x operation.
        public double PositionX { get; set; }
        // Executes position y operation.
        public double PositionY { get; set; }
    }

    // Executes talk to npc request operation.
    [Serializable]
    public class TalkToNpcRequest
    {
        // Executes npc id operation.
        public int NPCId { get; set; }
    }

    // Executes interact object request operation.
    [Serializable]
    public class InteractObjectRequest
    {
        // Executes map name operation.
        public string MapName { get; set; }
        // Executes object key operation.
        public string ObjectKey { get; set; }
        // Executes interaction type operation.
        public string InteractionType { get; set; }
        // Executes quest id operation.
        public int? QuestId { get; set; }
        // Executes progress delta operation.
        public int ProgressDelta { get; set; } = 1;
    }

    // Executes turn in quest item request operation.
    [Serializable]
    public class TurnInQuestItemRequest
    {
        // Executes npc id operation.
        public int NPCId { get; set; }
        // Executes quest id operation.
        public int QuestId { get; set; }
    }
}
