using System;
using System.Collections;
using System.Text;
using MysticJourney.API.Models.Response;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace MysticJourney.API.Core
{
    // Singleton MonoBehaviour – trái tim của hệ thống API.
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

        // Lưu JWT access token vào PlayerPrefs sau khi login thành công
        public void SaveToken(string token)
        {
            PlayerPrefs.SetString(ApiConfig.AccessTokenKey, token);
            PlayerPrefs.Save();
            Debug.Log("[ApiClient] Token saved.");
        }

        // Lấy token hiện tại từ PlayerPrefs (trống nếu chưa login)
        public string GetToken()
        {
            return PlayerPrefs.GetString(ApiConfig.AccessTokenKey, string.Empty);
        }

        // Xóa token và toàn bộ dữ liệu phiên khi logout
        public void ClearToken()
        {
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

        // BE unified ApiResponse<T> envelope { success, message, errorCode, data }
        // Tự động unwrap .data nếu body là envelope, ngược lại trả raw.
        private static T UnwrapEnvelope<T>(string rawBody)
        {
            var targetType = typeof(T);
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(ApiResponse<>))
                return JsonConvert.DeserializeObject<T>(rawBody);

            try
            {
                var envelope = JsonConvert.DeserializeObject<ApiResponse<object>>(rawBody);
                if (envelope != null && envelope.Success && envelope.Data != null)
                {
                    // Re-serialize Data rồi deserialize sang T để unwrap
                    string dataJson = JsonConvert.SerializeObject(envelope.Data);
                    return JsonConvert.DeserializeObject<T>(dataJson);
                }
            }
            catch
            {
                // Body không phải envelope → fallback parse raw
            }
            return JsonConvert.DeserializeObject<T>(rawBody);
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

            // Thành công → parse JSON thành kiểu T (auto unwrap ApiResponse envelope)
            try
            {
                T result = UnwrapEnvelope<T>(rawBody);
                Debug.Log($"[ApiClient] ✅ {request.responseCode} OK | type={typeof(T).Name}");
                onSuccess?.Invoke(result);
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
            }
        }
    }
}
