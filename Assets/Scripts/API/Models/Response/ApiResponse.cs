namespace MysticJourney.API.Models.Response
{
    [System.Serializable]
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }

    [System.Serializable]
    public class SimpleResponse
    {
        public string message { get; set; }
    }
}
