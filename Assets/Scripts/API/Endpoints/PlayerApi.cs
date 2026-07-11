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

        // ── Lấy inventory ────────────────────────────────────────────────
        public void GetMyInventory(Action<InventorySummaryResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMyInventory...");
            ApiClient.Instance.Get<InventorySummaryResponse>(
                ApiConfig.InventoryMe,
                response =>
                {
                    SafeDebugLog($"GetMyInventory OK | TotalItems={response.TotalItems} | TotalSkins={response.TotalSkins} | BagCapacity={response.BagCapacity}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMyInventory FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Trang bị item ───────────────────────────────────────────────
        public void EquipItem(int inventoryItemId, Action<InventoryActionResultResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"EquipItem → inventoryItemId={inventoryItemId}");
            var body = new EquipItemRequest { InventoryItemId = inventoryItemId };
            ApiClient.Instance.Post<EquipItemRequest, InventoryActionResultResponse>(
                ApiConfig.InventoryEquip, body,
                response =>
                {
                    SafeDebugLog($"EquipItem OK | ItemName={response.Item?.ItemName} | Slot={response.Item?.EquippedSlot}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"EquipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Gỡ trang bị item ───────────────────────────────────────────
        public void UnequipItem(int inventoryItemId, Action<InventoryActionResultResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"UnequipItem → inventoryItemId={inventoryItemId}");
            var body = new UnequipItemRequest { InventoryItemId = inventoryItemId };
            ApiClient.Instance.Post<UnequipItemRequest, InventoryActionResultResponse>(
                ApiConfig.InventoryUnequip, body,
                response =>
                {
                    SafeDebugLog($"UnequipItem OK | ItemName={response.Item?.ItemName}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UnequipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Tiêu thụ item ───────────────────────────────────────────────
        public void ConsumeItem(int inventoryItemId, int quantity, Action<SimpleResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"ConsumeItem → inventoryItemId={inventoryItemId} | quantity={quantity}");
            var body = new ConsumeItemRequest { InventoryItemId = inventoryItemId, Quantity = quantity };
            ApiClient.Instance.Post<ConsumeItemRequest, SimpleResponse>(
                ApiConfig.InventoryConsume, body,
                response =>
                {
                    SafeDebugLog($"ConsumeItem OK | message={response.message}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ConsumeItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
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

        // ── Lấy danh sách mail ──────────────────────────────────────────
        public void GetMyMails(Action<MailListPagedResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMyMails...");
            ApiClient.Instance.Get<MailListPagedResponse>(
                ApiConfig.MailMe,
                response =>
                {
                    SafeDebugLog($"GetMyMails OK | TotalMails={response.TotalMails}");
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
