using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════════════
    // AUTH API - Xác thực
    // ═══════════════════════════════════════════════════════════════════════
    public class AuthApi : BaseApiService<AuthApi>
    {
        // ═══════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════

        // ── Đăng nhập ──────────────────────────────
        public void LoginGame(
            string emailOrUsername,
            string password,
            Action<LoginGameResponse> onSuccess,
            Action<ApiException> onError)
        {
            // Một lần tắt game cưỡng bức không chạy logout nên token/socket cũ vẫn có thể
            // được phục hồi ở màn hình login. Dọn phiên local cũ trước khi BE tạo session mới
            // để callback SessionOverridden của socket cũ không thể xóa token mới.
            SessionService.PrepareForCredentialLogin();

            SafeDebugLog($"LoginGame -> emailOrUsername={emailOrUsername}");

            var body = new LoginGameRequest
            {
                EmailOrUsername = emailOrUsername,
                Password = password,
                ClientType = "Game"
            };

            ApiClient.Instance.Post<LoginGameRequest, LoginGameResponse>(
                ApiConfig.AuthLogin,
                body,
                response =>
                {
                    ApiClient.Instance.SaveToken(response.AccessToken);

                    // Lưu refresh token để tự động làm mới access token khi hết hạn
                    if (!string.IsNullOrEmpty(response.RefreshToken))
                        ApiClient.Instance.SaveRefreshToken(response.RefreshToken);

                    if (response.PlayerProfileId.HasValue)
                        PlayerPrefs.SetInt(ApiConfig.PlayerProfileIdKey, response.PlayerProfileId.Value);
                    else
                        Debug.LogWarning("[AuthApi] LoginGame: PlayerProfileId is null.");

                    PlayerPrefs.SetInt(ApiConfig.AccountIdKey, response.AccountId);
                    PlayerPrefs.SetString(ApiConfig.UserNameKey, response.UserName);

                    SaveProfileSession(response.PlayerProfileId, response.Level, response.PlayerClass);
                    SaveWorldSession(response.LastMapName, response.PositionX, response.PositionY);
                    PlayerPrefs.Save();

                    SafeDebugLog($"LoginGame OK | UserName={response.UserName} | AccountId={response.AccountId} | PlayerProfileId={response.PlayerProfileId}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"LoginGame FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        // ── Lấy thông tin người dùng ─────────────
        public void GetMe(Action<MeResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMe...");

            ApiClient.Instance.Get<MeResponse>(
                ApiConfig.AuthMe,
                response =>
                {
                    SaveProfileSession(response.PlayerProfileId, response.Level, response.PlayerClass);
                    SaveWorldSession(response.LastMapName, response.PositionX, response.PositionY);
                    SafeDebugLog($"GetMe OK | UserName={response.UserName} | Role={response.Role} | LastMap={response.LastMapName}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMe FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // ── Đăng xuất ─────────────────────────────
        public void Logout(Action<SimpleResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("Logout...");

            ApiClient.Instance.PostEmpty<SimpleResponse>(
                ApiConfig.AuthLogout,
                response =>
                {
                    ApiClient.Instance.ClearToken();
                    if (GameStateService.Instance != null)
                        GameStateService.Instance.Reset();
                    SafeDebugLog("Logout OK.");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    ApiClient.Instance.ClearToken();
                    Debug.LogWarning($"[AuthApi] Logout server error, local session cleared | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // ── Private: Lưu session profile ─────────
        private static void SaveProfileSession(int? playerProfileId, int level, string playerClass)
        {
            if (GameStateService.Instance == null)
            {
                Debug.LogWarning("[AuthApi] GameStateService.Instance is null, skipping session save.");
                return;
            }

            var state = GameStateService.Instance;

            if (playerProfileId.HasValue)
            {
                PlayerPrefs.SetInt(ApiConfig.PlayerProfileIdKey, playerProfileId.Value);
                state.PlayerProfileId = playerProfileId.Value;
            }

            var safeLevel = Mathf.Max(1, level);
            PlayerPrefs.SetInt(ApiConfig.PlayerLevelKey, safeLevel);
            state.PlayerLevel = safeLevel;

            if (string.IsNullOrWhiteSpace(playerClass))
            {
                PlayerPrefs.DeleteKey(ApiConfig.PlayerClassKey);
                state.PlayerClass = string.Empty;
            }
            else
            {
                var safeClass = playerClass.Trim();
                PlayerPrefs.SetString(ApiConfig.PlayerClassKey, safeClass);
                state.PlayerClass = safeClass;
            }
        }

        // ── Private: Lưu session world ───────────
        private static void SaveWorldSession(string mapName, double positionX, double positionY)
        {
            if (GameStateService.Instance == null)
            {
                Debug.LogWarning("[AuthApi] GameStateService.Instance is null, skipping world session save.");
                return;
            }

            var state = GameStateService.Instance;
            var defaultMap = "Map001";
            var safeMapName = string.IsNullOrWhiteSpace(mapName) ? defaultMap : mapName.Trim();
            var position = new Vector3((float)positionX, (float)positionY, 0f);

            PlayerPrefs.SetString(ApiConfig.LastMapNameKey, safeMapName);
            PlayerPrefs.SetFloat(ApiConfig.PositionXKey, position.x);
            PlayerPrefs.SetFloat(ApiConfig.PositionYKey, position.y);

            state.CurrentMapName = safeMapName;
            state.LastPosition = position;
        }
    }
}
