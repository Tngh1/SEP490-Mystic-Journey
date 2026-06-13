namespace MysticJourney.API.Models.Response
{
    // Maps GachaBannerResponseDto
    [System.Serializable]
    public class GachaBannerResponse
    {
        public int GachaBannerId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }            // "Standard", "Limited", "Weapon"
        public int PullCost { get; set; }           // Chi phí 1 lần pull (gems)
        public int PityLimit { get; set; }          // Số pull đảm bảo SSR
        public bool IsActive { get; set; }
        public string StartAt { get; set; }
        public string EndAt { get; set; }
    }

    // Maps GachaBannerDetailResponseDto (kế thừa GachaBannerResponseDto)
    [System.Serializable]
    public class GachaBannerDetailResponse : GachaBannerResponse
    {
        public GachaBannerItemResponse[] BannerItems { get; set; }  // Danh sách item trong banner
    }

    // Maps GachaBannerItemResponseDto
    [System.Serializable]
    public class GachaBannerItemResponse
    {
        public int GachaBannerItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemIconUrl { get; set; }
        public string ItemRarity { get; set; }
        public decimal DropRate { get; set; }       // 0.0 – 100.0
        public bool IsFeatured { get; set; }
    }
}
