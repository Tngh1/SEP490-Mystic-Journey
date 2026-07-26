using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class ChatApi : BaseApiService<ChatApi>
    {
        private const int MaxWorldPageSize = 100;
        private const int MaxFriendPageSize = 100;

        public void GetWorldMessages(
            int page,
            int pageSize,
            Action<PagedResultResponse<WorldChatMessageResponse>> onSuccess,
            Action<ApiException> onError)
        {
            int safePage = Math.Max(1, page);
            int safePageSize = Math.Max(1, Math.Min(pageSize, MaxWorldPageSize));
            string endpoint = $"{ApiConfig.ChatWorldMessages}?page={safePage}&pageSize={safePageSize}";

            SafeDebugLog($"GetWorldMessages -> page={safePage} pageSize={safePageSize}");
            ApiClient.Instance.Get<PagedResultResponse<WorldChatMessageResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetWorldMessages OK | TotalCount={response?.TotalCount ?? 0} | Items={response?.Items?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetWorldMessages FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void SendWorldMessage(
            string content,
            Action<WorldChatMessageResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new SendWorldChatMessageRequest
            {
                Content = content != null ? content.Trim() : string.Empty
            };

            SafeDebugLog("SendWorldMessage...");
            ApiClient.Instance.Post<SendWorldChatMessageRequest, WorldChatMessageResponse>(
                ApiConfig.ChatWorldSend,
                body,
                response =>
                {
                    SafeDebugLog($"SendWorldMessage OK | ChatMessageId={response?.ChatMessageId ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"SendWorldMessage FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void GetFriendMessages(
            int friendProfileId,
            int page,
            int pageSize,
            Action<PagedResultResponse<FriendChatMessageResponse>> onSuccess,
            Action<ApiException> onError)
        {
            int safePage = Math.Max(1, page);
            int safePageSize = Math.Max(1, Math.Min(pageSize, MaxFriendPageSize));
            string endpoint = $"{ApiConfig.ChatFriendMessages}?recipientId={friendProfileId}&page={safePage}&pageSize={safePageSize}";

            SafeDebugLog($"GetFriendMessages -> friendProfileId={friendProfileId} page={safePage} pageSize={safePageSize}");
            ApiClient.Instance.Get<PagedResultResponse<FriendChatMessageResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetFriendMessages OK | TotalCount={response?.TotalCount ?? 0} | Items={response?.Items?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetFriendMessages FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void SendFriendMessage(
            int friendProfileId,
            string content,
            Action<FriendChatMessageResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new SendFriendChatMessageRequest
            {
                RecipientId = friendProfileId,
                Content = content != null ? content.Trim() : string.Empty
            };

            SafeDebugLog($"SendFriendMessage -> friendProfileId={friendProfileId}");
            ApiClient.Instance.Post<SendFriendChatMessageRequest, FriendChatMessageResponse>(
                ApiConfig.ChatFriendSend,
                body,
                response =>
                {
                    SafeDebugLog($"SendFriendMessage OK | ChatMessageId={response?.ChatMessageId ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"SendFriendMessage FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void ReportWorldMessage(
            int chatMessageId,
            string reason,
            Action<ReportWorldChatMessageResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new ReportChatMessageRequest
            {
                ChatMessageId = chatMessageId,
                Reason = reason != null ? reason.Trim() : null
            };

            SafeDebugLog($"ReportWorldMessage -> ChatMessageId={chatMessageId}");
            ApiClient.Instance.Post<ReportChatMessageRequest, ReportWorldChatMessageResponse>(
                ApiConfig.ChatWorldReport,
                body,
                response =>
                {
                    SafeDebugLog($"ReportWorldMessage OK | ChatMessageId={response?.Message?.ChatMessageId ?? 0} | Locked={response?.Moderation?.ChatLocked ?? false}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ReportWorldMessage FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void ReportFriendMessage(
            int chatMessageId,
            string reason,
            Action<ReportFriendChatMessageResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new ReportChatMessageRequest
            {
                ChatMessageId = chatMessageId,
                Reason = reason != null ? reason.Trim() : null
            };

            SafeDebugLog($"ReportFriendMessage -> ChatMessageId={chatMessageId}");
            ApiClient.Instance.Post<ReportChatMessageRequest, ReportFriendChatMessageResponse>(
                ApiConfig.ChatFriendReport,
                body,
                response =>
                {
                    SafeDebugLog($"ReportFriendMessage OK | ChatMessageId={response?.Message?.ChatMessageId ?? 0} | Locked={response?.Moderation?.ChatLocked ?? false}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ReportFriendMessage FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}