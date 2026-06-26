using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class PlayerApi : BaseApiService<PlayerApi>
    {
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

        public void GetMyProfile(Action<PlayerProfileResponse> onSuccess, Action<ApiException> onError)
        {
            int profileId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);
            if (profileId <= 0)
            {
                SafeDebugError("GetMyProfile FAIL: Chua co PlayerProfileId – hay LoginGame() truoc.");
                onError?.Invoke(new ApiException { StatusCode = 0, ErrorCode = "NO_PROFILE_ID", Message = "PlayerProfileId not found. Please login first.", RawBody = "" });
                return;
            }
            GetProfileById(profileId, onSuccess, onError);
        }

        public void UpdateProfile(int profileId, UpdatePlayerProfileRequest body, Action<PlayerProfileResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"UpdateProfile → profileId={profileId} | DisplayName={body?.DisplayName}");
            string endpoint = string.Format(ApiConfig.PlayerProfileUpdate, profileId);
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

        public void GetMyInventory(Action<ApiResponse<InventorySummaryResponse>> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMyInventory...");
            ApiClient.Instance.Get<ApiResponse<InventorySummaryResponse>>(
                ApiConfig.InventoryMe,
                response =>
                {
                    if (response.Success && response.Data != null)
                        SafeDebugLog($"GetMyInventory OK | TotalItems={response.Data.TotalItems} | TotalSkins={response.Data.TotalSkins} | BagCapacity={response.Data.BagCapacity}");
                    else
                        Debug.LogWarning($"[PlayerApi] GetMyInventory: success={response.Success} | message={response.Message}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMyInventory FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void EquipItem(int inventoryItemId, Action<ApiResponse<InventoryActionResultResponse>> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"EquipItem → inventoryItemId={inventoryItemId}");
            var body = new EquipItemRequest { InventoryItemId = inventoryItemId };
            ApiClient.Instance.Post<EquipItemRequest, ApiResponse<InventoryActionResultResponse>>(
                ApiConfig.InventoryEquip, body,
                response =>
                {
                    SafeDebugLog($"EquipItem OK | ItemName={response.Data?.Item?.ItemName} | Slot={response.Data?.Item?.EquippedSlot}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"EquipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void UnequipItem(int inventoryItemId, Action<ApiResponse<InventoryActionResultResponse>> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"UnequipItem → inventoryItemId={inventoryItemId}");
            var body = new UnequipItemRequest { InventoryItemId = inventoryItemId };
            ApiClient.Instance.Post<UnequipItemRequest, ApiResponse<InventoryActionResultResponse>>(
                ApiConfig.InventoryUnequip, body,
                response =>
                {
                    SafeDebugLog($"UnequipItem OK | ItemName={response.Data?.Item?.ItemName}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UnequipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void ConsumeItem(int inventoryItemId, int quantity, Action<ApiResponse<object>> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"ConsumeItem → inventoryItemId={inventoryItemId} | quantity={quantity}");
            var body = new ConsumeItemRequest { InventoryItemId = inventoryItemId, Quantity = quantity };
            ApiClient.Instance.Post<ConsumeItemRequest, ApiResponse<object>>(
                ApiConfig.InventoryConsume, body,
                response =>
                {
                    SafeDebugLog($"ConsumeItem OK | message={response.Message}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ConsumeItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void GetFriends(Action<ApiResponse<PlayerProfileResponse[]>> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetFriends...");
            ApiClient.Instance.Get<ApiResponse<PlayerProfileResponse[]>>(
                "api/playerprofiles/me/friends",
                response =>
                {
                    SafeDebugLog($"GetFriends OK | Count={response.Data?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetFriends FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void GetMyMails(Action<PlayerMeMailsResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMyMails...");
            ApiClient.Instance.Get<PlayerMeMailsResponse>(
                ApiConfig.PlayerMeMails,
                response =>
                {
                    SafeDebugLog($"GetMyMails OK");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMyMails FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
