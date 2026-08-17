namespace MysticJourney.API.Models.Response
{
    // Initializes a new default instance of the ApiException class.
    public class ApiException
    {
        // Executes status code operation.
        public long StatusCode { get; set; }
        // Executes error code operation.
        public string ErrorCode { get; set; }
        // Executes message operation.
        public string Message { get; set; }
        // Executes raw body operation.
        public string RawBody { get; set; }

        // Executes to string operation.
        public override string ToString()
        {
            return $"[{StatusCode}] {ErrorCode}: {Message}";
        }
    }

    // Executes error body response operation.
    [System.Serializable]
    internal class ErrorBodyResponse
    {
        // Executes message operation.
        public string message { get; set; }
        // Executes error code operation.
        public string errorCode { get; set; }
        // Executes error operation.
        public string error { get; set; }
    }
}
