using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER API - Quản lý player profile, inventory, bạn bè, mail
    // ═══════════════════════════════════════════════════════════════════════
    public class PlayerApi : BaseApiService<PlayerApi>
    {
        // ── Lấy profile theo ID ────────────────────────────────────────────
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

        // ── Lấy profile của mình ──────────────────────────────────────────
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

        // ── Cập nhật profile ──────────────────────────────────────────────
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

        // ── Đổi tên ──────────────────────────────────────────────
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

        // ── Lấy danh sách bạn bè ────────────────────────────────────────
        public void GetFriends(Action<PlayerProfileResponse[]> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetFriends...");
            ApiClient.Instance.Get<PlayerProfileResponse[]>(
                ApiConfig.PlayerProfileMeFriends,
                response =>
                {
                    SafeDebugLog($"GetFriends OK | Count={response?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetFriends FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Gửi Heartbeat ───────────────────────────────────────────────
        public void SendHeartbeat(Action<SimpleResponse> onSuccess = null, Action<ApiException> onError = null)
        {
            ApiClient.Instance.PostEmpty<SimpleResponse>(
                ApiConfig.PlayerHeartbeat,
                response =>
                {
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"SendHeartbeat FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
