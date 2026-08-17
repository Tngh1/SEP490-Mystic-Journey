using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Request
{
    // Initializes a new default instance of the UpgradePlayerSkillRequest class.
    [Serializable]
    public class UpgradePlayerSkillRequest
    {
        // Executes player skill id operation.
        public int PlayerSkillId { get; set; }
    }

    // Executes equip skill request operation.
    [Serializable]
    public class EquipSkillRequest
    {
        // Executes player skill id operation.
        public int PlayerSkillId { get; set; }
        // Executes is equipped operation.
        public bool IsEquipped { get; set; }
        // Executes slot index operation.
        public int? SlotIndex { get; set; }
    }

}

namespace MysticJourney.API.Models.Response
{
    // Executes player skill response operation.
    [Serializable]
    public class PlayerSkillResponse
    {
        // Executes player skill id operation.
        public int PlayerSkillId { get; set; }
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes skill id operation.
        public int SkillId { get; set; }
        // Executes skill name operation.
        public string SkillName { get; set; }
        // Executes skill description operation.
        public string SkillDescription { get; set; }
        // Supported skill types: Active, Passive, Buff, or Debuff; the type controls activation and effect presentation.
        public string SkillType { get; set; }
        // Supported damage types: Physical, Magical, or TrueDamage; the value selects how skill damage is categorized and resolved.
        public string DamageType { get; set; }
        // Supported target types: SingleTarget, Area, Self, or Ally; the value determines who can receive the skill effect.
        public string TargetType { get; set; }
        // Executes level operation.
        public int Level { get; set; }
        // Executes experience operation.
        public int Experience { get; set; }
        // Executes is equipped operation.
        public bool IsEquipped { get; set; }
        // Executes equipped slot operation.
        public int? EquippedSlot { get; set; }
        // Executes cooldown seconds operation.
        public int CooldownSeconds { get; set; }
        // Executes base damage operation.
        public double BaseDamage { get; set; }
        // Executes damage per level operation.
        public double DamagePerLevel { get; set; }
        // Executes damage growth percent operation.
        public double DamageGrowthPercent { get; set; }
        // Executes effective damage operation.
        public double EffectiveDamage { get; set; }
        // Executes unlock level operation.
        public int UnlockLevel { get; set; }
        // Executes corruption cost operation.
        public float CorruptionCost { get; set; }
        // Executes unlocked at operation.
        public string UnlockedAt { get; set; }
        // Executes next available time operation.
        public string NextAvailableTime { get; set; }
    }

    // Executes player me skills response operation.
    [Serializable]
    public class PlayerMeSkillsResponse
    {
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes skills operation.
        public List<PlayerSkillResponse> Skills { get; set; } = new List<PlayerSkillResponse>();
        // Executes total count operation.
        public int TotalCount { get; set; }
    }
}
