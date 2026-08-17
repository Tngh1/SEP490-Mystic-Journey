using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the QuestRewardItemResponse class.
    [Serializable]
    public class QuestRewardItemResponse
    {
        // Executes quest reward item id operation.
        public int QuestRewardItemId { get; set; }
        // Executes item id operation.
        public int ItemId { get; set; }
        // Executes item name operation.
        public string ItemName { get; set; }
        // Executes icon url operation.
        public string IconUrl { get; set; }
        // Executes quantity operation.
        public int Quantity { get; set; }
    }

    // Executes quest reward skill response operation.
    [Serializable]
    public class QuestRewardSkillResponse
    {
        // Executes quest reward skill id operation.
        public int QuestRewardSkillId { get; set; }
        // Executes skill id operation.
        public int SkillId { get; set; }
        // Executes skill name operation.
        public string SkillName { get; set; }
        // Supported class requirements: Knight, Archer, Mage, or All; All allows every player class to use the skill or reward.
        public string ClassRequirement { get; set; }
        // Supported skill types: Active, Passive, Buff, or Debuff; the type controls activation and effect presentation.
        public string Type { get; set; }
        // Supported damage types: Physical, Magical, or TrueDamage; the value selects how skill damage is categorized and resolved.
        public string DamageType { get; set; }
    }

    // Executes quest response operation.
    [Serializable]
    public class QuestResponse
    {
        // Executes quest id operation.
        public int QuestId { get; set; }
        // Executes title operation.
        public string Title { get; set; }
        // Executes description operation.
        public string Description { get; set; }
        // Executes type operation.
        public string Type { get; set; }
        // Executes default status operation.
        public string DefaultStatus { get; set; }
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
        // Executes required level operation.
        public int RequiredLevel { get; set; }
        // Executes target amount operation.
        public int TargetAmount { get; set; }

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
        // Executes is active operation.
        public bool IsActive { get; set; }
    }
}
