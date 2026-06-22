using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class PlayerQuestResponse
    {
        public int PlayerQuestId { get; set; }
        public int QuestId { get; set; }
        public string QuestTitle { get; set; }
        public string QuestDescription { get; set; }
        public string QuestType { get; set; }
        public string MapName { get; set; }
        public string RegionName { get; set; }
        public string ObjectiveType { get; set; }
        public string ObjectiveTarget { get; set; }
        public string ObjectiveLocation { get; set; }
        public string QuestGiverName { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public int TargetAmount { get; set; }
        public int RequiredLevel { get; set; }
        public int RewardExperience { get; set; }
        public decimal RewardGold { get; set; }
        public decimal RewardGems { get; set; }
        public int? RewardItemId { get; set; }
        public string RewardItemName { get; set; }
        public int? RewardSkillId { get; set; }
        public string RewardSkillName { get; set; }
        public string AcceptedAt { get; set; }
        public string CompletedAt { get; set; }
        public string ClaimedAt { get; set; }
    }

    [Serializable]
    public class PlayerQuestListWrapper
    {
        public bool Success { get; set; }
        public List<PlayerQuestResponse> Data { get; set; }
    }

    [Serializable]
    public class PlayerQuestSingleWrapper
    {
        public bool Success { get; set; }
        public PlayerQuestResponse Data { get; set; }
    }

    [Serializable]
    public class BatchProgressWrapper
    {
        public bool Success { get; set; }
        public List<PlayerQuestResponse> Data { get; set; }
    }
}

namespace MysticJourney.API.Models.Request
{
    [Serializable]
    public class AcceptQuestRequest
    {
        public int QuestId { get; set; }
    }

    [Serializable]
    public class BatchProgressRequest
    {
        public List<QuestProgressItem> Updates { get; set; } = new();
    }

    [Serializable]
    public class QuestProgressItem
    {
        public int QuestId { get; set; }
        public int Progress { get; set; }
    }

    [Serializable]
    public class CompleteQuestRequest
    {
        public int QuestId { get; set; }
    }

    [Serializable]
    public class ClaimQuestRequest
    {
        public int QuestId { get; set; }
    }
}
