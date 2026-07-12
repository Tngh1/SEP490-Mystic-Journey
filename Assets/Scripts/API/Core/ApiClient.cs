using System;
using System.Collections;
using System.Text;
using MysticJourney.API.Models.Response;
using Newtonsoft.Json;
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

            DontDestroyOnLoad(go);
        }

        // ── Token Management ──────────────────────────────────────

        private string _cachedToken = null;

        // Lưu JWT access token vào PlayerPrefs sau khi login thành công
        public void SaveToken(string token)
        {
            _cachedToken = token;
            PlayerPrefs.SetString(ApiConfig.AccessTokenKey, token);
            PlayerPrefs.Save();
            Debug.Log("[ApiClient] Token saved.");
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
            PlayerPrefs.DeleteKey(ApiConfig.AccessTokenKey);
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

        // Gửi GET request và parse response thành kiểu T
        public void Get<T>(string endpoint, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth = false)
        {
            StartCoroutine(GetCoroutine(endpoint, onSuccess, onError, requiresAuth));
        }

        private IEnumerator GetCoroutine<T>(string endpoint, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            string url = ApiConfig.BaseUrl + endpoint;
            Debug.Log($"[ApiClient] GET {url}");

            using (var request = UnityWebRequest.Get(url))
            {
                SetCommonHeaders(request, requiresAuth);
                request.timeout = ApiConfig.Timeout;
                yield return request.SendWebRequest();
                HandleResponse(request, onSuccess, onError);
            }
        }

        // Gửi POST request với JSON body và parse response thành kiểu T
        public void Post<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth = false)
        {
            StartCoroutine(PostCoroutine(endpoint, body, onSuccess, onError, requiresAuth));
        }

        // Gửi POST không có body (dùng cho logout, mark-as-read, claim reward...)
        public void PostEmpty<TResponse>(string endpoint, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth = false)
        {
            StartCoroutine(PostCoroutine<object, TResponse>(endpoint, null, onSuccess, onError, requiresAuth));
        }

        private IEnumerator PostCoroutine<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            string url = ApiConfig.BaseUrl + endpoint;
            string jsonBody = body != null ? JsonConvert.SerializeObject(body) : "{}";
            Debug.Log($"[ApiClient] POST {url}  body={jsonBody}");

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = ApiConfig.Timeout;
                SetCommonHeaders(request, requiresAuth);
                yield return request.SendWebRequest();
                HandleResponse<TResponse>(request, onSuccess, onError);
            }
        }

        // Gửi PUT request với JSON body và parse response thành kiểu T
        public void Put<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            StartCoroutine(PutCoroutine(endpoint, body, onSuccess, onError, requiresAuth));
        }

        private IEnumerator PutCoroutine<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            string url = ApiConfig.BaseUrl + endpoint;
            string jsonBody = body != null ? JsonConvert.SerializeObject(body) : "{}";
            Debug.Log($"[ApiClient] PUT {url}  body={jsonBody}");

            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            using (var request = new UnityWebRequest(url, "PUT"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = ApiConfig.Timeout;
                SetCommonHeaders(request, requiresAuth);
                yield return request.SendWebRequest();
                HandleResponse<TResponse>(request, onSuccess, onError);
            }
        }

        // Gửi DELETE request và parse response thành kiểu T
        public void Delete<T>(string endpoint, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth = true)
        {
            StartCoroutine(DeleteCoroutine(endpoint, onSuccess, onError, requiresAuth));
        }

        private IEnumerator DeleteCoroutine<T>(string endpoint, Action<T> onSuccess, Action<ApiException> onError, bool requiresAuth)
        {
            string url = ApiConfig.BaseUrl + endpoint;
            Debug.Log($"[ApiClient] DELETE {url}");

            using (var request = UnityWebRequest.Delete(url))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = ApiConfig.Timeout;
                SetCommonHeaders(request, requiresAuth);
                yield return request.SendWebRequest();
                HandleResponse<T>(request, onSuccess, onError);
            }
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
        private void HandleResponse<T>(UnityWebRequest request, Action<T> onSuccess, Action<ApiException> onError)
        {
            string rawBody = request.downloadHandler?.text ?? string.Empty;

            // Lỗi mạng / không kết nối được server
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogError($"[ApiClient] ❌ Network Error: {request.error}");
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

                Debug.LogError($"[ApiClient] ❌ HTTP {request.responseCode} | ErrorCode={errorCode} | Message={errorMsg}");
                Debug.LogError($"[ApiClient] Raw body: {rawBody}");

                onError?.Invoke(new ApiException
                {
                    StatusCode = request.responseCode,
                    ErrorCode = errorCode,
                    Message = errorMsg,
                    RawBody = rawBody
                });
                return;
            }

            // Thành công HTTP nhưng BE có thể trả về { success: false } trong body
            // Parse envelope để kiểm tra success
            try
            {
                var envelope = JsonConvert.DeserializeObject<ApiResponse<object>>(rawBody);
                
                // Nếu BE trả về envelope với success: false → gọi onError
                // Chỉ check khi thực sự có trường success trong JSON (để tránh lỗi với các response dạng {"message":"ok"})
                if (envelope != null && !envelope.Success && (rawBody.Contains("\"success\"") || rawBody.Contains("\"Success\"")))
                {
                    Debug.LogWarning($"[ApiClient] ⚠️ BE returned success=false | ErrorCode={envelope.ErrorCode} | Message={envelope.Message}");
                    
                    onError?.Invoke(new ApiException
                    {
                        StatusCode = request.responseCode,
                        ErrorCode = envelope.ErrorCode ?? "BUSINESS_ERROR",
                        Message = envelope.Message ?? "Request failed",
                        RawBody = rawBody
                    });
                    return;
                }
            }
            catch
            {
                // Body không phải envelope hợp lệ, tiếp tục parse bình thường
            }

            // Parse thành công → deserialize JSON thành kiểu T
            T result = default;
            try
            {
                result = UnwrapEnvelope<T>(rawBody);
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
                    Message = $"Không thể parse JSON sang {typeof(T).Name}: {ex.Message}",
                    RawBody = rawBody
                });
                return; // Stop execution if parsing fails
            }

            // Gọi onSuccess BÊN NGOÀI try-catch để lỗi của Callback không bị nhầm thành lỗi Parse JSON
            onSuccess?.Invoke(result);
        }

        // Unwrap envelope ApiResponse<T> { success, message, errorCode, data }
        // Trả về .Data nếu là envelope, ngược lại parse trực tiếp
        private static T UnwrapEnvelope<T>(string rawBody)
        {
            var targetType = typeof(T);
            
            // Nếu T là ApiResponse<> → parse trực tiếp
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(ApiResponse<>))
            {
                Debug.Log($"[ApiClient] RAW JSON for {targetType.Name}: {rawBody}");
                return JsonConvert.DeserializeObject<T>(rawBody);
            }

            // Thử parse envelope trước
            try
            {
                var envelope = JsonConvert.DeserializeObject<ApiResponse<object>>(rawBody);
                if (envelope != null && envelope.Success && envelope.Data != null)
                {
                    // Re-serialize Data rồi deserialize sang T để unwrap
                    string dataJson = JsonConvert.SerializeObject(envelope.Data);
                    Debug.Log($"[ApiClient] UNWRAPPED JSON for {targetType.Name}: {dataJson}");
                    return JsonConvert.DeserializeObject<T>(dataJson);
                }
                else
                {
                    Debug.LogWarning($"[ApiClient] ENVELOPE FAILED OR DATA NULL. Raw: {rawBody}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ApiClient] Envelope parse error: {ex.Message}");
            }
            
            Debug.Log($"[ApiClient] FALLBACK JSON for {targetType.Name}: {rawBody}");
            // Parse trực tiếp nếu không có envelope
            return JsonConvert.DeserializeObject<T>(rawBody);
        }
    }
}
