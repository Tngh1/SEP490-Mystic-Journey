using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class AuthApi : MonoBehaviour
    {
        private static AuthApi _instance;

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
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void LoginGame(
            string emailOrUsername,
            string password,
            Action<LoginGameResponse> onSuccess,
            Action<ApiException> onError)
        {
            Debug.Log($"[AuthApi] LoginGame -> emailOrUsername={emailOrUsername}");

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

                    Debug.Log($"[AuthApi] LoginGame OK | UserName={response.UserName} | AccountId={response.AccountId} | PlayerProfileId={response.PlayerProfileId}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[AuthApi] LoginGame FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false
            );
        }

        public void GetMe(Action<MeResponse> onSuccess, Action<ApiException> onError)
        {
            Debug.Log("[AuthApi] GetMe...");

            ApiClient.Instance.Get<MeResponse>(
                ApiConfig.Me,
                response =>
                {
                    SaveProfileSession(response.PlayerProfileId, response.Level, response.PlayerClass);
                    SaveWorldSession(response.LastMapName, response.PositionX, response.PositionY);
                    Debug.Log($"[AuthApi] GetMe OK | UserName={response.UserName} | Role={response.Role} | LastMap={response.LastMapName}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[AuthApi] GetMe FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        public void Logout(Action<SimpleResponse> onSuccess, Action<ApiException> onError)
        {
            Debug.Log("[AuthApi] Logout...");

            ApiClient.Instance.PostEmpty<SimpleResponse>(
                ApiConfig.Logout,
                response =>
                {
                    ApiClient.Instance.ClearToken();
                    Debug.Log("[AuthApi] Logout OK.");
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
            if (playerProfileId.HasValue)
            {
                PlayerPrefs.SetInt(ApiConfig.PlayerProfileIdKey, playerProfileId.Value);
                WorldState.PlayerProfileId = playerProfileId.Value;
            }

            var safeLevel = Mathf.Max(1, level);
            PlayerPrefs.SetInt(ApiConfig.PlayerLevelKey, safeLevel);
            WorldState.PlayerLevel = safeLevel;

            var safeClass = string.IsNullOrWhiteSpace(playerClass) ? "Knight" : playerClass.Trim();
            PlayerPrefs.SetString(ApiConfig.PlayerClassKey, safeClass);
            WorldState.PlayerClass = safeClass;
        }

        private static void SaveWorldSession(string mapName, double positionX, double positionY)
        {
            var safeMapName = string.IsNullOrWhiteSpace(mapName) ? "ElfForest" : mapName.Trim();
            var position = new Vector3((float)positionX, (float)positionY, 0f);

            PlayerPrefs.SetString(ApiConfig.LastMapNameKey, safeMapName);
            PlayerPrefs.SetFloat(ApiConfig.PositionXKey, position.x);
            PlayerPrefs.SetFloat(ApiConfig.PositionYKey, position.y);

            WorldState.CurrentMapName = safeMapName;
            WorldState.LastPosition = position;
        }
    }
}
