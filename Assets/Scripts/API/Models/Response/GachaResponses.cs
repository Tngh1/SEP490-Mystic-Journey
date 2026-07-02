using System;
using System.Collections.Generic;

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class GachaBannerResponse
    {
        public int GachaBannerId;
        public string Name;
        public string Type;
        public int PullCost;
        public int PityLimit;
        public bool IsActive;
        public DateTime StartAt;
        public DateTime EndAt;
    }

    public class GachaBannerDetailResponse : GachaBannerResponse
    {
        public List<GachaBannerItemResponse> BannerItems;
    }

    [Serializable]
    public class GachaBannerItemResponse
    {
        public int GachaBannerItemId;
        public int ItemId;
        public string ItemName;
        public string ItemIconUrl;
        public string ItemRarity;
        public float DropRate;
        public bool IsFeatured;
    }

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

    [Serializable]
    public class MultiPullResultResponse
    {
        public bool Success;
        public string Message;
        public List<GachaPullResultResponse> PulledItems;
        public float TotalCost;
    }

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
