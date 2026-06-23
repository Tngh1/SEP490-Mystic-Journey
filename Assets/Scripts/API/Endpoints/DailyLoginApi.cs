using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class DailyLoginApi : BaseApiService<DailyLoginApi>
    {
        public void GetAll(int page, int pageSize, Action<PagedResultResponse<DailyLoginRewardResponse>> onSuccess, Action<ApiException> onError)
        {
            var endpoint = $"{ApiConfig.DailyLoginRewards}?page={page}&pageSize={pageSize}";
            ApiClient.Instance.Get<PagedResultResponse<DailyLoginRewardResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetAll OK | TotalCount={response.TotalCount}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }

        /// <summary>Lấy reward cho đúng số ngày tháng hiện tại từ server.</summary>
        public void GetCurrentMonth(Action<System.Collections.Generic.List<DailyLoginRewardResponse>> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get<System.Collections.Generic.List<DailyLoginRewardResponse>>(
                ApiConfig.DailyLoginRewardsCurrentMonth,
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
                requiresAuth: false);
        }

        // BE does not expose /api/dailyloginrewards/status; the actual status is derived client-side after fetching the world state.
        public void GetStatus(Action<PlayerDailyLoginResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetStatus skipped (BE does not expose daily-login/status endpoint).");
            onError?.Invoke(new ApiException
            {
                StatusCode = 0,
                ErrorCode = "NOT_IMPLEMENTED",
                Message = "Daily-login status endpoint is not implemented on the backend."
            });
        }

        public void Claim(Action<ClaimDailyRewardResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("Claim daily login reward...");
            ApiClient.Instance.PostEmpty<ClaimDailyRewardResponse>(
                ApiConfig.DailyLoginClaim,
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

        public void RetroClaim(int dayNumber, Action<ClaimDailyRewardResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"Retro claim daily login reward for day {dayNumber}...");
            var requestBody = new { DayNumber = dayNumber };
            ApiClient.Instance.Post<object, ClaimDailyRewardResponse>(
                ApiConfig.DailyLoginClaim.Replace("/claim", "/retro-claim"),
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