using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════════════
    // SKIN API - Áo
    // ═══════════════════════════════════════════════════════════════════════
    public class SkinApi : BaseApiService<SkinApi>
    {
        // ═══════════════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════════════

        // ── Trang bị skin ───────────────────────
        public void EquipSkin(int playerSkinId, bool isEquipped, Action<PlayerSkinResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new EquipSkinRequest { PlayerSkinId = playerSkinId, IsEquipped = isEquipped };
            SafeDebugLog($"EquipSkin -> playerSkinId={playerSkinId} isEquipped={isEquipped}");
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

        // ── Gỡ trang bị skin ───────────────────
        public void UnequipSkin(int playerSkinId, Action<SimpleResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new UnequipSkinRequest { PlayerSkinId = playerSkinId };
            SafeDebugLog($"UnequipSkin -> playerSkinId={playerSkinId}");
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
