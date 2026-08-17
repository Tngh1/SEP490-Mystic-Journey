using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the PlayerAchievementResponse class.
    [Serializable]
    public class PlayerAchievementResponse
    {
        // Executes player achievement id operation.
        public int PlayerAchievementId { get; set; }
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes achievement id operation.
        public int AchievementId { get; set; }
        // Executes achievement name operation.
        public string AchievementName { get; set; } = string.Empty;
        // Executes achievement description operation.
        public string AchievementDescription { get; set; } = string.Empty;
        // Executes achievement type operation.
        public string AchievementType { get; set; } = string.Empty;
        // Executes icon url operation.
        public string IconUrl { get; set; } = string.Empty;
        // Executes progress operation.
        public int Progress { get; set; }
        // Executes required value operation.
        public int RequiredValue { get; set; }
        // Executes is completed operation.
        public bool IsCompleted { get; set; }
        // Executes completed at operation.
        public string CompletedAt { get; set; } = string.Empty;
        // Executes unlocked at operation.
        public string UnlockedAt { get; set; } = string.Empty;
        // Executes reward item id operation.
        public int? RewardItemId { get; set; }
        // Executes reward item name operation.
        public string RewardItemName { get; set; } = string.Empty;
        // Executes reward quantity operation.
        public int RewardQuantity { get; set; }
        // Executes reward gold operation.
        public decimal RewardGold { get; set; }
        // Executes reward gem operation.
        public int RewardGem { get; set; }
        // Executes reward gems operation.
        public int RewardGems
        {
            get => RewardGem;
            set => RewardGem = value;
        }
    }

    // Executes player me achievements response operation.
    [Serializable]
    public class PlayerMeAchievementsResponse
    {
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes achievements operation.
        public List<PlayerAchievementResponse> Achievements { get; set; } = new();
        // Executes total count operation.
        public int TotalCount { get; set; }
        // Executes completed count operation.
        public int CompletedCount { get; set; }
    }
}
