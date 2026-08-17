using System;

namespace MysticJourney.API.Models.Request
{
    // Initializes a new default instance of the SendWorldChatMessageRequest class.
    [Serializable]
    public class SendWorldChatMessageRequest
    {
        // Executes content operation.
        public string Content { get; set; }
    }

    // Executes send friend chat message request operation.
    [Serializable]
    public class SendFriendChatMessageRequest
    {
        // Executes recipient id operation.
        public int RecipientId { get; set; }
        // Executes content operation.
        public string Content { get; set; }
    }

    // Executes report chat message request operation.
    [Serializable]
    public class ReportChatMessageRequest
    {
        // Executes chat message id operation.
        public int ChatMessageId { get; set; }
        // Executes reason operation.
        public string Reason { get; set; }
    }
}

namespace MysticJourney.API.Models.Response
{
    // Executes world chat message response operation.
    [Serializable]
    public class WorldChatMessageResponse
    {
        // Executes chat message id operation.
        public int ChatMessageId { get; set; }
        // Executes sender id operation.
        public int SenderId { get; set; }
        // Executes sender name operation.
        public string SenderName { get; set; }
        // Executes sender avatar url operation.
        public string SenderAvatarUrl { get; set; }
        // Executes channel operation.
        public string Channel { get; set; } = "World";
        // Executes content operation.
        public string Content { get; set; }
        // Executes is reported operation.
        public bool IsReported { get; set; }
        // Executes is hidden operation.
        public bool IsHidden { get; set; }
        // Executes reported by id operation.
        public int? ReportedById { get; set; }
        // Executes report reason operation.
        public string ReportReason { get; set; }
        // Executes reported at operation.
        public string ReportedAt { get; set; }
        // Executes sent at operation.
        public string SentAt { get; set; }
    }

    // Executes party chat message response operation.
    [Serializable]
    public class PartyChatMessageResponse
    {
        // Executes sender id operation.
        public int SenderId { get; set; }
        // Executes sender name operation.
        public string SenderName { get; set; }
        // Executes content operation.
        public string Content { get; set; }
        // Executes channel operation.
        public string Channel { get; set; } = "Party";
        // Executes sent at operation.
        public string SentAt { get; set; }
    }

    // Executes friend chat message response operation.
    [Serializable]
    public class FriendChatMessageResponse
    {
        // Executes chat message id operation.
        public int ChatMessageId { get; set; }
        // Executes sender id operation.
        public int SenderId { get; set; }
        // Executes sender name operation.
        public string SenderName { get; set; }
        // Executes sender avatar url operation.
        public string SenderAvatarUrl { get; set; }
        // Executes recipient id operation.
        public int RecipientId { get; set; }
        // Executes recipient name operation.
        public string RecipientName { get; set; }
        // Executes recipient avatar url operation.
        public string RecipientAvatarUrl { get; set; }
        // Executes content operation.
        public string Content { get; set; }
        // Executes is reported operation.
        public bool IsReported { get; set; }
        // Executes is hidden operation.
        public bool IsHidden { get; set; }
        // Executes reported by id operation.
        public int? ReportedById { get; set; }
        // Executes report reason operation.
        public string ReportReason { get; set; }
        // Executes reported at operation.
        public string ReportedAt { get; set; }
        // Executes sent at operation.
        public string SentAt { get; set; }
    }

    // Executes content safety category response operation.
    [Serializable]
    public class ContentSafetyCategoryResponse
    {
        // Executes category operation.
        public string Category { get; set; }
        // Executes severity operation.
        public int Severity { get; set; }
    }

    // Executes chat moderation result response operation.
    [Serializable]
    public class ChatModerationResultResponse
    {
        // Executes is toxic operation.
        public bool IsToxic { get; set; }
        // Executes chat locked operation.
        public bool ChatLocked { get; set; }
        // Executes lock level operation.
        public int LockLevel { get; set; }
        // Executes violation count operation.
        public int ViolationCount { get; set; }
        // Executes locked until operation.
        public string LockedUntil { get; set; }
        // Executes lock duration seconds operation.
        public int LockDurationSeconds { get; set; }
        // Executes max severity operation.
        public int MaxSeverity { get; set; }
        // Executes severity threshold operation.
        public int SeverityThreshold { get; set; }
        // Executes matched terms operation.
        public string[] MatchedTerms { get; set; }
        // Executes categories operation.
        public ContentSafetyCategoryResponse[] Categories { get; set; }
        // Executes warning message operation.
        public string WarningMessage { get; set; }
    }

    // Executes report world chat message response operation.
    [Serializable]
    public class ReportWorldChatMessageResponse
    {
        // Executes message operation.
        public WorldChatMessageResponse Message { get; set; }
        // Executes moderation operation.
        public ChatModerationResultResponse Moderation { get; set; }
    }

    // Executes report friend chat message response operation.
    [Serializable]
    public class ReportFriendChatMessageResponse
    {
        // Executes message operation.
        public FriendChatMessageResponse Message { get; set; }
        // Executes moderation operation.
        public ChatModerationResultResponse Moderation { get; set; }
    }
}
