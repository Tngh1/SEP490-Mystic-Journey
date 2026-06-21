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

        public void GetStatus(Action<PlayerDailyLoginResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get<ApiResponse<PlayerDailyLoginResponse>>(
                ApiConfig.DailyLoginStatus,
                response => onSuccess?.Invoke(response.Data),
                onError,
                requiresAuth: true);
        }

        public void Claim(Action<ClaimDailyRewardResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.PostEmpty<ApiResponse<ClaimDailyRewardResponse>>(
                ApiConfig.DailyLoginClaim,
                response => onSuccess?.Invoke(response.Data),
                onError,
                requiresAuth: true);
        }
    }
}
