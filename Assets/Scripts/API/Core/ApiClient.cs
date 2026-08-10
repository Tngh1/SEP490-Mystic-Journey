using System;
using System.Collections;
using System.Text;
using MysticJourney.API.Models.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MysticJourney.API.Core
{
    // Singleton MonoBehaviour - trái tim của hệ thống API.
    // Chạy Coroutine để gọi HTTP, quản lý JWT token trong PlayerPrefs.
    // Không cần attach vào GameObject; tự tạo khi gọi ApiClient.Instance.
    public class ApiClient : MonoBehaviour
    {
        private static ApiClient _instance;

        // Trả về instance duy nhất; tự tạo nếu chưa tồn tại.
        public static ApiClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ApiClient]");
                    PreserveAcrossScenes(go);
                    _instance = go.AddComponent<ApiClient>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            // Đảm bảo chỉ tồn tại đúng 1 instance khi load scene mới
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            PreserveAcrossScenes(gameObject);
        }

        private static void PreserveAcrossScenes(GameObject go)
        {
            if (go == null) return;

            if (go.transform.parent != null)
                go.transform.SetParent(null);

            if (Application.isPlaying)
                DontDestroyOnLoad(go);
        }

        // ── Token Management ──────────────────────────────────────

        private string _cachedToken = null;
        private string _cachedRefreshToken = null;
        private bool _isRefreshing = false; // Tránh nhiều request refresh cùng lúc

        // Lưu JWT access token vào PlayerPrefs sau khi login thành công
        public void SaveToken(string token)
        {
            _cachedToken = token;
            PlayerPrefs.SetString(ApiConfig.AccessTokenKey, token);
            PlayerPrefs.Save();
            Debug.Log("[ApiClient] Token saved.");
        }

        // Lưu Refresh Token vào PlayerPrefs
        public void SaveRefreshToken(string refreshToken)
        {
            _cachedRefreshToken = refreshToken;
            PlayerPrefs.SetString(ApiConfig.RefreshTokenKey, refreshToken);
            PlayerPrefs.Save();
        }

        // Lấy Refresh Token
        public string GetRefreshToken()
        {
            if (!string.IsNullOrEmpty(_cachedRefreshToken)) return _cachedRefreshToken;
            _cachedRefreshToken = PlayerPrefs.GetString(ApiConfig.RefreshTokenKey, string.Empty);
            return _cachedRefreshToken;
        }

        // Lấy token hiện tại từ PlayerPrefs (trống nếu chưa login)
        public string GetToken()
        {
            if (!string.IsNullOrEmpty(_cachedToken)) return _cachedToken;
            _cachedToken = PlayerPrefs.GetString(ApiConfig.AccessTokenKey, string.Empty);
            return _cachedToken;
        }

        // Xóa token và toàn bộ dữ liệu phiên khi logout
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

        // Kiểm tra người dùng có đang đăng nhập không
        public bool HasToken()
        {
            return !string.IsNullOrEmpty(GetToken());
        }

        // ── HTTP Methods ──────────────────────────────────────────
        //
        // requiresAuth mặc định TRUE cho mọi verb. Trước đây Get/Post/PostEmpty
        // mặc định false, nên quên tham số là gửi request KHÔNG có header
        // Authorization — và vì luồng refresh token trong SendCoroutine chỉ chạy
        // khi requiresAuth=true, những call đó gặp 401 là chết luôn, không
        // refresh. Chỉ endpoint thật sự công khai (LoginGame) mới truyền false.

        // Gửi GET request và parse response thành kiểu T
        public void Get<T>(string endpoint, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            StartCoroutine(SendCoroutine("GET", endpoint, null, onSuccess, onError, requiresAuth));
        }

        // Gửi POST request với JSON body và parse response thành kiểu T
        public void Post<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            StartCoroutine(SendCoroutine("POST", endpoint, Serialize(body), onSuccess, onError, requiresAuth));
        }

        // Gửi POST không có body (dùng cho logout, mark-as-read, claim reward...)
        public void PostEmpty<TResponse>(string endpoint, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            StartCoroutine(SendCoroutine("POST", endpoint, "{}", onSuccess, onError, requiresAuth));
        }

        // Gửi PUT request với JSON body và parse response thành kiểu T
        public void Put<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            StartCoroutine(SendCoroutine("PUT", endpoint, Serialize(body), onSuccess, onError, requiresAuth));
        }

        // Gửi DELETE request và parse response thành kiểu T
        public void Delete<T>(string endpoint, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            StartCoroutine(SendCoroutine("DELETE", endpoint, null, onSuccess, onError, requiresAuth));
        }

        private static string Serialize<T>(T body)
        {
            return body != null ? JsonConvert.SerializeObject(body) : "{}";
        }

        // Một coroutine dùng chung cho mọi verb; jsonBody == null nghĩa là không gửi body
        // Tự động retry 1 lần sau khi refresh access token nếu server trả 401.
        private IEnumerator SendCoroutine<T>(string method, string endpoint, string jsonBody, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            string url = ApiConfig.BaseUrl + endpoint;
            Debug.Log($"[ApiClient] {method} {url}");

            // Lần 1: gửi request bình thường
            using (var request = new UnityWebRequest(url, method))
            {
                if (jsonBody != null)
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = ApiConfig.Timeout;
                SetCommonHeaders(request, requiresAuth);
                yield return request.SendWebRequest();

                // Nếu 401 và có refresh token → thử refresh rồi retry
                if (requiresAuth && request.responseCode == 401)
                {
                    string rt = GetRefreshToken();
                    if (!string.IsNullOrEmpty(rt))
                    {
                        // Một request khác đang refresh → chờ nó xong rồi retry, đừng logout.
                        if (_isRefreshing)
                        {
                            while (_isRefreshing) yield return null;
                            yield return StartCoroutine(SendOnce(method, url, jsonBody, onSuccess, onError, requiresAuth));
                            yield break;
                        }

                        var outcome = RefreshOutcome.Rejected;
                        string rejectMessage = null;
                        yield return StartCoroutine(RefreshAccessTokenCoroutine(rt, (r, msg) =>
                        {
                            outcome = r;
                            rejectMessage = msg;
                        }));

                        if (outcome == RefreshOutcome.Success)
                        {
                            Debug.Log($"[ApiClient] Token refreshed. Retrying {method} {url}");
                            yield return StartCoroutine(SendOnce(method, url, jsonBody, onSuccess, onError, requiresAuth));
                            yield break;
                        }

                        if (outcome == RefreshOutcome.NetworkError)
                        {
                            // Không gọi được server để refresh → giữ session, người chơi thử lại khi có mạng.
                            Debug.LogWarning("[ApiClient] Refresh unreachable (network error). Keeping session.");
                            onError?.Invoke(new ApiException
                            {
                                StatusCode = 0,
                                ErrorCode = "NETWORK_ERROR",
                                Message = "Cannot reach the server. Check your connection and try again."
                            });
                            yield break;
                        }

                        // Rejected: server từ chối refresh token (hết hạn, session bị đè, hoặc
                        // tài khoản vừa bị ban). Giữ message của server để lý do ban tới được người chơi.
                        Debug.LogWarning("[ApiClient] Refresh token rejected. Session expired, overridden, or account banned.");
                        ClearToken();
                        var logoutReason = !string.IsNullOrEmpty(rejectMessage)
                            ? rejectMessage
                            : "Your session has ended. Please log in again.";
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

        // Gửi đúng 1 lần, không refresh/retry. Dùng cho lần retry sau khi đã có token mới.
        private IEnumerator SendOnce<T>(string method, string url, string jsonBody, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            using (var request = new UnityWebRequest(url, method))
            {
                if (jsonBody != null)
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = ApiConfig.Timeout;
                SetCommonHeaders(request, requiresAuth);
                yield return request.SendWebRequest();
                HandleResponse(request, onSuccess, onError, requiresAuth);
            }
        }

        // Kết quả của một lần refresh token. Phân biệt "server từ chối" với "không gọi được server".
        private enum RefreshOutcome
        {
            Success,
            Rejected,     // Server trả 4xx → refresh token hết hạn hoặc session bị đè
            NetworkError  // Không kết nối được → session vẫn có thể còn hợp lệ
        }

        // Coroutine gọi /api/auth/refresh-token.
        // Trả về NetworkError nếu không gọi được server (giữ session), Rejected nếu server từ chối.
        // rejectMessage mang message của server khi bị từ chối — tài khoản bị ban giữa phiên
        // sẽ bị chặn ở đây, và lý do ban nằm trong message đó nên không được bỏ đi.
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
                    // Mất mạng / timeout → chưa biết session còn hợp lệ hay không, đừng logout.
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
                        // Body không phải JSON → để null, caller dùng message mặc định.
                    }
                }
            }

            _isRefreshing = false;
            onDone?.Invoke(outcome, rejectMessage);
        }

        // ── Internal Helpers ──────────────────────────────────────

        // Gán Content-Type, Accept và Bearer token vào header của request
        private void SetCommonHeaders(UnityWebRequest request, bool requiresAuth)
        {
            request.SetRequestHeader("Content-Type", ApiConfig.ContentType);
            request.SetRequestHeader("Accept", ApiConfig.Accept);

            if (requiresAuth)
            {
                string token = GetToken();
                if (!string.IsNullOrEmpty(token))
                {
                    // Gắn JWT token theo chuẩn Bearer Authentication
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                }
                else
                {
                    // Không có token → cảnh báo, request sẽ bị 401 từ server
                    Debug.LogWarning("[ApiClient] requiresAuth=true nhưng không có token! Gọi AuthApi.LoginGame() trước.");
                }
            }
        }

        // Xử lý response: kiểm tra lỗi → parse JSON → gọi callback
        private void HandleResponse<T>(UnityWebRequest request, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            string rawBody = request.downloadHandler?.text ?? string.Empty;

            // Lỗi mạng / không kết nối được server
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

            // Lỗi HTTP (4xx, 5xx) từ server
            if (request.result == UnityWebRequest.Result.ProtocolError || request.responseCode >= 400)
            {
                string errorMsg = rawBody;
                string errorCode = "HTTP_ERROR";

                // Parse envelope lỗi để lấy message + errorCode chuẩn
                try
                {
                    var errObj = JsonConvert.DeserializeObject<ErrorBodyResponse>(rawBody);
                    if (errObj != null)
                    {
                        errorMsg = errObj.message ?? rawBody;
                        errorCode = errObj.errorCode ?? errObj.error ?? errorCode;
                    }
                }
                catch
                {
                    // Body không phải JSON → dùng raw text
                }

                Debug.LogError($"[ApiClient] ❌ HTTP {request.responseCode} on {request.url} | ErrorCode={errorCode} | Message={errorMsg}");
                Debug.LogError($"[ApiClient] Raw body: {rawBody}");

                // Chỉ logout khi request CÓ mang token mà vẫn bị từ chối. Login/register dùng
                // requiresAuth=false: sai mật khẩu cũng trả 401, logout ở đó là reload
                // MainMenuScene ngay dưới form đăng nhập.
                if (requiresAuth &&
                    (request.responseCode == 401 || string.Equals(errorCode, "SESSION_OVERRIDDEN", StringComparison.OrdinalIgnoreCase)))
                {
                    Debug.LogWarning("[ApiClient] Session overridden or unauthorized. Clearing token and logging out to MainMenu.");
                    ClearToken();
                    MysticJourney.Core.Services.SessionService.Logout(!string.IsNullOrEmpty(errorMsg) ? errorMsg : "Your session has ended. Please log in again.");
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

            // Thành công HTTP nhưng BE có thể trả về { success: false } trong body.
            // Parse JSON đúng 1 lần rồi dùng chung cho cả việc check success và unwrap data.
            T result = default;
            try
            {
                var json = string.IsNullOrWhiteSpace(rawBody) ? null : JToken.Parse(rawBody);
                var envelope = json as JObject;
                var successToken = envelope?.Property("success", StringComparison.OrdinalIgnoreCase)?.Value;
                bool isEnvelope = successToken != null && successToken.Type == JTokenType.Boolean;

                // BE trả về envelope với success: false → gọi onError
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
                // Parse thất bại → thường do DTO không khớp JSON response
                Debug.LogError($"[ApiClient] ❌ Parse Error | type={typeof(T).Name} | exception={ex.Message}");
                Debug.LogError($"[ApiClient] Raw body: {rawBody}");

                onError?.Invoke(new ApiException
                {
                    StatusCode = request.responseCode,
                    ErrorCode = "PARSE_ERROR",
                    Message = $"Failed to parse JSON into {typeof(T).Name}: {ex.Message}",
                    RawBody = rawBody
                });
                return; // Stop execution if parsing fails
            }

            // Report network success if we were reconnecting
            MysticJourney.Networking.NetworkReconnectManager.Instance?.ReportNetworkSuccess();

            // Gọi onSuccess BÊN NGOÀI try-catch để lỗi của Callback không bị nhầm thành lỗi Parse JSON
            onSuccess?.Invoke(result);
        }

        private static string ReadString(JObject obj, string name)
        {
            var value = obj?.Property(name, StringComparison.OrdinalIgnoreCase)?.Value;
            return value == null || value.Type == JTokenType.Null ? null : value.ToString();
        }

        // Unwrap envelope ApiResponse<T> { success, message, errorCode, data }
        // Trả về .data nếu là envelope, ngược lại parse trực tiếp từ JSON đã parse sẵn
        private static T UnwrapEnvelope<T>(JToken json, JObject envelope, bool isEnvelope)
        {
            if (json == null)
                return default;

            var targetType = typeof(T);

            // Nếu T là ApiResponse<> hoặc SimpleResponse → map trực tiếp cả envelope
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
