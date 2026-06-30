using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════════════
    // MAIL API - Thư
    // ═══════════════════════════════════════════════════════════════════════
    public class MailApi : BaseApiService<MailApi>
    {
        // ═══════════════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════════════

        // ── Lấy danh sách mail (overload cho tương thích) ───────────
        public void GetMyMails(
            Action<MailListPagedResponse> onSuccess,
            Action<ApiException> onError)
        {
            GetMyMails(1, 20, onSuccess, onError);
        }

        // ── Lấy danh sách mail có phân trang ──────────────────────
        public void GetMyMails(
            int page,
            int pageSize,
            Action<MailListPagedResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = $"{ApiConfig.MailMe}?page={page}&pageSize={pageSize}";
            SafeDebugLog($"GetMyMails → page={page} pageSize={pageSize}");
            
            // ApiClient đã xử lý success:false và unwrap envelope, nhận trực tiếp MailListPagedResponse
            ApiClient.Instance.Get<MailListPagedResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetMyMails OK | TotalMails={response.TotalMails} | TotalPages={response.TotalPages} | Items={response.Items?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMyMails FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Lấy chi tiết mail ─────────────────────────────────────
        public void GetById(
            int mailId,
            Action<MailDetailResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailById, mailId);
            SafeDebugLog($"GetById → mailId={mailId}");
            
            ApiClient.Instance.Get<MailDetailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetById OK | Title={response.Title} | IsRead={response.IsRead} | IsClaimed={response.IsClaimed}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetById FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Đánh dấu đã đọc ──────────────────────────────────────
        public void MarkAsRead(
            int mailId,
            Action<MailDetailResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailRead, mailId);
            SafeDebugLog($"MarkAsRead → mailId={mailId}");
            
            ApiClient.Instance.PostEmpty<MailDetailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"MarkAsRead OK | mailId={mailId} | IsRead={response.IsRead}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"MarkAsRead FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Nhận phần thưởng mail ────────────────────────────────
        public void ClaimReward(
            int mailId,
            Action<MailDetailResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailClaim, mailId);
            SafeDebugLog($"ClaimReward → mailId={mailId}");
            
            ApiClient.Instance.PostEmpty<MailDetailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"ClaimReward OK | mailId={mailId} | IsClaimed={response.IsClaimed} | Gold={response.AttachedGold} | Gems={response.AttachedGems}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ClaimReward FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Xóa mail ──────────────────────────────────────────────
        public void Delete(
            int mailId,
            Action<SimpleResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.MailById, mailId);
            SafeDebugLog($"Delete → mailId={mailId}");
            
            ApiClient.Instance.Delete<SimpleResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"Delete OK | mailId={mailId}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Delete FAIL | mailId={mailId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
