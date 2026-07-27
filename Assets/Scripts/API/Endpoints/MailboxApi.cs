using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════════════
    // MAILBOX API - Thư
    // ═══════════════════════════════════════════════════════════════════════
    public class MailboxApi : BaseApiService<MailboxApi>
    {
        // ═══════════════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════════════

        // ── Lấy danh sách thư (overload cho tương thích) ────────────
        public void GetMyMailboxes(
            Action<MailboxListPagedResponse> onSuccess,
            Action<ApiException> onError)
        {
            GetMyMailboxes(1, 20, onSuccess, onError);
        }

        // ── Lấy danh sách thư có phân trang ────────────────────────
        public void GetMyMailboxes(
            int page,
            int pageSize,
            Action<MailboxListPagedResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = $"{ApiConfig.MailMe}?page={page}&pageSize={pageSize}";
            SafeDebugLog($"GetMyMailboxes → page={page} pageSize={pageSize}");

            // ApiClient đã xử lý success:false và unwrap envelope, nhận trực tiếp MailboxListPagedResponse
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

        // ── Lấy chi tiết thư ───────────────────────────────────────
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

        // ── Đánh dấu đã đọc ──────────────────────────────────────
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

        // ── Nhận phần thưởng thư ───────────────────────────────────
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

        // ── Xóa thư ───────────────────────────────────────────────
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
