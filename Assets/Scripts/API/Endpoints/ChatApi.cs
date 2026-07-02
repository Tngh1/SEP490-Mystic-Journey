using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class ChatApi : BaseApiService<ChatApi>
    {
        private const int DefaultWorldPage = 1;
        private const int DefaultWorldPageSize = 50;
        private const int MaxWorldPageSize = 100;

        public void GetWorldMessages(
            Action<PagedResultResponse<WorldChatMessageResponse>> onSuccess,
            Action<ApiException> onError)
        {
            GetWorldMessages(DefaultWorldPage, DefaultWorldPageSize, onSuccess, onError);
        }

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

        public void ReportWorldMessage(
            int chatMessageId,
            string reason,
            Action<WorldChatMessageResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new ReportChatMessageRequest
            {
                ChatMessageId = chatMessageId,
                Reason = reason != null ? reason.Trim() : null
            };

            SafeDebugLog($"ReportWorldMessage -> ChatMessageId={chatMessageId}");
            ApiClient.Instance.Post<ReportChatMessageRequest, WorldChatMessageResponse>(
                ApiConfig.ChatWorldReport,
                body,
                response =>
                {
                    SafeDebugLog($"ReportWorldMessage OK | ChatMessageId={response?.ChatMessageId ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ReportWorldMessage FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
