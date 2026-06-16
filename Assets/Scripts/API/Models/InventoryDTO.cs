namespace MysticJourney.API.Models.Request
{
    [System.Serializable]
    public class EquipItemRequest
    {
        public int InventoryItemId { get; set; }
    }

    [System.Serializable]
    public class UnequipItemRequest
    {
        public int InventoryItemId { get; set; }
    }

    [System.Serializable]
    public class ConsumeItemRequest
    {
        public int InventoryItemId { get; set; }
        public int Quantity { get; set; }
    }
}

namespace MysticJourney.API.Models.Response
{
    // Response: GET /api/inventory/me → ApiResponse<InventorySummaryResponse>
    [System.Serializable]
    public class InventorySummaryResponse
    {
        public int TotalItems { get; set; }
        public int TotalSkins { get; set; }
        public InventoryItemResponse[] EquippedItems { get; set; }   // Items đang trang bị
        public InventoryItemResponse[] BagItems { get; set; }        // Items trong túi
        public int BagCapacity { get; set; }
        // Skin của player – dùng PlayerSkinId để gọi POST /api/skins/equip|unequip
        public PlayerSkinSummaryResponse[] PlayerSkins { get; set; }
    }

    // Thông tin skin nhỏ gọn kèm PlayerSkinId đúng
    [System.Serializable]
    public class PlayerSkinSummaryResponse
    {
        public int PlayerSkinId { get; set; }       // ID bảng PlayerSkins – dùng khi equip/unequip
        public int SkinId { get; set; }
        public string SkinName { get; set; }
        public string SkinDescription { get; set; }
        public string SkinType { get; set; }        // "FullSet", "Armor", "Weapon"
        public string SkinRarity { get; set; }      // "Common", "Epic"…
        public string IconUrl { get; set; }
        public string PreviewUrl { get; set; }
        public bool IsEquipped { get; set; }
    }

    [System.Serializable]
    public class InventoryItemResponse
    {
        public int InventoryItemId { get; set; }
        public int PlayerProfileId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public string ItemType { get; set; }       // "Weapon", "Armor", "Consumable"...
        public string ItemRarity { get; set; }     // "Common", "Rare", "Epic", "Legendary"
        public string IconUrl { get; set; }
        public int Quantity { get; set; }
        public bool IsEquipped { get; set; }
        public bool IsSkin { get; set; }
        public string EquippedSlot { get; set; }   // "Head", "Body", "Weapon"...
        public int EnhancementLevel { get; set; }
        public string CreatedAt { get; set; }
        
        // Base stats
        public int BaseHp { get; set; }
        public int BaseAtk { get; set; }
        public int BaseDef { get; set; }
        
        // Bonus stats
        public int BonusHp { get; set; }
        public int BonusAtk { get; set; }
        public int BonusDef { get; set; }
        public float BonusCritRate { get; set; }
        public float BonusCritDamage { get; set; }
    }

    // Response: POST /api/inventory/equip-item hoặc unequip-item
    [System.Serializable]
    public class InventoryActionResultResponse
    {
        public InventoryItemResponse Item { get; set; }        // Item sau khi equip/unequip
        public PlayerStatsResponse PlayerStats { get; set; }   // Stats mới của player
    }
}
