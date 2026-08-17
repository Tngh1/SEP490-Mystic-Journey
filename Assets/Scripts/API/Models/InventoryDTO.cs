namespace MysticJourney.API.Models.Request
{
    // Initializes a new default instance of the EquipItemRequest class.
    [System.Serializable]
    public class EquipItemRequest
    {
        // Executes inventory item id operation.
        public int InventoryItemId { get; set; }
    }

    // Executes unequip item request operation.
    [System.Serializable]
    public class UnequipItemRequest
    {
        // Executes inventory item id operation.
        public int InventoryItemId { get; set; }
    }

    // Executes consume item request operation.
    [System.Serializable]
    public class ConsumeItemRequest
    {
        // Executes inventory item id operation.
        public int InventoryItemId { get; set; }
        // Executes quantity operation.
        public int Quantity { get; set; }
    }
}

namespace MysticJourney.API.Models.Response
{
    // Executes inventory summary response operation.
    [System.Serializable]
    public class InventorySummaryResponse
    {
        // Executes total items operation.
        public int TotalItems { get; set; }
        // Executes total skins operation.
        public int TotalSkins { get; set; }
        // Executes equipped items operation.
        public InventoryItemResponse[] EquippedItems { get; set; }
        // Executes bag items operation.
        public InventoryItemResponse[] BagItems { get; set; }
        // Executes bag capacity operation.
        public int BagCapacity { get; set; }
        // Executes player skins operation.
        public PlayerSkinSummaryResponse[] PlayerSkins { get; set; }
    }

    // Executes player skin summary response operation.
    [System.Serializable]
    public class PlayerSkinSummaryResponse
    {
        // Executes player skin id operation.
        public int PlayerSkinId { get; set; }
        // Executes skin id operation.
        public int SkinId { get; set; }
        // Executes skin name operation.
        public string SkinName { get; set; }
        // Executes skin description operation.
        public string SkinDescription { get; set; }
        // Supported skin types include Armor and FullSet; the value identifies how the cosmetic is grouped and equipped.
        public string SkinType { get; set; }
        // Supported rarity values: Common, Uncommon, Rare, Epic, Legendary, or Mythic; rarity controls quality, visuals, and sorting priority.
        public string SkinRarity { get; set; }
        // Executes icon url operation.
        public string IconUrl { get; set; }
        // Executes preview url operation.
        public string PreviewUrl { get; set; }
        // Executes is equipped operation.
        public bool IsEquipped { get; set; }
    }

    // Executes inventory item response operation.
    [System.Serializable]
    public class InventoryItemResponse
    {
        // Executes inventory item id operation.
        public int InventoryItemId { get; set; }
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes item id operation.
        public int ItemId { get; set; }
        // Executes item name operation.
        public string ItemName { get; set; }
        // Executes item description operation.
        public string ItemDescription { get; set; }
        // Executes item type operation.
        public string ItemType { get; set; }
        // Supported rarity values: Common, Uncommon, Rare, Epic, Legendary, or Mythic; rarity controls quality, visuals, and sorting priority.
        public string ItemRarity { get; set; }
        // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
        public string ItemSlot { get; set; }
        // Executes icon url operation.
        public string IconUrl { get; set; }
        // Executes corruption reduction operation.
        public float CorruptionReduction { get; set; }
        // Executes quantity operation.
        public int Quantity { get; set; }
        // Executes is equipped operation.
        public bool IsEquipped { get; set; }
        // Executes is skin operation.
        public bool IsSkin { get; set; }
        // Executes equipped slot operation.
        public string EquippedSlot { get; set; }
        // Executes enhancement level operation.
        public int EnhancementLevel { get; set; }
        // Executes created at operation.
        public string CreatedAt { get; set; }

        // Executes base hp operation.
        public int BaseHp { get; set; }
        // Executes base atk operation.
        public int BaseAtk { get; set; }
        // Executes base def operation.
        public int BaseDef { get; set; }

        // Executes bonus hp operation.
        public int BonusHp { get; set; }
        // Executes bonus atk operation.
        public int BonusAtk { get; set; }
        // Executes bonus def operation.
        public int BonusDef { get; set; }
        // Executes bonus crit rate operation.
        public float BonusCritRate { get; set; }
        // Executes bonus crit damage operation.
        public float BonusCritDamage { get; set; }
    }

    // Executes inventory action result response operation.
    [System.Serializable]
    public class InventoryActionResultResponse
    {
        // Executes item operation.
        public InventoryItemResponse Item { get; set; }
        // Executes player stats operation.
        public PlayerStatsResponse PlayerStats { get; set; }
    }

    // Executes consume item result response operation.
    [System.Serializable]
    public class ConsumeItemResultResponse
    {
        // Executes item name operation.
        public string ItemName { get; set; }
        // Executes effect type operation.
        public string EffectType { get; set; }
        // Executes effect value operation.
        public int EffectValue { get; set; }
        // Executes current hp operation.
        public int? CurrentHp { get; set; }
        // Executes max hp operation.
        public int? MaxHp { get; set; }
        // Executes current energy operation.
        public int? CurrentEnergy { get; set; }
        // Executes max energy operation.
        public int? MaxEnergy { get; set; }
        // Executes corruption level operation.
        public float? CorruptionLevel { get; set; }
        // Executes remaining quantity operation.
        public int RemainingQuantity { get; set; }
    }
}
