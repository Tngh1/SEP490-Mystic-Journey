using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════
    // DAILY LOGIN API - Thưởng đăng nhập hàng ngày
    // ═══════════════════════════════════════════════════════════════
    public class DailyLoginApi : BaseApiService<DailyLoginApi>
    {
        // ═══════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════

        // ── Lấy rewards tháng hiện tại ──────────────
        public void GetCurrentMonth(Action<System.Collections.Generic.List<DailyLoginRewardResponse>> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get<System.Collections.Generic.List<DailyLoginRewardResponse>>(
                ApiConfig.DailyLoginRewardCurrentMonth,
                response =>
                {
                    SafeDebugLog($"GetCurrentMonth OK | Days={response?.Count}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetCurrentMonth FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Nhận thưởng đăng nhập ────────────────
        public void Claim(Action<ClaimDailyRewardResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("Claim daily login reward...");
            ApiClient.Instance.PostEmpty<ClaimDailyRewardResponse>(
                ApiConfig.WorldDailyLoginClaim,
                response =>
                {
                    SafeDebugLog($"Claim OK | TotalDays={response?.TotalDaysClaimed}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Claim FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Nhận thưởng đăng nhập trễ ──────────
        public void RetroClaim(int dayNumber, Action<ClaimDailyRewardResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"Retro claim daily login reward for day {dayNumber}...");
            var requestBody = new { DayNumber = dayNumber };
            ApiClient.Instance.Post<object, ClaimDailyRewardResponse>(
                ApiConfig.WorldDailyLoginRetroClaim,
                requestBody,
                response =>
                {
                    SafeDebugLog($"RetroClaim OK | TotalDays={response?.TotalDaysClaimed}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"RetroClaim FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
