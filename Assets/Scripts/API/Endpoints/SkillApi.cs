using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════
    // SKILL API - Kỹ năng
    // ═══════════════════════════════════════════════════════════════
    public class SkillApi : BaseApiService<SkillApi>
    {
        // ═══════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════

        // ── Lấy skills của player ────────────────────────
        public void GetMySkills(Action<PlayerMeSkillsResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMySkills...");
            ApiClient.Instance.Get<PlayerMeSkillsResponse>(
                ApiConfig.PlayerSkillsMe,
                response =>
                {
                    SafeDebugLog($"GetMySkills OK | Count={response.Skills?.Count}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMySkills FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Nâng cấp skill ──────────────────────────────
        public void UpgradePlayerSkill(int playerSkillId, Action<PlayerSkillResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"UpgradePlayerSkill → playerSkillId={playerSkillId}");
            var body = new UpgradePlayerSkillRequest { PlayerSkillId = playerSkillId };
            ApiClient.Instance.Post<UpgradePlayerSkillRequest, PlayerSkillResponse>(
                ApiConfig.PlayerSkillsUpgrade,
                body,
                response =>
                {
                    SafeDebugLog($"UpgradePlayerSkill OK | playerSkillId={response.PlayerSkillId} | Level={response.Level}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UpgradePlayerSkill FAIL | playerSkillId={playerSkillId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Trang bị skill ──────────────────────────────
        public void EquipPlayerSkill(int playerSkillId, bool isEquipped, int? slotIndex, Action<PlayerSkillResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"EquipPlayerSkill → playerSkillId={playerSkillId}, isEquipped={isEquipped}, slotIndex={slotIndex}");
            var body = new EquipSkillRequest { PlayerSkillId = playerSkillId, IsEquipped = isEquipped, SlotIndex = slotIndex };
            ApiClient.Instance.Post<EquipSkillRequest, PlayerSkillResponse>(
                ApiConfig.PlayerSkillsEquip,
                body,
                response =>
                {
                    SafeDebugLog($"EquipPlayerSkill OK | SkillName={response.SkillName}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"EquipPlayerSkill FAIL | {error.StatusCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── Phế hủy skill ──────────────────────────────
        public void DismantlePlayerSkill(int playerSkillId, int? targetPlayerSkillId, Action<PlayerSkillResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"DismantlePlayerSkill → playerSkillId={playerSkillId}, targetPlayerSkillId={targetPlayerSkillId}");
            var body = new DismantlePlayerSkillRequest { PlayerSkillId = playerSkillId, TargetPlayerSkillId = targetPlayerSkillId };
            ApiClient.Instance.Post<DismantlePlayerSkillRequest, PlayerSkillResponse>(
                ApiConfig.PlayerSkillsDismantle,
                body,
                response =>
                {
                    SafeDebugLog($"DismantlePlayerSkill OK | playerSkillId={playerSkillId}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"DismantlePlayerSkill FAIL | {error.StatusCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void RecordSkillCast(int playerSkillId, Action<PlayerSkillResponse> onSuccess = null, Action<ApiException> onError = null)
        {
            ApiClient.Instance.Post<object, PlayerSkillResponse>(
                string.Format(ApiConfig.PlayerSkillsRecordCast, playerSkillId),
                new { },
                requiresAuth: true,
                onSuccess: response => onSuccess?.Invoke(response),
                onError: error => onError?.Invoke(error)
            );
        }
    }
}
