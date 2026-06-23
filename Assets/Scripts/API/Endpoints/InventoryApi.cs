using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class InventoryApi : BaseApiService<InventoryApi>
    {
        public void GetInventory(Action<ApiResponse<InventorySummaryResponse>> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetInventory → GET /api/inventory/me");
            ApiClient.Instance.Get<ApiResponse<InventorySummaryResponse>>(
                ApiConfig.InventoryMe,
                response =>
                {
                    SafeDebugLog($"GetInventory OK | TotalItems={response.Data?.TotalItems} | BagItems={response.Data?.BagItems?.Length}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetInventory FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void EquipItem(int inventoryItemId, Action<ApiResponse<InventoryActionResultResponse>> onSuccess, Action<ApiException> onError)
        {
            var body = new EquipItemRequest { InventoryItemId = inventoryItemId };
            SafeDebugLog($"EquipItem → POST /api/inventory/equip-item | inventoryItemId={inventoryItemId}");
            ApiClient.Instance.Post<EquipItemRequest, ApiResponse<InventoryActionResultResponse>>(
                ApiConfig.InventoryEquip, body,
                response =>
                {
                    SafeDebugLog($"EquipItem OK | ItemName={response.Data?.Item?.ItemName} | IsEquipped={response.Data?.Item?.IsEquipped}");
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
            var body = new UnequipItemRequest { InventoryItemId = inventoryItemId };
            SafeDebugLog($"UnequipItem → POST /api/inventory/unequip-item | inventoryItemId={inventoryItemId}");
            ApiClient.Instance.Post<UnequipItemRequest, ApiResponse<InventoryActionResultResponse>>(
                ApiConfig.InventoryUnequip, body,
                response =>
                {
                    SafeDebugLog($"UnequipItem OK | ItemName={response.Data?.Item?.ItemName} | IsEquipped={response.Data?.Item?.IsEquipped}");
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
            var body = new ConsumeItemRequest { InventoryItemId = inventoryItemId, Quantity = quantity };
            SafeDebugLog($"ConsumeItem → POST /api/inventory/consume-item | inventoryItemId={inventoryItemId} | qty={quantity}");
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

        public void EquipSkin(int playerSkinId, Action<ApiResponse<PlayerSkinResponse>> onSuccess, Action<ApiException> onError)
        {
            var body = new EquipSkinRequest { PlayerSkinId = playerSkinId, IsEquipped = true };
            SafeDebugLog($"EquipSkin → POST /api/skins/equip | playerSkinId={playerSkinId}");
            ApiClient.Instance.Post<EquipSkinRequest, ApiResponse<PlayerSkinResponse>>(
                ApiConfig.SkinEquip, body,
                response =>
                {
                    SafeDebugLog($"EquipSkin OK | SkinName={response.Data?.SkinName} | IsEquipped={response.Data?.IsEquipped}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"EquipSkin FAIL | playerSkinId={playerSkinId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void UnequipSkin(int playerSkinId, Action<ApiResponse<object>> onSuccess, Action<ApiException> onError)
        {
            var body = new UnequipSkinRequest { PlayerSkinId = playerSkinId };
            SafeDebugLog($"UnequipSkin → POST /api/skins/unequip | playerSkinId={playerSkinId}");
            ApiClient.Instance.Post<UnequipSkinRequest, ApiResponse<object>>(
                ApiConfig.SkinUnequip, body,
                response =>
                {
                    SafeDebugLog($"UnequipSkin OK | message={response.Message}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UnequipSkin FAIL | playerSkinId={playerSkinId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
