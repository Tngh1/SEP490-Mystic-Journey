using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════
    // SHOP API - Cửa hàng
    // ═══════════════════════════════════════════════════════════════
    public class ShopApi : BaseApiService<ShopApi>
    {
        // ═══════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════

        // ── Lấy danh sách shop items ─────────────────────
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

        // ── Lấy shop item theo ID ───────────────────────
        public void GetById(int shopItemId, Action<ShopItemResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.ShopItemById, shopItemId);
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
