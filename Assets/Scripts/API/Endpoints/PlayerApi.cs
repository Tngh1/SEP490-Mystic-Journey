using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class PlayerApi : BaseApiService<PlayerApi>
    {
        // Executes get profile by id operation.
        public void GetProfileById(int profileId, Action<PlayerProfileResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"GetProfileById → profileId={profileId}");
            string endpoint = string.Format(ApiConfig.PlayerProfileById, profileId);
            ApiClient.Instance.Get<PlayerProfileResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetProfileById OK | DisplayName={response.DisplayName} | Level={response.Level} | Gold={response.Gold} | Gems={response.Gems}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetProfileById FAIL | profileId={profileId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Executes get my profile operation.
        public void GetMyProfile(Action<PlayerProfileResponse> onSuccess, Action<ApiException> onError)
        {
            int profileId = MysticJourney.Core.Services.GameStateService.Instance.PlayerProfileId;
            if (profileId <= 0)
            {
                profileId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);
            }

            if (profileId <= 0)
            {
                SafeDebugError("GetMyProfile FAIL: Chua co PlayerProfileId – hay LoginGame() truoc.");
                onError?.Invoke(new ApiException { StatusCode = 0, ErrorCode = "NO_PROFILE_ID", Message = "PlayerProfileId not found. Please login first.", RawBody = "" });
                return;
            }
            GetProfileById(profileId, onSuccess, onError);
        }

        // Executes update profile operation.
        public void UpdateProfile(int profileId, UpdatePlayerProfileRequest body, Action<PlayerProfileResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"UpdateProfile → profileId={profileId} | DisplayName={body?.DisplayName}");
            string endpoint = string.Format(ApiConfig.PlayerProfileById, profileId);
            ApiClient.Instance.Put<UpdatePlayerProfileRequest, PlayerProfileResponse>(
                endpoint, body,
                response =>
                {
                    SafeDebugLog($"UpdateProfile OK | DisplayName={response.DisplayName} | Level={response.Level}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UpdateProfile FAIL | profileId={profileId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Executes change name operation.
        public void ChangeName(ChangeNameRequestDto body, Action<PlayerProfileResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"ChangeName → NewName={body?.NewName}");
            ApiClient.Instance.Post<ChangeNameRequestDto, PlayerProfileResponse>(
                ApiConfig.PlayerProfileChangeName, body,
                response =>
                {
                    SafeDebugLog($"ChangeName OK | NewName={response.DisplayName}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ChangeName FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
