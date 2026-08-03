using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class WikiApi : BaseApiService<WikiApi>
    {
        public void GetClasses(
            Action<List<ClassConfigDTO>> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog("GetClasses...");
            ApiClient.Instance.Get<List<ClassConfigDTO>>(
                ApiConfig.WikiClasses,
                response =>
                {
                    if (response != null)
                    {
                        SafeDebugLog($"GetClasses OK | Count={response.Count}");
                        onSuccess?.Invoke(response);
                    }
                },
                error =>
                {
                    SafeDebugError($"GetClasses FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }
    }
}
