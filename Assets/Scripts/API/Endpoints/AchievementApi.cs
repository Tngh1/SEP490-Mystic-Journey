using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Tương ứng AchievementsController → GET /api/achievements
    // Không cần auth cho tất cả endpoint
    public class AchievementApi : MonoBehaviour
    {
        private static AchievementApi _instance;

        public static AchievementApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AchievementApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AchievementApi>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // GET /api/achievements?page=&pageSize=&search=&type=&isActive=
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
            if (!string.IsNullOrEmpty(type))   endpoint += $"&type={type}";
            if (isActive.HasValue)              endpoint += $"&isActive={isActive.Value}";

            Debug.Log($"[AchievementApi] GetAll → page={page} pageSize={pageSize}");

            ApiClient.Instance.Get<PaginatedResponse<AchievementResponse>>(
                endpoint,
                response =>
                {
                    Debug.Log($"[AchievementApi] ✅ GetAll OK | TotalCount={response.TotalCount} | Page={response.Page}/{response.TotalPages}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[AchievementApi] ❌ GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        // GET /api/achievements/{id}
        public void GetById(int achievementId, Action<AchievementResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.AchievementById, achievementId);
            Debug.Log($"[AchievementApi] GetById → achievementId={achievementId}");

            ApiClient.Instance.Get<AchievementResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[AchievementApi] ✅ GetById OK | Name={response.Name} | Type={response.Type} | Point={response.Point}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[AchievementApi] ❌ GetById FAIL | achievementId={achievementId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }
    }
}
