using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class SkinApi : BaseApiService<SkinApi>
    {
        public void EquipSkin(int playerSkinId, bool isEquipped, Action<ApiResponse<PlayerSkinResponse>> onSuccess, Action<ApiException> onError)
        {
            var body = new EquipSkinRequest { PlayerSkinId = playerSkinId, IsEquipped = isEquipped };
            SafeDebugLog($"EquipSkin -> playerSkinId={playerSkinId} isEquipped={isEquipped}");
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
            SafeDebugLog($"UnequipSkin -> playerSkinId={playerSkinId}");
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
