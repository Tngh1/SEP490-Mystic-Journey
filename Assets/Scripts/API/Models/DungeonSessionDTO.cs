using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    // ── Response: POST /api/dungeons/{id}/enter ───────────────────────────────
    // Maps EnterDungeonResponseDto. Energy chưa bị trừ ở bước này.
    [System.Serializable]
    public class EnterDungeonResponse
    {
        public int DungeonSessionId { get; set; }
        public int PlayerProfileId { get; set; }
        public int DungeonConfigId { get; set; }
        public string DungeonName { get; set; }
        public int EnergyCost { get; set; }
        public int PlayerCurrentEnergy { get; set; }
        public string EnterTime { get; set; }
        public string Status { get; set; }          // "Active"
    }

    // ── Response: POST /api/dungeons/session/{id}/progress ───────────────────
    // Maps DungeonProgressResponseDto
    [System.Serializable]
    public class DungeonProgressResponse
    {
        public int DungeonProgressId { get; set; }
        public int DungeonSessionId { get; set; }
        public int MonstersKilled { get; set; }
        public bool BossKilled { get; set; }
        public int CompletionPercentage { get; set; }
        public string ExtraData { get; set; }
        public string UpdatedAt { get; set; }
        public string SessionStatus { get; set; }   // "Active"
    }

    // ── Response: POST /api/dungeons/session/{id}/complete ───────────────────
    // Maps CompleteDungeonResponseDto
    [System.Serializable]
    public class CompleteDungeonResponse
    {
        public int DungeonSessionId { get; set; }
        public string Status { get; set; }          // "Completed"
        public string CompletedTime { get; set; }
        public ChestPreviewResponse RewardChest { get; set; }
        public string Message { get; set; }
    }

    // Preview chest (maps ChestResponseDto)
    [System.Serializable]
    public class ChestPreviewResponse
    {
        public int ChestId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }            // Common | Rare | Epic | Legendary
        public int GoldMinReward { get; set; }
        public int GoldMaxReward { get; set; }
        public int ExperienceReward { get; set; }
        public ChestItemInfo[] ChestItems { get; set; }
    }

    [System.Serializable]
    public class ChestItemInfo
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemIconUrl { get; set; }
        public string ItemRarity { get; set; }
        public int QuantityMin { get; set; }
        public int QuantityMax { get; set; }
        public float DropRate { get; set; }
        public bool IsGuaranteed { get; set; }
    }

    // ── Response: POST /api/dungeons/session/{id}/claim-reward ───────────────
    // Maps ClaimDungeonRewardResponseDto. Energy bị trừ TẠI ĐÂY.
    [System.Serializable]
    public class ClaimDungeonRewardResponse
    {
        public int DungeonSessionId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public int EnergyConsumed { get; set; }
        public int GoldEarned { get; set; }
        public int ExperienceEarned { get; set; }
        public DungeonRewardItemResponse[] Items { get; set; }
    }

    [System.Serializable]
    public class DungeonRewardItemResponse
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemIconUrl { get; set; }
        public string ItemType { get; set; }
        public string Rarity { get; set; }
        public int Quantity { get; set; }
    }
}

namespace MysticJourney.API.Models.Request
{
    // ── Request: POST /api/dungeons/session/{id}/progress ────────────────────
    [System.Serializable]
    public class UpdateDungeonProgressRequest
    {
        public int MonstersKilled { get; set; }
        public bool BossKilled { get; set; }

        /// <summary>0–100</summary>
        public int CompletionPercentage { get; set; }

        /// <summary>JSON string tuỳ chọn cho dữ liệu mở rộng (tầng, bẫy, v.v.)</summary>
        public string ExtraData { get; set; }
    }
}
