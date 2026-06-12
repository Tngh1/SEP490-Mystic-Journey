using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Tương ứng DungeonsController → GET /api/dungeons
    // Không cần auth cho tất cả endpoint
    public class DungeonApi : MonoBehaviour
    {
        private static DungeonApi _instance;

        public static DungeonApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[DungeonApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<DungeonApi>();
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

        // GET /api/dungeons?page=&pageSize=&search=&type=&isActive=
        public void GetAll(
            int page,
            int pageSize,
            Action<PaginatedResponse<DungeonResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string type = null,
            bool? isActive = null)
        {
            string endpoint = $"{ApiConfig.DungeonAll}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={search}";
            if (!string.IsNullOrEmpty(type))   endpoint += $"&type={type}";
            if (isActive.HasValue)              endpoint += $"&isActive={isActive.Value}";

            Debug.Log($"[DungeonApi] GetAll → page={page} pageSize={pageSize}");

            ApiClient.Instance.Get<PaginatedResponse<DungeonResponse>>(
                endpoint,
                response =>
                {
                    Debug.Log($"[DungeonApi] ✅ GetAll OK | TotalCount={response.TotalCount} | Page={response.Page}/{response.TotalPages}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[DungeonApi] ❌ GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        // GET /api/dungeons/{id}
        public void GetById(int dungeonConfigId, Action<DungeonResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonById, dungeonConfigId);
            Debug.Log($"[DungeonApi] GetById → dungeonConfigId={dungeonConfigId}");

            ApiClient.Instance.Get<DungeonResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[DungeonApi] ✅ GetById OK | Name={response.Name} | Type={response.Type} | Difficulty={response.Difficulty}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[DungeonApi] ❌ GetById FAIL | dungeonConfigId={dungeonConfigId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }
    }
}
