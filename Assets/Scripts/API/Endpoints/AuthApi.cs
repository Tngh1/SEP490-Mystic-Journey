using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Xử lý toàn bộ API xác thực tài khoản.
    // Chỉ có 3 endpoint: LoginGame, GetMe, Logout.
    // Token được tự động lưu/xóa trong PlayerPrefs.
    public class AuthApi : MonoBehaviour
    {
        private static AuthApi _instance;

        // Singleton – không cần attach vào GameObject thủ công
        public static AuthApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AuthApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AuthApi>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── LoginGame ─────────────────────────────────────────────
        // POST /api/accounts/login-game
        // Dùng EmailOrUsername (có thể nhập email hoặc username đều được).
        // Sau khi thành công, token và PlayerProfileId được lưu tự động.
        public void LoginGame(
            string emailOrUsername,
            string password,
            Action<LoginGameResponse> onSuccess,
            Action<ApiException> onError)
        {
            Debug.Log($"[AuthApi] LoginGame → emailOrUsername={emailOrUsername}");

            var body = new LoginGameRequest
            {
                EmailOrUsername = emailOrUsername,
                Password = password
            };

            ApiClient.Instance.Post<LoginGameRequest, LoginGameResponse>(
                ApiConfig.LoginGame,
                body,
                response =>
                {
                    // Lưu token để các API cần auth dùng sau
                    ApiClient.Instance.SaveToken(response.AccessToken);

                    // Lưu PlayerProfileId (nullable vì tài khoản mới chưa có profile)
                    if (response.PlayerProfileId.HasValue)
                        PlayerPrefs.SetInt(ApiConfig.PlayerProfileIdKey, response.PlayerProfileId.Value);
                    else
                        Debug.LogWarning("[AuthApi] LoginGame: PlayerProfileId is null – profile chưa tồn tại.");

                    PlayerPrefs.SetInt(ApiConfig.AccountIdKey, response.AccountId);
                    PlayerPrefs.SetString(ApiConfig.UserNameKey, response.UserName);
                    PlayerPrefs.Save();

                    Debug.Log($"[AuthApi] ✅ LoginGame OK | UserName={response.UserName} | AccountId={response.AccountId} | PlayerProfileId={response.PlayerProfileId}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[AuthApi] ❌ LoginGame FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        // ── GetMe ─────────────────────────────────────────────────
        // GET /api/accounts/me  (cần auth)
        // Lấy thông tin tài khoản đang đăng nhập: role, email, vị trí cuối.
        public void GetMe(Action<MeResponse> onSuccess, Action<ApiException> onError)
        {
            Debug.Log("[AuthApi] GetMe...");

            ApiClient.Instance.Get<MeResponse>(
                ApiConfig.Me,
                response =>
                {
                    Debug.Log($"[AuthApi] ✅ GetMe OK | UserName={response.UserName} | Role={response.Role} | LastMap={response.LastMapName}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[AuthApi] ❌ GetMe FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // ── Logout ────────────────────────────────────────────────
        // POST /api/accounts/logout  (cần auth)
        // Token local bị xóa dù server có lỗi hay không.
        public void Logout(Action<SimpleResponse> onSuccess, Action<ApiException> onError)
        {
            Debug.Log("[AuthApi] Logout...");

            ApiClient.Instance.PostEmpty<SimpleResponse>(
                ApiConfig.Logout,
                response =>
                {
                    ApiClient.Instance.ClearToken();
                    Debug.Log("[AuthApi] ✅ Logout OK – token đã xóa.");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    // Dù server lỗi vẫn xóa token để người dùng không bị kẹt
                    ApiClient.Instance.ClearToken();
                    Debug.LogWarning($"[AuthApi] ⚠ Logout server lỗi nhưng token local đã xóa | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }
    }
}
