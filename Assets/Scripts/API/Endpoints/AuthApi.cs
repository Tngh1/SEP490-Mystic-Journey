using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;
using MysticJourney.Core.Utilities;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class AuthApi : BaseApiService<AuthApi>
    {
        public void LoginGame(
            string emailOrUsername,
            string password,
            Action<LoginGameResponse> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog($"LoginGame -> emailOrUsername={emailOrUsername}");

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
                    ApiClient.Instance.SaveToken(response.AccessToken);

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

        public void GetMe(Action<MeResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMe...");

            ApiClient.Instance.Get<MeResponse>(
                ApiConfig.Me,
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

        public void Logout(Action<SimpleResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("Logout...");

            ApiClient.Instance.PostEmpty<SimpleResponse>(
                ApiConfig.Logout,
                response =>
                {
                    ApiClient.Instance.ClearToken();
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

        private static void SaveProfileSession(int? playerProfileId, int level, string playerClass)
        {
            var state = GameStateService.Instance;

            if (playerProfileId.HasValue)
            {
                PlayerPrefs.SetInt(ApiConfig.PlayerProfileIdKey, playerProfileId.Value);
                state.PlayerProfileId = playerProfileId.Value;
            }

            var safeLevel = Mathf.Max(1, level);
            PlayerPrefs.SetInt(ApiConfig.PlayerLevelKey, safeLevel);
            state.PlayerLevel = safeLevel;

            var safeClass = string.IsNullOrWhiteSpace(playerClass) ? GameConstants.PlayerClasses.Knight : playerClass.Trim();
            PlayerPrefs.SetString(ApiConfig.PlayerClassKey, safeClass);
            state.PlayerClass = safeClass;
        }

        private static void SaveWorldSession(string mapName, double positionX, double positionY)
        {
            var state = GameStateService.Instance;
            var safeMapName = string.IsNullOrWhiteSpace(mapName) ? GameConstants.WorldDefaults.DefaultMap : mapName.Trim();
            var position = new Vector3((float)positionX, (float)positionY, 0f);

            PlayerPrefs.SetString(ApiConfig.LastMapNameKey, safeMapName);
            PlayerPrefs.SetFloat(ApiConfig.PositionXKey, position.x);
            PlayerPrefs.SetFloat(ApiConfig.PositionYKey, position.y);

            state.CurrentMapName = safeMapName;
            state.LastPosition = position;
        }
    }
}
