using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Xử lý API liên quan Player Profile và Inventory.
    // Tất cả endpoint đều cần auth (JWT token).
    public class PlayerApi : MonoBehaviour
    {
        private static PlayerApi _instance;

        // Singleton – không cần attach vào GameObject thủ công
        public static PlayerApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[PlayerApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<PlayerApi>();
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

        // ── Player Profile ────────────────────────────────────────

        // GET /api/playerprofiles/{id}
        // Lấy thông tin profile theo ID cụ thể
        public void GetProfileById(int profileId, Action<PlayerProfileResponse> onSuccess, Action<ApiException> onError)
        {
            Debug.Log($"[PlayerApi] GetProfileById → profileId={profileId}");

            string endpoint = string.Format(ApiConfig.PlayerProfileById, profileId);
            ApiClient.Instance.Get<PlayerProfileResponse>(
                endpoint,
                response =>
                {
                    Debug.Log($"[PlayerApi] ✅ GetProfileById OK | DisplayName={response.DisplayName} | Level={response.Level} | Gold={response.Gold} | Gems={response.Gems}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[PlayerApi] ❌ GetProfileById FAIL | profileId={profileId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // Lấy profile của player đang đăng nhập (dùng PlayerProfileId đã lưu sau login)
        public void GetMyProfile(Action<PlayerProfileResponse> onSuccess, Action<ApiException> onError)
        {
            int profileId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);

            // Kiểm tra đã login và có ProfileId chưa
            if (profileId <= 0)
            {
                Debug.LogError("[PlayerApi] ❌ GetMyProfile FAIL: Chưa có PlayerProfileId – hãy LoginGame() trước.");
                onError?.Invoke(new ApiException
                {
                    StatusCode = 0,
                    ErrorCode = "NO_PROFILE_ID",
                    Message = "PlayerProfileId not found. Please login first.",
                    RawBody = ""
                });
                return;
            }

            GetProfileById(profileId, onSuccess, onError);
        }

        // PUT /api/playerprofiles/{id}  (cần auth)
        // Cập nhật thông tin profile: DisplayName, AvatarUrl, PlayerClass...
        public void UpdateProfile(
            int profileId,
            UpdatePlayerProfileRequest body,
            Action<PlayerProfileResponse> onSuccess,
            Action<ApiException> onError)
        {
            Debug.Log($"[PlayerApi] UpdateProfile → profileId={profileId} | DisplayName={body?.DisplayName}");

            string endpoint = string.Format(ApiConfig.PlayerProfileUpdate, profileId);
            ApiClient.Instance.Put<UpdatePlayerProfileRequest, PlayerProfileResponse>(
                endpoint,
                body,
                response =>
                {
                    Debug.Log($"[PlayerApi] ✅ UpdateProfile OK | DisplayName={response.DisplayName} | Level={response.Level}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[PlayerApi] ❌ UpdateProfile FAIL | profileId={profileId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // ── Inventory ─────────────────────────────────────────────

        // GET /api/inventory/me  (cần auth)
        // Trả về ApiResponse<InventorySummaryResponse> với EquippedItems và BagItems tách riêng
        public void GetMyInventory(
            Action<ApiResponse<InventorySummaryResponse>> onSuccess,
            Action<ApiException> onError)
        {
            Debug.Log("[PlayerApi] GetMyInventory...");

            ApiClient.Instance.Get<ApiResponse<InventorySummaryResponse>>(
                ApiConfig.InventoryMe,
                response =>
                {
                    if (response.Success && response.Data != null)
                        Debug.Log($"[PlayerApi] ✅ GetMyInventory OK | TotalItems={response.Data.TotalItems} | TotalSkins={response.Data.TotalSkins} | BagCapacity={response.Data.BagCapacity}");
                    else
                        Debug.LogWarning($"[PlayerApi] ⚠ GetMyInventory: success={response.Success} | message={response.Message}");

                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[PlayerApi] ❌ GetMyInventory FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // POST /api/inventory/equip-item  (cần auth)
        // Trang bị item vào slot tương ứng; server trả về item đã equip + PlayerStats mới
        public void EquipItem(
            int inventoryItemId,
            Action<ApiResponse<InventoryActionResultResponse>> onSuccess,
            Action<ApiException> onError)
        {
            Debug.Log($"[PlayerApi] EquipItem → inventoryItemId={inventoryItemId}");

            var body = new EquipItemRequest { InventoryItemId = inventoryItemId };
            ApiClient.Instance.Post<EquipItemRequest, ApiResponse<InventoryActionResultResponse>>(
                ApiConfig.InventoryEquip,
                body,
                response =>
                {
                    Debug.Log($"[PlayerApi] ✅ EquipItem OK | ItemName={response.Data?.Item?.ItemName} | Slot={response.Data?.Item?.EquippedSlot}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[PlayerApi] ❌ EquipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // POST /api/inventory/unequip-item  (cần auth)
        // Tháo item khỏi slot; server trả về item đã unequip + PlayerStats mới
        public void UnequipItem(
            int inventoryItemId,
            Action<ApiResponse<InventoryActionResultResponse>> onSuccess,
            Action<ApiException> onError)
        {
            Debug.Log($"[PlayerApi] UnequipItem → inventoryItemId={inventoryItemId}");

            var body = new UnequipItemRequest { InventoryItemId = inventoryItemId };
            ApiClient.Instance.Post<UnequipItemRequest, ApiResponse<InventoryActionResultResponse>>(
                ApiConfig.InventoryUnequip,
                body,
                response =>
                {
                    Debug.Log($"[PlayerApi] ✅ UnequipItem OK | ItemName={response.Data?.Item?.ItemName}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[PlayerApi] ❌ UnequipItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // POST /api/inventory/consume-item  (cần auth)
        // Sử dụng item tiêu hao (potion, scroll...); server trả về ApiResponse<object>
        public void ConsumeItem(
            int inventoryItemId,
            int quantity,
            Action<ApiResponse<object>> onSuccess,
            Action<ApiException> onError)
        {
            Debug.Log($"[PlayerApi] ConsumeItem → inventoryItemId={inventoryItemId} | quantity={quantity}");

            var body = new ConsumeItemRequest { InventoryItemId = inventoryItemId, Quantity = quantity };
            ApiClient.Instance.Post<ConsumeItemRequest, ApiResponse<object>>(
                ApiConfig.InventoryConsume,
                body,
                response =>
                {
                    Debug.Log($"[PlayerApi] ✅ ConsumeItem OK | message={response.Message}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[PlayerApi] ❌ ConsumeItem FAIL | inventoryItemId={inventoryItemId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }
    }
}
