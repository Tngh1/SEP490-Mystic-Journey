using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Tương ứng QuestsController → GET /api/quests
    // Không cần auth cho tất cả endpoint
    public class QuestApi : MonoBehaviour
    {
        private static QuestApi _instance;

        public static QuestApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[QuestApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<QuestApi>();
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

        // GET /api/quests?page=&pageSize=&search=&type=&isActive=
        public void GetAll(
            int page,
            int pageSize,
            Action<PaginatedResponse<QuestResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string type = null,
            bool? isActive = null)
        {
            string endpoint = $"{ApiConfig.QuestAll}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={search}";
            if (!string.IsNullOrEmpty(type))   endpoint += $"&type={type}";
            if (isActive.HasValue)              endpoint += $"&isActive={isActive.Value}";

            Debug.Log($"[QuestApi] GetAll → page={page} pageSize={pageSize}");

            ApiClient.Instance.Get<PaginatedResponse<QuestResponse>>(
                endpoint,
                response =>
                {
                    Debug.Log($"[QuestApi] ✅ GetAll OK | TotalCount={response.TotalCount} | Page={response.Page}/{response.TotalPages}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[QuestApi] ❌ GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        // GET /api/quests/{id}
        public void GetById(int questId, Action<QuestResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.QuestById, questId);
            Debug.Log($"[QuestApi] GetById → questId={questId}");

            ApiClient.Instance.Get<QuestResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[QuestApi] ✅ GetById OK | Title={response.Title} | Type={response.Type} | RequiredLevel={response.RequiredLevel}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[QuestApi] ❌ GetById FAIL | questId={questId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }
    }
}
