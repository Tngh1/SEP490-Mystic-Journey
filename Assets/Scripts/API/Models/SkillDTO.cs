using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Request
{
    [Serializable]
    public class UpgradePlayerSkillRequest
    {
        public int PlayerSkillId { get; set; }
    }

    // ĐÃ THÊM CLASS NÀY VÀO ĐÂY
    [Serializable]
    public class EquipSkillRequest
    {
        public int PlayerSkillId { get; set; }
        public bool IsEquipped { get; set; }
        public int? SlotIndex { get; set; }
    }

}

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class PlayerSkillResponse
    {
        public int PlayerSkillId { get; set; }
        public int PlayerProfileId { get; set; }
        public int SkillId { get; set; }
        public string SkillName { get; set; }
        public string SkillDescription { get; set; }
        public string SkillType { get; set; }
        public string DamageType { get; set; }
        public string TargetType { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public bool IsEquipped { get; set; }
        public int? EquippedSlot { get; set; }
        public int CooldownSeconds { get; set; }
        public double BaseDamage { get; set; }
        public double DamagePerLevel { get; set; }
        public double DamageGrowthPercent { get; set; }
        public double EffectiveDamage { get; set; }
        public int UnlockLevel { get; set; }
        public float CorruptionCost { get; set; }
        public string UnlockedAt { get; set; }
        public string NextAvailableTime { get; set; }
    }

    [Serializable]
    public class PlayerMeSkillsResponse
    {
        public int PlayerProfileId { get; set; }
        public List<PlayerSkillResponse> Skills { get; set; } = new List<PlayerSkillResponse>();
        public int TotalCount { get; set; }
    }
}
