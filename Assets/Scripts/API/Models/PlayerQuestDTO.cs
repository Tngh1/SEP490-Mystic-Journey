using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the PlayerQuestResponse class.
    [Serializable]
    public class PlayerQuestResponse
    {
        // Executes player quest id operation.
        public int PlayerQuestId { get; set; }
        // Executes quest id operation.
        public int QuestId { get; set; }
        // Executes quest title operation.
        public string QuestTitle { get; set; }
        // Executes quest description operation.
        public string QuestDescription { get; set; }
        // Executes quest type operation.
        public string QuestType { get; set; }
        // Executes map name operation.
        public string MapName { get; set; }
        // Executes region name operation.
        public string RegionName { get; set; }
        // Executes objective type operation.
        public string ObjectiveType { get; set; }
        // Executes objective target operation.
        public string ObjectiveTarget { get; set; }
        // Executes objective location operation.
        public string ObjectiveLocation { get; set; }
        // Executes quest giver name operation.
        public string QuestGiverName { get; set; }
        // Executes status operation.
        public string Status { get; set; }
        // Executes progress operation.
        public int Progress { get; set; }
        // Executes target amount operation.
        public int TargetAmount { get; set; }
        // Executes required level operation.
        public int RequiredLevel { get; set; }

        // Executes icon url operation.
        public string IconUrl { get; set; }
        // Executes quest icon url operation.
        public string QuestIconUrl { get; set; }
        // Executes icon key operation.
        public string IconKey { get; set; }
        // Executes quest icon key operation.
        public string QuestIconKey { get; set; }

        // Executes reward experience operation.
        public int RewardExperience { get; set; }
        // Executes reward gold operation.
        public decimal RewardGold { get; set; }
        // Executes reward gems operation.
        public decimal RewardGems { get; set; }
        // Executes reward experience icon url operation.
        public string RewardExperienceIconUrl { get; set; }
        // Executes reward exp icon url operation.
        public string RewardExpIconUrl { get; set; }
        // Executes reward gold icon url operation.
        public string RewardGoldIconUrl { get; set; }
        // Executes reward gems icon url operation.
        public string RewardGemsIconUrl { get; set; }
        // Executes reward gem icon url operation.
        public string RewardGemIconUrl { get; set; }
        // Executes reward item id operation.
        public int? RewardItemId { get; set; }
        // Executes reward item name operation.
        public string RewardItemName { get; set; }
        // Executes reward item icon url operation.
        public string RewardItemIconUrl { get; set; }
        // Executes reward items operation.
        public List<QuestRewardItemResponse> RewardItems { get; set; } = new();
        // Executes reward skill id operation.
        public int? RewardSkillId { get; set; }
        // Executes reward skill name operation.
        public string RewardSkillName { get; set; }
        // Executes reward skill icon url operation.
        public string RewardSkillIconUrl { get; set; }
        // Executes reward skills operation.
        public List<QuestRewardSkillResponse> RewardSkills { get; set; } = new();
        // Executes accepted at operation.
        public string AcceptedAt { get; set; }
        // Executes completed at operation.
        public string CompletedAt { get; set; }
        // Executes claimed at operation.
        public string ClaimedAt { get; set; }
    }

}

namespace MysticJourney.API.Models.Request
{
    // Executes accept quest request operation.
    [Serializable]
    public class AcceptQuestRequest
    {
        // Executes quest id operation.
        public int QuestId { get; set; }
    }

    // Executes batch progress request operation.
    [Serializable]
    public class BatchProgressRequest
    {
        // Executes updates operation.
        public List<QuestProgressItem> Updates { get; set; } = new();
    }

    // Executes quest progress item operation.
    [Serializable]
    public class QuestProgressItem
    {
        // Executes quest id operation.
        public int QuestId { get; set; }
        // Executes progress operation.
        public int Progress { get; set; }
    }

    // Executes complete quest request operation.
    [Serializable]
    public class CompleteQuestRequest
    {
        // Executes quest id operation.
        public int QuestId { get; set; }
    }

    // Executes claim quest request operation.
    [Serializable]
    public class ClaimQuestRequest
    {
        // Executes quest id operation.
        public int QuestId { get; set; }
    }
}
