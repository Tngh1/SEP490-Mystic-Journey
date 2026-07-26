using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════
    // ACHIEVEMENT API - Thành tựu
    // ═══════════════════════════════════════════════════════════════
    public class AchievementApi : BaseApiService<AchievementApi>
    {
        // ═══════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════

        // ── Lấy danh sách thành tựu ──────────────
        public void GetAll(
            int page,
            int pageSize,
            Action<PaginatedResponse<AchievementResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string type = null,
            bool? isActive = null)
        {
            string endpoint = $"{ApiConfig.AchievementAll}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={search}";
            if (!string.IsNullOrEmpty(type)) endpoint += $"&type={type}";
            if (isActive.HasValue) endpoint += $"&isActive={isActive.Value}";

            SafeDebugLog($"GetAll → page={page} pageSize={pageSize}");
            ApiClient.Instance.Get<PaginatedResponse<AchievementResponse>>(
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

        public void GetMyAchievements(Action<PlayerMeAchievementsResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMyAchievements → me");
            ApiClient.Instance.Get<PlayerMeAchievementsResponse>(
                ApiConfig.AchievementMe,
                response => onSuccess?.Invoke(response),
                error =>
                {
                    SafeDebugError($"GetMyAchievements FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

    }
}
