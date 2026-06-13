namespace MysticJourney.API.Models.Response
{
    // Maps MailResponseDto
    [System.Serializable]
    public class MailResponse
    {
        public int MailId { get; set; }
        public int PlayerProfileId { get; set; }
        public string PlayerName { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string Type { get; set; }            // "System", "Event", "Gift"
        public decimal AttachedGold { get; set; }
        public decimal AttachedGems { get; set; }
        public int? AttachedItemId { get; set; }
        public string AttachedItemName { get; set; }
        public int AttachedItemQuantity { get; set; }
        public bool IsRead { get; set; }
        public bool IsClaimed { get; set; }
        public bool IsDeleted { get; set; }
        public string DeletedAt { get; set; }
        public string SentAt { get; set; }
        public string ExpiredAt { get; set; }
    }
}
