using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class PlayerAchievementResponse
    {
        public int PlayerAchievementId { get; set; }
        public int PlayerProfileId { get; set; }
        public int AchievementId { get; set; }
        public string AchievementName { get; set; } = string.Empty;
        public string AchievementDescription { get; set; } = string.Empty;
        public string AchievementType { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public int Progress { get; set; }
        public int RequiredValue { get; set; }
        public bool IsCompleted { get; set; }
        public string CompletedAt { get; set; } = string.Empty;
        public string UnlockedAt { get; set; } = string.Empty;
        public int? RewardItemId { get; set; }
        public string RewardItemName { get; set; } = string.Empty;
        public int RewardQuantity { get; set; }
        public decimal RewardGold { get; set; }
        public int RewardGem { get; set; }
        public int RewardGems
        {
            get => RewardGem;
            set => RewardGem = value;
        }
    }

    [Serializable]
    public class PlayerMeAchievementsResponse
    {
        public int PlayerProfileId { get; set; }
        public List<PlayerAchievementResponse> Achievements { get; set; } = new();
        public int TotalCount { get; set; }
        public int CompletedCount { get; set; }
    }
}
