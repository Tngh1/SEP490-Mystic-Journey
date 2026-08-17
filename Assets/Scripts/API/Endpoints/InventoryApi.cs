using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class InventoryApi : BaseApiService<InventoryApi>
    {

        // Executes get inventory operation.
        public void GetInventory(Action<InventorySummaryResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetInventory → GET /api/inventory/me");
            ApiClient.Instance.Get<InventorySummaryResponse>(
                ApiConfig.InventoryMe,
                response =>
                {
                    SafeDebugLog($"GetInventory OK | TotalItems={response.TotalItems} | BagItems={response.BagItems?.Length}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetInventory FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Equips an inventory item to the appropriate equipment slot and updates player combat stats.
        public void EquipItem(int inventoryItemId, Action<InventoryActionResultResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new EquipItemRequest { InventoryItemId = inventoryItemId };
            SafeDebugLog($"EquipItem → POST /api/inventory/equip-item | inventoryItemId={inventoryItemId}");
            ApiClient.Instance.Post<EquipItemRequest, InventoryActionResultResponse>(
                ApiConfig.InventoryEquip, body,
                response =>
                {
                    SafeDebugLog($"EquipItem OK | ItemName={response.Item?.ItemName} | IsEquipped={response.Item?.IsEquipped}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"EquipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Unequips an equipped item back into player inventory and recalculates player combat stats.
        public void UnequipItem(int inventoryItemId, Action<InventoryActionResultResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new UnequipItemRequest { InventoryItemId = inventoryItemId };
            SafeDebugLog($"UnequipItem → POST /api/inventory/unequip-item | inventoryItemId={inventoryItemId}");
            ApiClient.Instance.Post<UnequipItemRequest, InventoryActionResultResponse>(
                ApiConfig.InventoryUnequip, body,
                response =>
                {
                    SafeDebugLog($"UnequipItem OK | ItemName={response.Item?.ItemName} | IsEquipped={response.Item?.IsEquipped}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UnequipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Executes consume item operation.
        public void ConsumeItem(int inventoryItemId, int quantity, Action<ConsumeItemResultResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new ConsumeItemRequest { InventoryItemId = inventoryItemId, Quantity = quantity };
            SafeDebugLog($"ConsumeItem → POST /api/inventory/consume-item | inventoryItemId={inventoryItemId} | qty={quantity}");
            ApiClient.Instance.Post<ConsumeItemRequest, ConsumeItemResultResponse>(
                ApiConfig.InventoryConsume, body,
                response =>
                {
                    SafeDebugLog($"ConsumeItem OK | effect={response.EffectType} | value={response.EffectValue}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ConsumeItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Executes equip skin operation.
        public void EquipSkin(int playerSkinId, Action<PlayerSkinResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new EquipSkinRequest { PlayerSkinId = playerSkinId, IsEquipped = true };
            SafeDebugLog($"EquipSkin → POST /api/skins/equip | playerSkinId={playerSkinId}");
            ApiClient.Instance.Post<EquipSkinRequest, PlayerSkinResponse>(
                ApiConfig.SkinEquip, body,
                response =>
                {
                    SafeDebugLog($"EquipSkin OK | SkinName={response.SkinName} | IsEquipped={response.IsEquipped}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"EquipSkin FAIL | playerSkinId={playerSkinId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Executes unequip skin operation.
        public void UnequipSkin(int playerSkinId, Action<SimpleResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new UnequipSkinRequest { PlayerSkinId = playerSkinId };
            SafeDebugLog($"UnequipSkin → POST /api/skins/unequip | playerSkinId={playerSkinId}");
            ApiClient.Instance.Post<UnequipSkinRequest, SimpleResponse>(
                ApiConfig.SkinUnequip, body,
                response =>
                {
                    SafeDebugLog($"UnequipSkin OK | message={response.message}");
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
