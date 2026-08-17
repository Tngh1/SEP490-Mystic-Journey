using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class CurrencyApi : BaseApiService<CurrencyApi>
    {
        // Executes get my balance operation.
        public void GetMyBalance(Action<CurrencyBalanceResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMyBalance -> GET /api/currencies/me/balance");
            ApiClient.Instance.Get<CurrencyBalanceResponse>(
                ApiConfig.CurrencyBalance,
                response =>
                {
                    SafeDebugLog($"GetMyBalance OK | Gold={response.Gold} | Gems={response.Gems}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMyBalance FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

    }
}
