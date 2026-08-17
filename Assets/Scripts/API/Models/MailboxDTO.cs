namespace MysticJourney.API.Models.Response
{

    // Initializes a new default instance of the MailboxSummaryResponse class.
    [System.Serializable]
    public class MailboxSummaryResponse
    {
        public int MailboxId;
        public string Title;
        // Mailbox type is a free-form category with System as the current default; the backend does not enforce a closed allowlist.
        public string Type;
        public bool IsRead;
        public bool HasClaimableReward;
        public bool IsClaimed;
        public int? RemainingDays;
        public string SentAt;
        public string ExpiredAt;
    }

    // Executes mailbox list paged response operation.
    [System.Serializable]
    public class MailboxListPagedResponse
    {
        public int TotalMailboxes;
        public int Page;
        public int PageSize;
        public int TotalPages;
        public MailboxSummaryResponse[] Items;
    }

    // Executes mailbox reward item response operation.
    [System.Serializable]
    public class MailboxRewardItemResponse
    {
        public int ItemId;
        public string ItemName;
        public string IconUrl;
        public int Quantity;
    }

    // Executes mailbox detail response operation.
    [System.Serializable]
    public class MailboxDetailResponse
    {
        public int MailboxId;
        public string Title;
        public string Content;
        // Mailbox type is a free-form category with System as the current default; the backend does not enforce a closed allowlist.
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
