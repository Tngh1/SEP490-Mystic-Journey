using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the GachaBannerResponse class.
    [Serializable]
    public class GachaBannerResponse
    {
        public int GachaBannerId;
        public string Name;
        // Supported gacha banner types: Standard, Limited, or Event; the type controls banner categorization and presentation.
        public string Type;
        public int PullCost;
        public int PityLimit;
        public bool IsActive;
        public DateTime StartAt;
        public DateTime EndAt;
    }

    // Initializes a new default instance of the GachaBannerResponse class.
    public class GachaBannerDetailResponse : GachaBannerResponse
    {
        public List<GachaBannerItemResponse> BannerItems;
    }

    // Executes gacha banner item response operation.
    [Serializable]
    public class GachaBannerItemResponse
    {
        public int GachaBannerItemId;
        public int ItemId;
        public string ItemName;
        public string ItemIconUrl;
        // Supported rarity values: Common, Uncommon, Rare, Epic, Legendary, or Mythic; rarity controls quality, visuals, and sorting priority.
        public string ItemRarity;
        public float DropRate;
        public bool IsFeatured;
    }

    // Executes gacha pull result response operation.
    [Serializable]
    public class GachaPullResultResponse
    {
        public bool Success;
        public string Message;
        public int PulledItemId;
        public string PulledItemName;
        public string PulledItemIconUrl;
        public string PulledItemRarity;
        public bool IsNew;
        public int PityCounter;
        public int CurrentPity;
    }

    // Executes multi pull result response operation.
    [Serializable]
    public class MultiPullResultResponse
    {
        public bool Success;
        public string Message;
        public List<GachaPullResultResponse> PulledItems;
        public float TotalCost;
    }

    // Executes gacha pull history response operation.
    [Serializable]
    public class GachaPullHistoryResponse
    {
        public int GachaPullHistoryId;
        public int PlayerProfileId;
        public int GachaBannerId;
        public string BannerName;
        public int RewardItemId;
        public string RewardItemName;
        public string RewardItemIconUrl;
        public string RewardItemRarity;
        public int PullCount;
        public float CostSpent;
        public DateTime PulledAt;
    }
}
