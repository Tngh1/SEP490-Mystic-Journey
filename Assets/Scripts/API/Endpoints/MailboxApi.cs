using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class MailboxApi : BaseApiService<MailboxApi>
    {
        // ─── Player APIs ───────────────────────────────────────────────────────
        // Load my mailboxes using on success and on error and returns the computed result.
        public void GetMyMailboxes(
            Action<MailboxListPagedResponse> onSuccess,
            Action<ApiException> onError)
        {
            GetMyMailboxes(1, 20, onSuccess, onError);
        }

        // Load my mailboxes using page, page size, on success, and on error; it sends the GET API request.
        public void GetMyMailboxes(
            int page,
            int pageSize,
            Action<MailboxListPagedResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = $"{ApiConfig.MailMe}?page={page}&pageSize={pageSize}";
            SafeDebugLog($"GetMyMailboxes → page={page} pageSize={pageSize}");

            ApiClient.Instance.Get<MailboxListPagedResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetMyMailboxes OK | TotalMailboxes={response.TotalMailboxes} | TotalPages={response.TotalPages} | Items={response.Items?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMyMailboxes FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Load by id using mailbox id, on success, and on error; it sends the GET API request.
        public void GetById(
            int mailboxId,
            Action<MailboxDetailResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailById, mailboxId);
            SafeDebugLog($"GetById → mailboxId={mailboxId}");

            ApiClient.Instance.Get<MailboxDetailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetById OK | Title={response.Title} | IsRead={response.IsRead} | IsClaimed={response.IsClaimed}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetById FAIL | mailboxId={mailboxId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Process as read using mailbox id, on success, and on error; it sends the POST API request.
        public void MarkAsRead(
            int mailboxId,
            Action<MailboxDetailResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailRead, mailboxId);
            SafeDebugLog($"MarkAsRead → mailboxId={mailboxId}");

            ApiClient.Instance.PostEmpty<MailboxDetailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"MarkAsRead OK | mailboxId={mailboxId} | IsRead={response.IsRead}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"MarkAsRead FAIL | mailboxId={mailboxId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Process claim reward using mailbox id, on success, and on error; it sends the POST API request.
        public void ClaimReward(
            int mailboxId,
            Action<MailboxDetailResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailClaim, mailboxId);
            SafeDebugLog($"ClaimReward → mailboxId={mailboxId}");

            ApiClient.Instance.PostEmpty<MailboxDetailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"ClaimReward OK | mailboxId={mailboxId} | IsClaimed={response.IsClaimed} | Gold={response.AttachedGold} | Gems={response.AttachedGems}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ClaimReward FAIL | mailboxId={mailboxId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Delete through the endpoint and return the completed API result.
        public void Delete(
            int mailboxId,
            Action<SimpleResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailById, mailboxId);
            SafeDebugLog($"Delete → mailboxId={mailboxId}");

            ApiClient.Instance.Delete<SimpleResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"Delete OK | mailboxId={mailboxId}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Delete FAIL | mailboxId={mailboxId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
