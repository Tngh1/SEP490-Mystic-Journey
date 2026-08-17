namespace MysticJourney.API.Models.Response
{
    [System.Serializable]
    public class ApiResponse<T>
    {
        // Executes success operation.
        public bool Success { get; set; }
        // Executes message operation.
        public string Message { get; set; }
        // Executes error code operation.
        public string ErrorCode { get; set; }
        // Executes data operation.
        public T Data { get; set; }
    }

    // Executes simple response operation.
    [System.Serializable]
    public class SimpleResponse
    {
        // Executes message operation.
        public string message { get; set; }
    }
}
