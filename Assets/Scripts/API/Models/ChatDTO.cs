using System;

namespace MysticJourney.API.Models.Request
{
    [Serializable]
    public class SendWorldChatMessageRequest
    {
        public string Content { get; set; }
    }

    [Serializable]
    public class SendFriendChatMessageRequest
    {
        public int RecipientId { get; set; }
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
        public int? ReportedById { get; set; }
        public string ReportReason { get; set; }
        public string ReportedAt { get; set; }
        public string SentAt { get; set; }
    }

    [Serializable]
    public class FriendChatMessageResponse
    {
        public int ChatMessageId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderAvatarUrl { get; set; }
        public int RecipientId { get; set; }
        public string RecipientName { get; set; }
        public string RecipientAvatarUrl { get; set; }
        public string Content { get; set; }
        public bool IsReported { get; set; }
        public bool IsHidden { get; set; }
        public int? ReportedById { get; set; }
        public string ReportReason { get; set; }
        public string ReportedAt { get; set; }
        public string SentAt { get; set; }
    }

    [Serializable]
    public class ContentSafetyCategoryResponse
    {
        public string Category { get; set; }
        public int Severity { get; set; }
    }

    [Serializable]
    public class ChatModerationResultResponse
    {
        public bool IsToxic { get; set; }
        public bool ChatLocked { get; set; }
        public int LockLevel { get; set; }
        public int ViolationCount { get; set; }
        public string LockedUntil { get; set; }
        public int LockDurationSeconds { get; set; }
        public int MaxSeverity { get; set; }
        public int SeverityThreshold { get; set; }
        public string[] MatchedTerms { get; set; }
        public ContentSafetyCategoryResponse[] Categories { get; set; }
        public string WarningMessage { get; set; }
    }

    [Serializable]
    public class ReportWorldChatMessageResponse
    {
        public WorldChatMessageResponse Message { get; set; }
        public ChatModerationResultResponse Moderation { get; set; }
    }

    [Serializable]
    public class ReportFriendChatMessageResponse
    {
        public FriendChatMessageResponse Message { get; set; }
        public ChatModerationResultResponse Moderation { get; set; }
    }
}