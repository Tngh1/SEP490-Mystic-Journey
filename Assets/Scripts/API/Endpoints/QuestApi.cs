using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class QuestApi : BaseApiService<QuestApi>
    {

        // Executes get by id operation.
        public void GetById(int questId, Action<QuestResponse> onSuccess, Action<ApiException> onError)
        {
            var endpoint = string.Format(ApiConfig.QuestById, questId);
            ApiClient.Instance.Get<QuestResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetById OK | questId={questId} | Title={response.Title}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetById FAIL | questId={questId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
