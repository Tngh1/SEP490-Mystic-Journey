using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class AchievementApi : BaseApiService<AchievementApi>
    {

        // ─── Player APIs ───────────────────────────────────────────────────────
        // Load all using page, page size, on success, and on error; it sends the GET API request and guards invalid or unavailable states.
        public void GetAll(
            int page,
            int pageSize,
            Action<PaginatedResponse<AchievementResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            // Supported achievement types: Combat, Exploration, Social, Collection, or Progression; the type selects the tracked activity category.
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
                requiresAuth: true);
        }

        // Executes get my achievements operation.
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

        // Executes unlock achievement operation.
        public void UnlockAchievement(int playerAchievementId, Action<PlayerAchievementResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.AchievementUnlock, playerAchievementId);
            SafeDebugLog($"UnlockAchievement → {endpoint}");
            ApiClient.Instance.PostEmpty<PlayerAchievementResponse>(
                endpoint,
                response => onSuccess?.Invoke(response),
                error =>
                {
                    SafeDebugError($"UnlockAchievement FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
