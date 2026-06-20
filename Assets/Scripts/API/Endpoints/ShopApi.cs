using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

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
            string endpoint = $"{ApiConfig.ShopItems}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={search}";
            if (!string.IsNullOrEmpty(currency)) endpoint += $"&currency={currency}";
            if (isActive.HasValue) endpoint += $"&isActive={isActive.Value}";

            SafeDebugLog($"GetAll → page={page} pageSize={pageSize}");
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
            string endpoint = $"{ApiConfig.ShopItems}/{shopItemId}";
            SafeDebugLog($"GetById → shopItemId={shopItemId}");
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
    }
}
