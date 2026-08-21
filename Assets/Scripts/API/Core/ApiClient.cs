using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MysticJourney.API.Models.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MysticJourney.API.Core
{
    // Executes mono behaviour operation.
    public class ApiClient : MonoBehaviour
    {
        private static ApiClient _instance;

        // Executes instance operation.
        public static ApiClient Instance
        {
            get
            {
                if (!Application.isPlaying)
                    return null;

                if (_instance == null)  // Entity not found — short-circuit with appropriate error result
                {
                    _instance = ApiRuntimeHost.GetOrAdd<ApiClient>();
                }
                return _instance;
            }
        }

        // Initializes internal component caches and dependencies for ApiClient upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            PreserveAcrossScenes(gameObject);
        }

        // Executes preserve across scenes operation.
        private static void PreserveAcrossScenes(GameObject go)
        {
            if (go == null) return;  // Entity not found — short-circuit with appropriate error result

            if (go.transform.parent != null)
                go.transform.SetParent(null);

            if (Application.isPlaying)
                DontDestroyOnLoad(go);
        }


        private string _cachedToken = null;
        private string _cachedRefreshToken = null;
        private bool _isRefreshing = false;

        // Executes pending get request operation.
        private sealed class PendingGetRequest
        {
            public Type ResponseType;
            public readonly List<Action<object>> SuccessCallbacks = new();
            public readonly List<Action<ApiException>> ErrorCallbacks = new();
        }

        private readonly Dictionary<string, PendingGetRequest> _pendingGets = new();

        // Executes save token operation.
        public void SaveToken(string token)
        {
            _cachedToken = token;
            PlayerPrefs.SetString(ApiConfig.AccessTokenKey, token);
            PlayerPrefs.Save();
            Debug.Log("[ApiClient] Token saved.");
        }

        // Executes save refresh token operation.
        // Validates input parameters against null or empty values.
        public void SaveRefreshToken(string refreshToken)
        {
            _cachedRefreshToken = refreshToken;
            PlayerPrefs.SetString(ApiConfig.RefreshTokenKey, refreshToken);
            PlayerPrefs.Save();
        }

        // Return the cached refresh token when available; otherwise load it from PlayerPrefs and cache the value.
        public string GetRefreshToken()
        {
            if (!string.IsNullOrEmpty(_cachedRefreshToken)) return _cachedRefreshToken;
            _cachedRefreshToken = PlayerPrefs.GetString(ApiConfig.RefreshTokenKey, string.Empty);
            return _cachedRefreshToken;
        }

        // Return the cached access token when available; otherwise load it from PlayerPrefs and cache the value.
        public string GetToken()
        {
            if (!string.IsNullOrEmpty(_cachedToken)) return _cachedToken;
            _cachedToken = PlayerPrefs.GetString(ApiConfig.AccessTokenKey, string.Empty);
            return _cachedToken;
        }

        // Executes clear token operation.
        // Validates input parameters against null or empty values.
        public void ClearToken()
        {
            _cachedToken = null;
            _cachedRefreshToken = null;
            PlayerPrefs.DeleteKey(ApiConfig.AccessTokenKey);
            PlayerPrefs.DeleteKey(ApiConfig.RefreshTokenKey);
            PlayerPrefs.DeleteKey(ApiConfig.PlayerProfileIdKey);
            PlayerPrefs.DeleteKey(ApiConfig.AccountIdKey);
            PlayerPrefs.DeleteKey(ApiConfig.UserNameKey);
            PlayerPrefs.DeleteKey(ApiConfig.PlayerLevelKey);
            PlayerPrefs.DeleteKey(ApiConfig.PlayerClassKey);
            PlayerPrefs.DeleteKey(ApiConfig.LastMapNameKey);
            PlayerPrefs.DeleteKey(ApiConfig.PositionXKey);
            PlayerPrefs.DeleteKey(ApiConfig.PositionYKey);
            PlayerPrefs.Save();
            Debug.Log("[ApiClient] Token cleared.");
        }

        // Executes has token operation.
        // Validates input parameters against null or empty values.
        public bool HasToken()
        {
            return !string.IsNullOrEmpty(GetToken());
        }


        // Send a GET request with optional query parameters, unwrap the API envelope, and return the typed response payload.
        public void Get<T>(string endpoint, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            string key = (requiresAuth ? "auth:" : "anonymous:") + endpoint;
            if (_pendingGets.TryGetValue(key, out var existing))
            {
                if (existing.ResponseType == typeof(T))
                {
                    existing.SuccessCallbacks.Add(value => onSuccess?.Invoke(value is T typed ? typed : default));
                    existing.ErrorCallbacks.Add(error => onError?.Invoke(error));
                    return;
                }

                // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                StartCoroutine(SendCoroutine("GET", endpoint, null, onSuccess, onError, requiresAuth));
                return;
            }

            var pending = new PendingGetRequest { ResponseType = typeof(T) };
            pending.SuccessCallbacks.Add(value => onSuccess?.Invoke(value is T typed ? typed : default));
            pending.ErrorCallbacks.Add(error => onError?.Invoke(error));
            _pendingGets[key] = pending;

            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(SendCoroutine<T>(
                "GET",
                endpoint,
                null,
                response => CompletePendingGet(key, response),
                error => FailPendingGet(key, error),
                requiresAuth));
        }

        // Remove the completed GET entry from the pending map, invoke every success callback with the response, and log callback exceptions without breaking the remaining callbacks.
        private void CompletePendingGet<T>(string key, T response)
        {
            if (!_pendingGets.Remove(key, out var pending)) return;  // Mark entity for deletion in the next SaveChanges call

            foreach (var callback in pending.SuccessCallbacks)
            {
                try { callback(response); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        // Remove the failed GET entry from the pending map, invoke every error callback with the API exception, and log callback exceptions without breaking the remaining callbacks.
        private void FailPendingGet(string key, ApiException error)
        {
            if (!_pendingGets.Remove(key, out var pending)) return;  // Mark entity for deletion in the next SaveChanges call

            foreach (var callback in pending.ErrorCallbacks)
            {
                try { callback(error); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        // Send a POST request with the supplied payload, unwrap the API envelope, and return the typed response payload.
        public void Post<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(SendCoroutine("POST", endpoint, Serialize(body), onSuccess, onError, requiresAuth));
        }

        // Send an authenticated or anonymous POST request with an empty JSON object through the coroutine transport.
        public void PostEmpty<TResponse>(string endpoint, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(SendCoroutine("POST", endpoint, "{}", onSuccess, onError, requiresAuth));
        }

        // Send a PUT request with the supplied payload, unwrap the API envelope, and return the typed response payload.
        public void Put<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(SendCoroutine("PUT", endpoint, Serialize(body), onSuccess, onError, requiresAuth));
        }

        // Delete through the endpoint and return the completed API result.
        public void Delete<T>(string endpoint, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
            StartCoroutine(SendCoroutine("DELETE", endpoint, null, onSuccess, onError, requiresAuth));
        }

        // Serialize a non-null request body to JSON and use an empty JSON object when no body was supplied.
        private static string Serialize<T>(T body)
        {
            return body != null ? JsonConvert.SerializeObject(body) : "{}";
        }

        // Build the UnityWebRequest, attach JSON and authentication headers, await the network result, refresh tokens when required, deserialize successful data, and route failures to the error callback.
        private IEnumerator SendCoroutine<T>(string method, string endpoint, string jsonBody, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            string url = ApiConfig.BaseUrl + endpoint;
            Debug.Log($"[ApiClient] {method} {url}");

            using (var request = new UnityWebRequest(url, method))
            {
                if (jsonBody != null)  // Entity exists — proceed with conditional branch
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = ApiConfig.Timeout;
                SetCommonHeaders(request, requiresAuth);
                yield return request.SendWebRequest();

                if (requiresAuth && request.responseCode == 401)
                {
                    string rt = GetRefreshToken();
                    if (!string.IsNullOrEmpty(rt))
                    {
                        if (_isRefreshing)
                        {
                            while (_isRefreshing) yield return null;
                            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                            yield return StartCoroutine(SendOnce(method, url, jsonBody, onSuccess, onError, requiresAuth));
                            yield break;
                        }

                        var outcome = RefreshOutcome.Rejected;
                        string rejectMessage = null;
                        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                        yield return StartCoroutine(RefreshAccessTokenCoroutine(rt, (r, msg) =>
                        {
                            outcome = r;
                            rejectMessage = msg;
                        }));

                        if (outcome == RefreshOutcome.Success)
                        {
                            Debug.Log($"[ApiClient] Token refreshed. Retrying {method} {url}");
                            // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                            yield return StartCoroutine(SendOnce(method, url, jsonBody, onSuccess, onError, requiresAuth));
                            yield break;
                        }

                        if (outcome == RefreshOutcome.NetworkError)
                        {
                            Debug.LogWarning("[ApiClient] Refresh unreachable (network error). Keeping session.");
                            onError?.Invoke(new ApiException
                            {
                                StatusCode = 0,
                                ErrorCode = "NETWORK_ERROR",
                                Message = "Cannot reach the server. Check your connection and try again."
                            });
                            yield break;
                        }

                        Debug.LogWarning("[ApiClient] Refresh token rejected. Session expired, overridden, or account banned.");
                        ClearToken();
                        var logoutReason = !string.IsNullOrEmpty(rejectMessage)
                            ? rejectMessage
                            : "Your session has ended. Please log in again.";

                        if (logoutReason.ToLower().Contains("invalid refresh token"))
                        {
                            logoutReason = "Your account has been logged in on another device.";
                        }

                        MysticJourney.Core.Services.SessionService.Logout(logoutReason);
                        onError?.Invoke(new ApiException
                        {
                            StatusCode = 401,
                            ErrorCode = "SESSION_EXPIRED",
                            Message = logoutReason
                        });
                        yield break;
                    }
                }

                HandleResponse(request, onSuccess, onError, requiresAuth);
            }
        }

        // Process once using method, url, json body, and on success; it loads bytes, updates common headers, and sends web request and guards invalid or unavailable states.
        private IEnumerator SendOnce<T>(string method, string url, string jsonBody, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                if (jsonBody != null)  // Entity exists — proceed with conditional branch
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = ApiConfig.Timeout;
                SetCommonHeaders(request, requiresAuth);
                yield return request.SendWebRequest();
                HandleResponse(request, onSuccess, onError, requiresAuth);
            }
        }

        // Executes refresh outcome operation.
        // Validates input parameters against null or empty values.
        private enum RefreshOutcome
        {
            Success,
            Rejected,
            NetworkError
        }

        // Executes refresh access token coroutine operation.
        private IEnumerator RefreshAccessTokenCoroutine(string refreshToken, Action<RefreshOutcome, string> onDone)
        {
            _isRefreshing = true;
            string url = ApiConfig.BaseUrl + ApiConfig.AuthRefreshToken;
            string body = JsonConvert.SerializeObject(new { refreshToken });
            var outcome = RefreshOutcome.Rejected;
            string rejectMessage = null;

            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = ApiConfig.Timeout;
                req.SetRequestHeader("Content-Type", ApiConfig.ContentType);
                req.SetRequestHeader("Accept", ApiConfig.Accept);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.ConnectionError ||
                    req.result == UnityWebRequest.Result.DataProcessingError)
                {
                    Debug.LogWarning($"[ApiClient] Token refresh unreachable: {req.error}");
                    outcome = RefreshOutcome.NetworkError;
                }
                else if (req.responseCode < 400)
                {
                    try
                    {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(req.downloadHandler.text);
                        var data = json["data"];
                        string newAccessToken = data?["accessToken"]?.ToString() ?? data?["AccessToken"]?.ToString();
                        string newRefreshToken = data?["refreshToken"]?.ToString() ?? data?["RefreshToken"]?.ToString();

                        if (!string.IsNullOrEmpty(newAccessToken))
                        {
                            SaveToken(newAccessToken);
                            if (!string.IsNullOrEmpty(newRefreshToken))
                                SaveRefreshToken(newRefreshToken);
                            Debug.Log("[ApiClient] Access token refreshed successfully.");
                            outcome = RefreshOutcome.Success;
                        }
                        else
                        {
                            Debug.LogWarning("[ApiClient] Refresh response had no access token.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ApiClient] Failed to parse refresh response: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ApiClient] Token refresh rejected. Code={req.responseCode}");
                    try
                    {
                        var errObj = JsonConvert.DeserializeObject<ErrorBodyResponse>(req.downloadHandler?.text ?? string.Empty);
                        rejectMessage = errObj?.message;
                    }
                    catch
                    {
                    }
                }
            }

            _isRefreshing = false;
            onDone?.Invoke(outcome, rejectMessage);
        }


        // Executes set common headers operation.
        // Validates input parameters against null or empty values.
        private void SetCommonHeaders(UnityWebRequest request, bool requiresAuth)
        {
            request.SetRequestHeader("Content-Type", ApiConfig.ContentType);
            request.SetRequestHeader("Accept", ApiConfig.Accept);

            if (requiresAuth)
            {
                string token = GetToken();
                if (!string.IsNullOrEmpty(token))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                }
                else
                {
                    Debug.LogWarning("[ApiClient] requiresAuth=true nhưng không có token! Gọi AuthApi.LoginGame() trước.");
                }
            }
        }

        // Process the supplied values: normalizes or validates the text before returning the derived result and converts the payload between the runtime object and its JSON representation.
        private void HandleResponse<T>(UnityWebRequest request, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            string rawBody = request.downloadHandler?.text ?? string.Empty;

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogError($"[ApiClient] ❌ Network Error: {request.error}");
                MysticJourney.Networking.NetworkReconnectManager.Instance?.ReportNetworkError();
                onError?.Invoke(new ApiException
                {
                    StatusCode = 0,
                    ErrorCode = "NETWORK_ERROR",
                    Message = request.error,
                    RawBody = rawBody
                });
                return;
            }

            if (request.result == UnityWebRequest.Result.ProtocolError || request.responseCode >= 400)
            {
                string errorMsg = rawBody;
                string errorCode = "HTTP_ERROR";

                try
                {
                    var errorObject = JObject.Parse(rawBody);
                    errorMsg = ExtractStructuredErrorMessage(errorObject) ?? rawBody;
                    errorCode = ReadString(errorObject, "errorCode")
                             ?? ReadString(errorObject, "error")
                             ?? (errorObject.Property("errors", StringComparison.OrdinalIgnoreCase) != null
                                 ? "VALIDATION_ERROR"
                                 : errorCode);
                }
                catch
                {
                }

                Debug.LogError($"[ApiClient] ❌ HTTP {request.responseCode} on {request.url} | ErrorCode={errorCode} | Message={errorMsg}");
                Debug.LogError($"[ApiClient] Raw body: {rawBody}");

                if (requiresAuth &&
                    (request.responseCode == 401 || string.Equals(errorCode, "SESSION_OVERRIDDEN", StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.LogWarning("[ApiClient] Session overridden or unauthorized. Clearing token and logging out to MainMenu.");
                    ClearToken();

                    string logoutReason = errorMsg;
                    if (string.IsNullOrEmpty(logoutReason) || logoutReason.ToLower().Contains("invalid refresh token"))  // Mandatory string argument is null or empty — fail fast
                    {
                        logoutReason = "Your account has been logged in on another device.";
                    }

                    MysticJourney.Core.Services.SessionService.Logout(logoutReason);
                }

                onError?.Invoke(new ApiException
                {
                    StatusCode = request.responseCode,
                    ErrorCode = errorCode,
                    Message = errorMsg,
                    RawBody = rawBody
                });
                return;
            }

            T result = default;
            try
            {
                var json = string.IsNullOrWhiteSpace(rawBody) ? null : JToken.Parse(rawBody);
                var envelope = json as JObject;
                var successToken = envelope?.Property("success", StringComparison.OrdinalIgnoreCase)?.Value;
                bool isEnvelope = successToken != null && successToken.Type == JTokenType.Boolean;

                if (isEnvelope && !successToken.Value<bool>())
                {
                    string errCode = ReadString(envelope, "errorCode") ?? "BUSINESS_ERROR";
                    string errText = ReadString(envelope, "message") ?? "Request failed";
                    Debug.LogWarning($"[ApiClient] ⚠️ BE returned success=false | ErrorCode={errCode} | Message={errText}");

                    onError?.Invoke(new ApiException
                    {
                        StatusCode = request.responseCode,
                        ErrorCode = errCode,
                        Message = errText,
                        RawBody = rawBody
                    });
                    return;
                }

                result = UnwrapEnvelope<T>(json, envelope, isEnvelope);
                Debug.Log($"[ApiClient] ✅ {request.responseCode} OK | type={typeof(T).Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ApiClient] ❌ Parse Error | type={typeof(T).Name} | exception={ex.Message}");
                Debug.LogError($"[ApiClient] Raw body: {rawBody}");

                onError?.Invoke(new ApiException
                {
                    StatusCode = request.responseCode,
                    ErrorCode = "PARSE_ERROR",
                    Message = $"Failed to parse JSON into {typeof(T).Name}: {ex.Message}",
                    RawBody = rawBody
                });
                return;
            }

            MysticJourney.Networking.NetworkReconnectManager.Instance?.ReportNetworkSuccess();

            onSuccess?.Invoke(result);
        }

        // Executes read string operation.
        private static string ReadString(JObject obj, string name)
        {
            var value = obj?.Property(name, StringComparison.OrdinalIgnoreCase)?.Value;
            return value == null || value.Type == JTokenType.Null ? null : value.ToString();
        }

        private static string ExtractStructuredErrorMessage(JObject errorObject)
        {
            if (errorObject == null)
                return null;

            string directMessage = ReadString(errorObject, "message");
            if (!string.IsNullOrWhiteSpace(directMessage))
                return directMessage;

            var errorsToken = errorObject
                .Property("errors", StringComparison.OrdinalIgnoreCase)
                ?.Value as JObject;

            if (errorsToken != null)
            {
                var messages = new List<string>();
                foreach (var errorProperty in errorsToken.Properties())
                {
                    if (errorProperty.Value is JArray errorArray)
                    {
                        foreach (var entry in errorArray)
                        {
                            string text = entry?.ToString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(text) && !messages.Contains(text))
                                messages.Add(text);
                        }
                    }
                    else
                    {
                        string text = errorProperty.Value?.ToString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(text) && !messages.Contains(text))
                            messages.Add(text);
                    }
                }

                if (messages.Count > 0)
                    return string.Join("\n", messages);
            }

            return ReadString(errorObject, "detail")
                ?? ReadString(errorObject, "title");
        }

        // Process unwrap envelope using json, envelope, and is envelope; it loads generic type definition and guards invalid or unavailable states.
        private static T UnwrapEnvelope<T>(JToken json, JObject envelope, bool isEnvelope)
        {
            if (json == null)  // Entity not found — short-circuit with appropriate error result
                return default;

            var targetType = typeof(T);

            if ((targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(ApiResponse<>)) ||
                targetType == typeof(SimpleResponse))
            {
                return json.ToObject<T>();
            }

            if (isEnvelope)
            {
                var data = envelope.Property("data", StringComparison.OrdinalIgnoreCase)?.Value;
                if (data != null && data.Type != JTokenType.Null)
                    return data.ToObject<T>();
            }

            return json.ToObject<T>();
        }

    }
}
