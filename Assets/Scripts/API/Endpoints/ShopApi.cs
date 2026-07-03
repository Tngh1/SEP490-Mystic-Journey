using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine.Networking;

namespace MysticJourney.API.Endpoints
{
    public class ShopApi : BaseApiService<ShopApi>
    {
        public void GetAll(
            int page,
            int pageSize,
            Action<PaginatedResponse<ShopItemResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string currency = null,
            bool? isActive = null)
        {
            string endpoint = $"{ApiConfig.ShopItemAll}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={UnityWebRequest.EscapeURL(search)}";
            if (!string.IsNullOrEmpty(currency)) endpoint += $"&currency={UnityWebRequest.EscapeURL(currency)}";
            if (isActive.HasValue) endpoint += $"&isActive={isActive.Value}";

            SafeDebugLog($"GetAll -> page={page} pageSize={pageSize}");
            ApiClient.Instance.Get<PaginatedResponse<ShopItemResponse>>(
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

        public void GetById(int shopItemId, Action<ShopItemResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.ShopItemById, shopItemId);
            SafeDebugLog($"GetById -> shopItemId={shopItemId}");
            ApiClient.Instance.Get<ShopItemResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetById OK | ItemName={response.ItemName} | Price={response.Price} | Currency={response.Currency}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetById FAIL | shopItemId={shopItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }

        public void GetPlayerShopItems(
            int page,
            int pageSize,
            Action<PagedResultResponse<ShopItemPublicResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string currency = null,
            string itemType = null,
            bool includeSoldOut = false)
        {
            string endpoint = $"{ApiConfig.PlayerShopItems}?page={page}&pageSize={pageSize}&includeSoldOut={includeSoldOut.ToString().ToLowerInvariant()}";
            endpoint = AddQuery(endpoint, "search", search);
            endpoint = AddQuery(endpoint, "currency", currency);
            endpoint = AddQuery(endpoint, "itemType", itemType);

            SafeDebugLog($"GetPlayerShopItems -> page={page} pageSize={pageSize} includeSoldOut={includeSoldOut}");
            ApiClient.Instance.Get<PagedResultResponse<ShopItemPublicResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetPlayerShopItems OK | TotalCount={response?.TotalCount ?? 0} | Items={response?.Items?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetPlayerShopItems FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void PurchaseItem(
            int shopItemId,
            int quantity,
            Action<PurchaseShopItemResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new PurchaseShopItemRequest
            {
                ShopItemId = shopItemId,
                Quantity = Math.Max(1, quantity)
            };

            SafeDebugLog($"PurchaseItem -> shopItemId={body.ShopItemId} quantity={body.Quantity}");
            ApiClient.Instance.Post<PurchaseShopItemRequest, PurchaseShopItemResponse>(
                ApiConfig.PlayerShopPurchase,
                body,
                response =>
                {
                    SafeDebugLog($"PurchaseItem OK | ItemName={response.ItemName} | Quantity={response.Quantity} | Total={response.TotalPrice} {response.Currency}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"PurchaseItem FAIL | shopItemId={body.ShopItemId} quantity={body.Quantity} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        private static string AddQuery(string endpoint, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return endpoint;

            return endpoint + $"&{key}={UnityWebRequest.EscapeURL(value.Trim())}";
        }
    }
}
