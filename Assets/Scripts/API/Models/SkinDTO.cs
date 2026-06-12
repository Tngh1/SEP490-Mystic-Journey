namespace MysticJourney.API.Models.Request
{
    // POST /api/skins/equip  (IsEquipped=true để equip, false để đổi trạng thái)
    [System.Serializable]
    public class EquipSkinRequest
    {
        public int PlayerSkinId { get; set; }  // ID trong bảng PlayerSkin
        public bool IsEquipped { get; set; }
    }

    // POST /api/skins/unequip
    [System.Serializable]
    public class UnequipSkinRequest
    {
        public int PlayerSkinId { get; set; }  // ID trong bảng PlayerSkin
    }
}

namespace MysticJourney.API.Models.Response
{
    // Maps PlayerSkinResponseDto
    [System.Serializable]
    public class PlayerSkinResponse
    {
        public int PlayerSkinId { get; set; }
        public int PlayerProfileId { get; set; }
        public int SkinId { get; set; }
        public string SkinName { get; set; }
        public string SkinDescription { get; set; }
        public string SkinType { get; set; }        // "Armor", "Weapon"
        public string SkinRarity { get; set; }
        public string IconUrl { get; set; }
        public string PreviewUrl { get; set; }
        public bool IsEquipped { get; set; }
        public string UnlockedAt { get; set; }
    }
}
