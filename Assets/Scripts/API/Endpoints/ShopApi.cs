using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Tương ứng ShopItemsController → /api/shopitems
    // GetById và GetAll: không cần auth
    public class ShopApi : MonoBehaviour
    {
        private static ShopApi _instance;

        public static ShopApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ShopApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<ShopApi>();
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

        // GET /api/shopitems?page=&pageSize=&search=&currency=&isActive=
        public void GetAll(
            int page,
            int pageSize,
            Action<PaginatedResponse<ShopItemResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string currency = null,
            bool? isActive = null)
        {
            string endpoint = $"{ApiConfig.ShopItems}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search))   endpoint += $"&search={search}";
            if (!string.IsNullOrEmpty(currency)) endpoint += $"&currency={currency}";
            if (isActive.HasValue)               endpoint += $"&isActive={isActive.Value}";

            Debug.Log($"[ShopApi] GetAll → page={page} pageSize={pageSize}");

            ApiClient.Instance.Get<PaginatedResponse<ShopItemResponse>>(
                endpoint,
                response =>
                {
                    Debug.Log($"[ShopApi] ✅ GetAll OK | TotalCount={response.TotalCount} | Page={response.Page}/{response.TotalPages}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[ShopApi] ❌ GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        // GET /api/shopitems/{id}
        public void GetById(int shopItemId, Action<ShopItemResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = $"{ApiConfig.ShopItems}/{shopItemId}";
            Debug.Log($"[ShopApi] GetById → shopItemId={shopItemId}");

            ApiClient.Instance.Get<ShopItemResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[ShopApi] ✅ GetById OK | ItemName={response.ItemName} | Price={response.Price} | Currency={response.Currency}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[ShopApi] ❌ GetById FAIL | shopItemId={shopItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }
    }
}
