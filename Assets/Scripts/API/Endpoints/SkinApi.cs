using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // Tương ứng SkinsController → /api/skins
    // Tất cả endpoint đều cần auth (Authorize)
    public class SkinApi : MonoBehaviour
    {
        private static SkinApi _instance;

        public static SkinApi Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[SkinApi]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<SkinApi>();
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

        // POST /api/skins/equip  (cần auth)
        // Trang bị skin cho player; IsEquipped = true để equip, false để chuyển skin
        // Server trả ApiResponse<PlayerSkinResponse>
        public void EquipSkin(
            int playerSkinId,
            bool isEquipped,
            Action<ApiResponse<PlayerSkinResponse>> onSuccess,
            Action<ApiException> onError)
        {
            var body = new EquipSkinRequest { PlayerSkinId = playerSkinId, IsEquipped = isEquipped };
            Debug.Log($"[SkinApi] EquipSkin → playerSkinId={playerSkinId} isEquipped={isEquipped}");

            ApiClient.Instance.Post<EquipSkinRequest, ApiResponse<PlayerSkinResponse>>(
                ApiConfig.SkinEquip,
                body,
                response =>
                {
                    Debug.Log($"[SkinApi] ✅ EquipSkin OK | SkinName={response.Data?.SkinName} | IsEquipped={response.Data?.IsEquipped}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[SkinApi] ❌ EquipSkin FAIL | playerSkinId={playerSkinId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }

        // POST /api/skins/unequip  (cần auth)
        // Tháo skin đang mặc; PlayerSkinId là ID trong bảng PlayerSkin
        public void UnequipSkin(
            int playerSkinId,
            Action<ApiResponse<object>> onSuccess,
            Action<ApiException> onError)
        {
            var body = new UnequipSkinRequest { PlayerSkinId = playerSkinId };
            Debug.Log($"[SkinApi] UnequipSkin → playerSkinId={playerSkinId}");

            ApiClient.Instance.Post<UnequipSkinRequest, ApiResponse<object>>(
                ApiConfig.SkinUnequip,
                body,
                response =>
                {
                    Debug.Log($"[SkinApi] ✅ UnequipSkin OK | message={response.Message}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    Debug.LogError($"[SkinApi] ❌ UnequipSkin FAIL | playerSkinId={playerSkinId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true
            );
        }
    }
}
