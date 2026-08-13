namespace MysticJourney.API.Models.Request
{
    // PUT /api/playerprofiles/{id}
    [System.Serializable]
    public class UpdatePlayerProfileRequest
    {
        public string DisplayName { get; set; }
        public string AvatarUrl { get; set; }
        public string PlayerClass { get; set; }
        public int? Level { get; set; }
        public int? ExperiencePoints { get; set; }
        public decimal? Gold { get; set; }
        public decimal? Gems { get; set; }
        public int? Energy { get; set; }
        public int? MaxEnergy { get; set; }
        public float? CorruptionLevel { get; set; }
        public bool? IsBanned { get; set; }
    }

    [System.Serializable]
    public class ChangeNameRequestDto
    {
        public string NewName { get; set; }
    }
}

namespace MysticJourney.API.Models.Response
{
    // Response: GET /api/playerprofiles/{id}
    [System.Serializable]
    public class PlayerProfileResponse
    {
        public int PlayerProfileId { get; set; }
        public int AccountId { get; set; }
        public string AccountEmail { get; set; }
        public string DisplayName { get; set; }
        public string AvatarUrl { get; set; }
        public string PlayerClass { get; set; }
        public int Level { get; set; }
        public int ExperiencePoints { get; set; }
        public int AvailableStatPoints { get; set; }
        public decimal Gold { get; set; }
        public decimal Gems { get; set; }
        public int Energy { get; set; }
        public int MaxEnergy { get; set; }
        public string LastEnergyUpdateTime { get; set; }
        public string LastMapName { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public float CorruptionLevel { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public bool IsBanned { get; set; }
        public string LastFreeGachaTime { get; set; }
        public bool HasChangedName { get; set; }
    }

    // Response mở rộng kèm Stats
    [System.Serializable]
    public class PlayerStatsResponse
    {
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public int Atk { get; set; }
        public int Def { get; set; }
        public float MoveSpeed { get; set; }
        public float AttackSpeed { get; set; }
        public float CritRate { get; set; }
        public float CritDamage { get; set; }
        public float DamageBonus { get; set; }
        public int SkillPoints { get; set; }
        public int TotalWins { get; set; }
        public int TotalLosses { get; set; }
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }
        public System.Collections.Generic.List<PlayerBuffDTO> ActiveBuffs { get; set; }
    }
}

namespace MysticJourney.API.Models.Request
{
    [System.Serializable]
    public class AllocateStatRequestDto
    {
        public string StatName { get; set; }
    }
}
