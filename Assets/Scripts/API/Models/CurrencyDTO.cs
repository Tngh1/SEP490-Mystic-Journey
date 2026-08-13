namespace MysticJourney.API.Models.Response
{
    [System.Serializable]
    public class CurrencyBalanceResponse
    {
        public int PlayerProfileId { get; set; }
        public decimal Gold { get; set; }
        public decimal Gems { get; set; }
        public string ServerTimeUtc { get; set; }
    }

    [System.Serializable]
    public class PlayerCurrencyLogResponse
    {
        public int PlayerCurrencyLogId { get; set; }
        public int PlayerProfileId { get; set; }
        public string Currency { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
    }
}
