namespace MysticJourney.API.Models.Response
{
    // ═══════════════════════════════════════════════════════════════════════
    // MAIL DTOs - Response models cho Mail API
    // ═══════════════════════════════════════════════════════════════════════

    // Maps MailSummaryDto – dùng trong danh sách mail (GET /api/mails/me)
    [System.Serializable]
    public class MailSummaryResponse
    {
        public int MailId;
        public string Title;
        public string Type;             // "System", "Event", "Gift"
        public bool IsRead;
        public bool HasClaimableReward; // có phần thưởng chưa claim
        public bool IsClaimed;
        public int? RemainingDays;      // null = không có hạn
        public string SentAt;
        public string ExpiredAt;
    }

    // Maps MailListPagedDto – trả về bởi GET /api/mails/me
    [System.Serializable]
    public class MailListPagedResponse
    {
        public int TotalMails;
        public int Page;
        public int PageSize;
        public int TotalPages;
        public MailSummaryResponse[] Items;
    }

    // Maps MailRewardItemDto – item đính kèm trong mail
    [System.Serializable]
    public class MailRewardItemResponse
    {
        public int ItemId;
        public string ItemName;
        public string IconUrl;
        public int Quantity;
    }

    // Maps MailDetailDto – trả về bởi GET /api/mails/{id}, POST /read, /claim
    [System.Serializable]
    public class MailDetailResponse
    {
        public int MailId;
        public string Title;
        public string Content;
        public string Type;             // "System", "Event", "Gift"
        public bool IsRead;
        public bool IsClaimed;
        public float AttachedGold;
        public float AttachedGems;
        public MailRewardItemResponse AttachedItem; // null nếu không có item
        public string SentAt;
        public string ExpiredAt;
    }

    // ───────────────────────────────────────────────────────────────────
    // Aliases cho tương thích ngược với code cũ
    // ───────────────────────────────────────────────────────────────────

    // MailResponse = MailSummaryResponse (dùng trong danh sách)
    [System.Serializable]
    public class MailResponse
    {
        public int MailId;
        public string Title;
        public string Type;
        public bool IsRead;
        public bool HasClaimableReward;
        public bool IsClaimed;
        public int? RemainingDays;
        public string SentAt;
        public string ExpiredAt;

        public int? AttachedItemId => HasClaimableReward ? 0 : (int?)null;
        public int AttachedItemQuantity => 0;
    }

}
