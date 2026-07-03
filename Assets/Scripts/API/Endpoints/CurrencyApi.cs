using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class CurrencyApi : BaseApiService<CurrencyApi>
    {
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

        public void SpendCurrency(
            string currency,
            decimal amount,
            string reason,
            Action<CurrencySpendResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new SpendCurrencyRequest
            {
                Currency = currency,
                Amount = amount,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Spend" : reason
            };

            SafeDebugLog($"SpendCurrency -> POST /api/currencies/spend | currency={body.Currency} | amount={body.Amount}");
            ApiClient.Instance.Post<SpendCurrencyRequest, CurrencySpendResponse>(
                ApiConfig.CurrencySpend,
                body,
                response =>
                {
                    SafeDebugLog($"SpendCurrency OK | Currency={response.Currency} | BalanceAfter={response.BalanceAfter}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"SpendCurrency FAIL | currency={body.Currency} | amount={body.Amount} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}