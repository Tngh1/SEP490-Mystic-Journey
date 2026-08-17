namespace MysticJourney.API.Models.Request
{
    // Initializes a new default instance of the EquipSkinRequest class.
    [System.Serializable]
    public class EquipSkinRequest
    {
        // Executes player skin id operation.
        public int PlayerSkinId { get; set; }
        // Executes is equipped operation.
        public bool IsEquipped { get; set; }
    }

    // Executes unequip skin request operation.
    [System.Serializable]
    public class UnequipSkinRequest
    {
        // Executes player skin id operation.
        public int PlayerSkinId { get; set; }
    }
}

namespace MysticJourney.API.Models.Response
{
    // Executes player skin response operation.
    [System.Serializable]
    public class PlayerSkinResponse
    {
        // Executes player skin id operation.
        public int PlayerSkinId { get; set; }
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
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
        // Executes unlocked at operation.
        public string UnlockedAt { get; set; }
    }
}
