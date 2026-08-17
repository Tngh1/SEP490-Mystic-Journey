using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class GachaApi : BaseApiService<GachaApi>
    {

        // Executes get by id operation.
        public void GetById(int gachaBannerId, Action<GachaBannerDetailResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.GachaById, gachaBannerId);
            SafeDebugLog($"GetById → gachaBannerId={gachaBannerId}");
            ApiClient.Instance.Get<GachaBannerDetailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetById OK | Name={response.Name} | PullCost={response.PullCost} | Pity={response.PityLimit} | BannerItems={response.BannerItems?.Count ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetById FAIL | gachaBannerId={gachaBannerId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Validate the active banner, player, payment item, pull count, and pity state; spend currency, select rewards by weighted chance, update inventory and history atomically, then return every pull result.
        public void Pull(
            int bannerId,
            int pullCount,
            bool isFreePull,
            Action<MultiPullResultResponse> onSuccess,
            Action<ApiException> onError)
        {
            var endpoint = string.Format(ApiConfig.GachaPull, bannerId);
            var body = new MysticJourney.API.Models.Request.GachaPullRequest {
                GachaBannerId = bannerId,
                PullCount = pullCount,
                IsFreePull = isFreePull
            };

            ApiClient.Instance.Post<MysticJourney.API.Models.Request.GachaPullRequest, MultiPullResultResponse>(
                endpoint,
                body,
                response =>
                {
                    SafeDebugLog($"Pull OK | bannerId={bannerId} | count={pullCount}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Pull FAIL | bannerId={bannerId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Load history using page, page size, on success, and on error; it sends the GET API request.
        public void GetHistory(
            int page,
            int pageSize,
            Action<PaginatedResponse<GachaPullHistoryResponse>> onSuccess,
            Action<ApiException> onError)
        {
            var endpoint = $"{ApiConfig.GachaHistory}?page={page}&pageSize={pageSize}";

            ApiClient.Instance.Get<PaginatedResponse<GachaPullHistoryResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetHistory OK | TotalCount={response.TotalCount}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetHistory FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
