using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class WikiApi : BaseApiService<WikiApi>
    {
        // ─── Guest APIs ───────────────────────────────────────────────────────
        // Load classes using on success and on error; it sends the GET API request and guards invalid or unavailable states.
        public void GetClasses(
            Action<List<ClassConfigDTO>> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog("GetClasses...");
            ApiClient.Instance.Get<List<ClassConfigDTO>>(
                ApiConfig.WikiClasses,
                response =>
                {
                    if (response != null)  // Entity exists — proceed with conditional branch
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
