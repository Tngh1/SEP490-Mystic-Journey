using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class QuestRewardItemResponse
    {
        public int QuestRewardItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string IconUrl { get; set; }
        public int Quantity { get; set; }
    }

    [Serializable]
    public class QuestRewardSkillResponse
    {
        public int QuestRewardSkillId { get; set; }
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public string ClassRequirement { get; set; }
        public string Type { get; set; }
        public string DamageType { get; set; }
    }

    [Serializable]
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

        public string IconUrl { get; set; }
        public string QuestIconUrl { get; set; }
        public string IconKey { get; set; }
        public string QuestIconKey { get; set; }

        public int RewardExperience { get; set; }
        public decimal RewardGold { get; set; }
        public decimal RewardGems { get; set; }
        public string RewardExperienceIconUrl { get; set; }
        public string RewardExpIconUrl { get; set; }
        public string RewardGoldIconUrl { get; set; }
        public string RewardGemsIconUrl { get; set; }
        public string RewardGemIconUrl { get; set; }
        public int? RewardItemId { get; set; }
        public string RewardItemName { get; set; }
        public string RewardItemIconUrl { get; set; }
        public List<QuestRewardItemResponse> RewardItems { get; set; } = new();
        public int? RewardSkillId { get; set; }
        public string RewardSkillName { get; set; }
        public string RewardSkillIconUrl { get; set; }
        public List<QuestRewardSkillResponse> RewardSkills { get; set; } = new();
        public bool IsActive { get; set; }
    }
}
