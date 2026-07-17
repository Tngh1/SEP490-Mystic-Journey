using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════
    // GACHA API - Quay thưởng
    // ═══════════════════════════════════════════════════════════════
    public class GachaApi : BaseApiService<GachaApi>
    {
        // ═══════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════

        // ── Lấy danh sách banner gacha ────────────────
        public void GetAll(
            int page,
            int pageSize,
            Action<PaginatedResponse<GachaBannerResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string type = null,
            bool? isActive = null)
        {
            string endpoint = $"{ApiConfig.GachaAll}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={search}";
            if (!string.IsNullOrEmpty(type)) endpoint += $"&type={type}";
            if (isActive.HasValue) endpoint += $"&isActive={isActive.Value}";

            SafeDebugLog($"GetAll → page={page} pageSize={pageSize}");
            ApiClient.Instance.Get<PaginatedResponse<GachaBannerResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetAll OK | TotalCount={response.TotalCount} | Page={response.Page}/{response.TotalPages}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }

        // ── Lấy banner gacha theo ID ─────────────────
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
                requiresAuth: false);
        }

        // ── Thực hiện quay gacha ─────────────────────────────
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

        // ── Lấy lịch sử quay ──────────────────────────────
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
