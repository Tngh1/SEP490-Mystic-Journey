using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Tương ứng GachaBannersController → GET /api/gachabanners
    // GetAll và GetById: không cần auth
    public class GachaApi : MonoBehaviour
    {
        private static GachaApi _instance;

        public static GachaApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[GachaApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<GachaApi>();
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

        // GET /api/gachabanners?page=&pageSize=&search=&type=&isActive=
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
            if (!string.IsNullOrEmpty(type))   endpoint += $"&type={type}";
            if (isActive.HasValue)              endpoint += $"&isActive={isActive.Value}";

            Debug.Log($"[GachaApi] GetAll → page={page} pageSize={pageSize}");

            ApiClient.Instance.Get<PaginatedResponse<GachaBannerResponse>>(
                endpoint,
                response =>
                {
                    Debug.Log($"[GachaApi] ✅ GetAll OK | TotalCount={response.TotalCount} | Page={response.Page}/{response.TotalPages}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[GachaApi] ❌ GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        // GET /api/gachabanners/{id}
        // Trả về GachaBannerDetailResponse kèm BannerItems[]
        public void GetById(int gachaBannerId, Action<GachaBannerDetailResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.GachaById, gachaBannerId);
            Debug.Log($"[GachaApi] GetById → gachaBannerId={gachaBannerId}");

            ApiClient.Instance.Get<GachaBannerDetailResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[GachaApi] ✅ GetById OK | Name={response.Name} | PullCost={response.PullCost} | Pity={response.PityLimit} | BannerItems={response.BannerItems?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[GachaApi] ❌ GetById FAIL | gachaBannerId={gachaBannerId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }
    }
}
