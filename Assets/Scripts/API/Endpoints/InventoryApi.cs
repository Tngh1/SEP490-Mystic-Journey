using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Tầng gọi InventoryController → /api/inventory
    // và SkinsController → /api/skins
    // Tất cả endpoint đều cần auth (Authorize)
    public class InventoryApi : MonoBehaviour
    {
        private static InventoryApi _instance;

        public static InventoryApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[InventoryApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<InventoryApi>();
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

        // ==================== UC 20.1 – View Inventory ====================
        // GET /api/inventory/me  (cần auth)
        // Server trả ApiResponse<InventorySummaryResponse>
        public void GetInventory(
            Action<ApiResponse<InventorySummaryResponse>> onSuccess,
            Action<ApiException> onError)
        {
            Debug.Log("[InventoryApi] GetInventory → GET /api/inventory/me");

            ApiClient.Instance.Get<ApiResponse<InventorySummaryResponse>>(
                ApiConfig.InventoryMe,
                response =>
                {
                    Debug.Log($"[InventoryApi] ✅ GetInventory OK | TotalItems={response.Data?.TotalItems} | BagItems={response.Data?.BagItems?.Length}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[InventoryApi] ❌ GetInventory FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // ==================== UC 20.4 – Equip Item ====================
        // POST /api/inventory/equip-item  (cần auth)
        // Server trả ApiResponse<InventoryActionResultResponse>
        public void EquipItem(
            int inventoryItemId,
            Action<ApiResponse<InventoryActionResultResponse>> onSuccess,
            Action<ApiException> onError)
        {
            var body = new EquipItemRequest { InventoryItemId = inventoryItemId };
            Debug.Log($"[InventoryApi] EquipItem → POST /api/inventory/equip-item | inventoryItemId={inventoryItemId}");

            ApiClient.Instance.Post<EquipItemRequest, ApiResponse<InventoryActionResultResponse>>(
                ApiConfig.InventoryEquip,
                body,
                response =>
                {
                    Debug.Log($"[InventoryApi] ✅ EquipItem OK | ItemName={response.Data?.Item?.ItemName} | IsEquipped={response.Data?.Item?.IsEquipped}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[InventoryApi] ❌ EquipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // ==================== UC 20.5 – Unequip Item ====================
        // POST /api/inventory/unequip-item  (cần auth)
        // Server trả ApiResponse<InventoryActionResultResponse>
        public void UnequipItem(
            int inventoryItemId,
            Action<ApiResponse<InventoryActionResultResponse>> onSuccess,
            Action<ApiException> onError)
        {
            var body = new UnequipItemRequest { InventoryItemId = inventoryItemId };
            Debug.Log($"[InventoryApi] UnequipItem → POST /api/inventory/unequip-item | inventoryItemId={inventoryItemId}");

            ApiClient.Instance.Post<UnequipItemRequest, ApiResponse<InventoryActionResultResponse>>(
                ApiConfig.InventoryUnequip,
                body,
                response =>
                {
                    Debug.Log($"[InventoryApi] ✅ UnequipItem OK | ItemName={response.Data?.Item?.ItemName} | IsEquipped={response.Data?.Item?.IsEquipped}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[InventoryApi] ❌ UnequipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // ==================== UC 20.3 – Consume Item ====================
        // POST /api/inventory/consume-item  (cần auth)
        // Server trả ApiResponse<object>
        public void ConsumeItem(
            int inventoryItemId,
            int quantity,
            Action<ApiResponse<object>> onSuccess,
            Action<ApiException> onError)
        {
            var body = new ConsumeItemRequest { InventoryItemId = inventoryItemId, Quantity = quantity };
            Debug.Log($"[InventoryApi] ConsumeItem → POST /api/inventory/consume-item | inventoryItemId={inventoryItemId} | qty={quantity}");

            ApiClient.Instance.Post<ConsumeItemRequest, ApiResponse<object>>(
                ApiConfig.InventoryConsume,
                body,
                response =>
                {
                    Debug.Log($"[InventoryApi] ✅ ConsumeItem OK | message={response.Message}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[InventoryApi] ❌ ConsumeItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // ==================== UC 20.6 – Equip Skin ====================
        // POST /api/skins/equip  (cần auth)
        // Server trả ApiResponse<PlayerSkinResponse>
        public void EquipSkin(
            int playerSkinId,
            Action<ApiResponse<PlayerSkinResponse>> onSuccess,
            Action<ApiException> onError)
        {
            var body = new EquipSkinRequest { PlayerSkinId = playerSkinId, IsEquipped = true };
            Debug.Log($"[InventoryApi] EquipSkin → POST /api/skins/equip | playerSkinId={playerSkinId}");

            ApiClient.Instance.Post<EquipSkinRequest, ApiResponse<PlayerSkinResponse>>(
                ApiConfig.SkinEquip,
                body,
                response =>
                {
                    Debug.Log($"[InventoryApi] ✅ EquipSkin OK | SkinName={response.Data?.SkinName} | IsEquipped={response.Data?.IsEquipped}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[InventoryApi] ❌ EquipSkin FAIL | playerSkinId={playerSkinId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // ==================== UC 20.7 – Unequip Skin ====================
        // POST /api/skins/unequip  (cần auth)
        // Server trả ApiResponse<object>
        public void UnequipSkin(
            int playerSkinId,
            Action<ApiResponse<object>> onSuccess,
            Action<ApiException> onError)
        {
            var body = new UnequipSkinRequest { PlayerSkinId = playerSkinId };
            Debug.Log($"[InventoryApi] UnequipSkin → POST /api/skins/unequip | playerSkinId={playerSkinId}");

            ApiClient.Instance.Post<UnequipSkinRequest, ApiResponse<object>>(
                ApiConfig.SkinUnequip,
                body,
                response =>
                {
                    Debug.Log($"[InventoryApi] ✅ UnequipSkin OK | message={response.Message}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[InventoryApi] ❌ UnequipSkin FAIL | playerSkinId={playerSkinId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }
    }
}
