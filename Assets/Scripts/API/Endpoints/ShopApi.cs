using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine.Networking;

namespace MysticJourney.API.Endpoints
{
    public class ShopApi : BaseApiService<ShopApi>
    {
        public void GetFixedShopItems(
            int page,
            int pageSize,
            Action<PagedResultResponse<ShopItemPublicResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string currency = null,
            string itemType = null,
            bool includeSoldOut = false)
        {
            string endpoint = BuildPlayerShopQuery(ApiConfig.PlayerShopFixed, page, pageSize, search, currency, itemType, includeSoldOut);

            SafeDebugLog($"GetFixedShopItems -> page={page} pageSize={pageSize} itemType={itemType} includeSoldOut={includeSoldOut}");
            ApiClient.Instance.Get<PagedResultResponse<ShopItemPublicResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetFixedShopItems OK | TotalCount={response?.TotalCount ?? 0} | Items={response?.Items?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetFixedShopItems FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void GetDailyDeals(
            Action<PagedResultResponse<ShopItemPublicResponse>> onSuccess,
            Action<ApiException> onError,
            bool includeSoldOut = false)
        {
            string endpoint = BuildPlayerShopQuery(ApiConfig.PlayerShopDailyDeals, 1, 10, null, null, null, includeSoldOut);

            SafeDebugLog($"GetDailyDeals -> includeSoldOut={includeSoldOut}");
            ApiClient.Instance.Get<PagedResultResponse<ShopItemPublicResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetDailyDeals OK | Items={response?.Items?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetDailyDeals FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void GetPlayerShopRefreshStatus(
            Action<ShopRefreshStatusResponse> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog("GetPlayerShopRefreshStatus");
            ApiClient.Instance.Get<ShopRefreshStatusResponse>(
                ApiConfig.PlayerShopRefreshStatus,
                response =>
                {
                    SafeDebugLog($"GetPlayerShopRefreshStatus OK | Remaining={response?.RefreshesRemainingToday ?? 0}/{response?.MaxDailyRefreshes ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetPlayerShopRefreshStatus FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void RefreshPlayerShop(
            int page,
            int pageSize,
            Action<ShopRefreshResponse> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string currency = null,
            string itemType = null,
            bool includeSoldOut = false)
        {
            string endpoint = BuildPlayerShopQuery(ApiConfig.PlayerShopRefresh, 1, 10, null, null, null, includeSoldOut);

            SafeDebugLog($"RefreshPlayerShop -> daily deals includeSoldOut={includeSoldOut}");
            ApiClient.Instance.PostEmpty<ShopRefreshResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"RefreshPlayerShop OK | Remaining={response?.RefreshStatus?.RefreshesRemainingToday ?? 0}/{response?.RefreshStatus?.MaxDailyRefreshes ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"RefreshPlayerShop FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
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

        public void GetSkins(
            Action<SkinShopItemResponse[]> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog("GetSkins");
            ApiClient.Instance.Get<SkinShopItemResponse[]>(
                ApiConfig.PlayerShopSkins,
                response => onSuccess?.Invoke(response ?? Array.Empty<SkinShopItemResponse>()),
                error =>
                {
                    SafeDebugError($"GetSkins FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void PurchaseSkin(
            int skinId,
            Action<PurchaseShopSkinResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new PurchaseShopSkinRequest { SkinId = skinId };
            SafeDebugLog($"PurchaseSkin -> skinId={skinId}");
            ApiClient.Instance.Post<PurchaseShopSkinRequest, PurchaseShopSkinResponse>(
                ApiConfig.PlayerShopSkinPurchase,
                body,
                onSuccess,
                error =>
                {
                    SafeDebugError($"PurchaseSkin FAIL | skinId={skinId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        private static string BuildPlayerShopQuery(
            string baseEndpoint,
            int page,
            int pageSize,
            string search,
            string currency,
            string itemType,
            bool includeSoldOut)
        {
            string endpoint = $"{baseEndpoint}?page={Math.Max(1, page)}&pageSize={Math.Max(1, pageSize)}&includeSoldOut={includeSoldOut.ToString().ToLowerInvariant()}";
            endpoint = AddQuery(endpoint, "search", search);
            endpoint = AddQuery(endpoint, "currency", currency);
            endpoint = AddQuery(endpoint, "itemType", itemType);
            return endpoint;
        }

        private static string AddQuery(string endpoint, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return endpoint;

            return endpoint + $"&{key}={UnityWebRequest.EscapeURL(value.Trim())}";
        }
    }
}
