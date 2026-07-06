namespace MysticJourney.API.Models.Request
{
    [System.Serializable]
    public class SpendCurrencyRequest
    {
        public string Currency { get; set; } = "Gold";
        public decimal Amount { get; set; }
        public string Reason { get; set; } = "Spend";
    }
}

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
    public class CurrencySpendResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Currency { get; set; }
        public decimal AmountSpent { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public CurrencyBalanceResponse Balance { get; set; }
        public PlayerCurrencyLogResponse Transaction { get; set; }
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