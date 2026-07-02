using System;

namespace MysticJourney.API.Models.Request
{
    [Serializable]
    public class SendWorldChatMessageRequest
    {
        public string Content { get; set; }
    }

    [Serializable]
    public class ReportChatMessageRequest
    {
        public int ChatMessageId { get; set; }
        public string Reason { get; set; }
    }
}

namespace MysticJourney.API.Models.Response
{
    [Serializable]
    public class WorldChatMessageResponse
    {
        public int ChatMessageId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderAvatarUrl { get; set; }
        public string Channel { get; set; } = "World";
        public string Content { get; set; }
        public bool IsReported { get; set; }
        public bool IsHidden { get; set; }
        public string SentAt { get; set; }
    }
}
