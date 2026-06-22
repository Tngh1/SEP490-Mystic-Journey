namespace MysticJourney.API.Models.Response
{
    public class ApiException
    {
        public long StatusCode { get; set; }
        public string ErrorCode { get; set; }
        public string Message { get; set; }
        public string RawBody { get; set; }

        public override string ToString()
        {
            return $"[{StatusCode}] {ErrorCode}: {Message}";
        }
    }

    [System.Serializable]
    internal class ErrorBodyResponse
    {
        public string message { get; set; }
        public string errorCode { get; set; }
        public string error { get; set; }
    }
}
