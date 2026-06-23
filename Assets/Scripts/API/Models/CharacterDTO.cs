using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Models.Response
{
    // ── Response: POST /api/characters ────────────────────────────────────────
    // Maps CharacterResponseDto từ backend
    [System.Serializable]
    public class CharacterResponse
    {
        public int PlayerProfileId { get; set; }
        public int AccountId { get; set; }
        public string CharacterName { get; set; }
        public string PlayerClass { get; set; }     // Knight | Archer | Mage
        public int Level { get; set; }
        public int ExperiencePoints { get; set; }
        public decimal Gold { get; set; }
        public decimal Gems { get; set; }
        public int Energy { get; set; }
        public string CreatedAt { get; set; }
        public PlayerStatsResponse Stats { get; set; }
    }

    // ── Response: GET /api/characters/stats ───────────────────────────────────
    // Tái sử dụng PlayerStatsResponse đã có sẵn trong PlayerStatsResponseDto.cs
    // Không cần khai báo lại.

    // ── Response: POST /api/characters/upgrade ────────────────────────────────
    // Maps UpgradeAttributeResponseDto từ backend
    [System.Serializable]
    public class UpgradeAttributeResponse
    {
        public string UpgradedAttribute { get; set; }
        public int AmountSpent { get; set; }
        public int RemainingSkillPoints { get; set; }
        public PlayerStatsResponse Stats { get; set; }
    }
}

namespace MysticJourney.API.Models.Request
{
    // ── Request: POST /api/characters ─────────────────────────────────────────
    [System.Serializable]
    public class CreateCharacterRequest
    {
        public string CharacterName { get; set; }

        /// <summary>Knight | Archer | Mage</summary>
        public string SelectedClass { get; set; }
    }

    // ── Request: POST /api/characters/upgrade ─────────────────────────────────
    [System.Serializable]
    public class UpgradeAttributeRequest
    {
        /// <summary>MaxHp | Atk | Def | MoveSpeed | AttackSpeed | CritRate | CritDamage | DamageBonus</summary>
        public string AttributeName { get; set; }

        /// <summary>Số skill point muốn chi (1–10). Mặc định 1.</summary>
        public int Amount { get; set; } = 1;
    }
}
