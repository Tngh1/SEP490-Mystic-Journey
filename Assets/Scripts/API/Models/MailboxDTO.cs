namespace MysticJourney.API.Models.Response
{
    // ═══════════════════════════════════════════════════════════════════════
    // MAILBOX DTOs - Response models cho Mailbox API
    // ═══════════════════════════════════════════════════════════════════════

    // Maps MailboxSummaryDto – dùng trong danh sách mail (GET /api/mailboxes/me)
    [System.Serializable]
    public class MailboxSummaryResponse
    {
        public int MailboxId;
        public string Title;
        public string Type;             // "System", "Event", "Gift"
        public bool IsRead;
        public bool HasClaimableReward; // có phần thưởng chưa claim
        public bool IsClaimed;
        public int? RemainingDays;      // null = không có hạn
        public string SentAt;
        public string ExpiredAt;
    }

    // Maps MailboxListPagedDto – trả về bởi GET /api/mailboxes/me
    [System.Serializable]
    public class MailboxListPagedResponse
    {
        public int TotalMailboxes;
        public int Page;
        public int PageSize;
        public int TotalPages;
        public MailboxSummaryResponse[] Items;
    }

    // Maps MailboxRewardItemDto – item đính kèm trong mail
    [System.Serializable]
    public class MailboxRewardItemResponse
    {
        public int ItemId;
        public string ItemName;
        public string IconUrl;
        public int Quantity;
    }

    // Maps MailboxDetailDto – trả về bởi GET /api/mailboxes/{id}, POST /read, /claim
    [System.Serializable]
    public class MailboxDetailResponse
    {
        public int MailboxId;
        public string Title;
        public string Content;
        public string Type;
        public bool IsRead;
        public bool IsClaimed;
        public float AttachedGold;
        public float AttachedGems;
        public MailboxRewardItemResponse[] AttachedItems;
        public string SentAt;
        public string ExpiredAt;
    }
}
