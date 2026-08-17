using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class DailyLoginApi : BaseApiService<DailyLoginApi>
    {

        // Executes get current month operation.
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

        // Executes claim operation.
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

        // Executes retro claim operation.
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
