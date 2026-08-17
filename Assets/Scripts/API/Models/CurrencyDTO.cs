namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the CurrencyBalanceResponse class.
    [System.Serializable]
    public class CurrencyBalanceResponse
    {
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Executes gold operation.
        public decimal Gold { get; set; }
        // Executes gems operation.
        public decimal Gems { get; set; }
        // Executes server time utc operation.
        public string ServerTimeUtc { get; set; }
    }

    // Executes player currency log response operation.
    [System.Serializable]
    public class PlayerCurrencyLogResponse
    {
        // Executes player currency log id operation.
        public int PlayerCurrencyLogId { get; set; }
        // Executes player profile id operation.
        public int PlayerProfileId { get; set; }
        // Supported currencies: Gold or Gems; the selected currency determines which player balance is charged or credited.
        public string Currency { get; set; }
        // Executes type operation.
        public string Type { get; set; }
        // Executes amount operation.
        public decimal Amount { get; set; }
        // Executes balance after operation.
        public decimal BalanceAfter { get; set; }
        // Executes note operation.
        public string Note { get; set; }
        // Executes created at operation.
        public string CreatedAt { get; set; }
    }
}
