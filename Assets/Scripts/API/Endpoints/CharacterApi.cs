using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    /// <summary>
    /// API service cho Character: tạo nhân vật, xem chỉ số, nâng cấp attribute.
    /// Tất cả endpoint đều yêu cầu JWT (requiresAuth = true).
    /// ApiClient tự động xử lý envelope { success, message, errorCode, data } từ BE.
    /// </summary>
    public class CharacterApi : BaseApiService<CharacterApi>
    {
        // ── POST /api/characters ──────────────────────────────────────────────
        /// <summary>
        /// Tạo nhân vật lần đầu sau khi đăng ký: đặt tên + chọn class.
        /// Gọi sau khi LoginGame() thành công và chưa có PlayerStat.
        /// Energy chưa bị trừ ở bước này.
        /// </summary>
        public void CreateCharacter(
            CreateCharacterRequest body,
            Action<CharacterResponse> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog($"CreateCharacter → Name={body?.CharacterName} | Class={body?.SelectedClass}");
            ApiClient.Instance.Post<CreateCharacterRequest, CharacterResponse>(
                ApiConfig.CharacterCreate,
                body,
                response =>
                {
                    SafeDebugLog($"CreateCharacter OK | Class={response.PlayerClass} | Level={response.Level}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"CreateCharacter FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── GET /api/characters/stats ─────────────────────────────────────────
        /// <summary>
        /// Lấy toàn bộ chỉ số nhân vật (HP, ATK, DEF, tốc độ, crit, skill points...).
        /// Dùng để hiển thị màn hình Stat hoặc sync lên game object khi load scene.
        /// </summary>
        public void GetMyStats(
            Action<PlayerStatsResponse> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog("GetMyStats...");
            ApiClient.Instance.Get<PlayerStatsResponse>(
                ApiConfig.CharacterStats,
                response =>
                {
                    SafeDebugLog($"GetMyStats OK | HP={response.MaxHp} | ATK={response.Atk} | SKP={response.SkillPoints}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetMyStats FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── POST /api/characters/upgrade ──────────────────────────────────────
        /// <summary>
        /// Chi Skill Points để tăng 1 attribute.
        /// Attribute hợp lệ: MaxHp | Atk | Def | MoveSpeed | AttackSpeed | CritRate | CritDamage | DamageBonus
        /// </summary>
        public void UpgradeAttribute(
            UpgradeAttributeRequest body,
            Action<UpgradeAttributeResponse> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog($"UpgradeAttribute → {body?.AttributeName} x{body?.Amount}");
            ApiClient.Instance.Post<UpgradeAttributeRequest, UpgradeAttributeResponse>(
                ApiConfig.CharacterUpgrade,
                body,
                response =>
                {
                    SafeDebugLog($"UpgradeAttribute OK | {response.UpgradedAttribute} +{response.AmountSpent} | SKP left={response.RemainingSkillPoints}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UpgradeAttribute FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // ── PUT /api/characters/hp ────────────────────────────────────────────
        /// <summary>
        /// Đồng bộ máu hiện tại về Backend
        /// </summary>
        public void UpdateHp(
            int currentHp,
            Action<SimpleResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new UpdateHpRequest { currentHp = currentHp };
            ApiClient.Instance.Put<UpdateHpRequest, SimpleResponse>(
                ApiConfig.CharacterHp,
                body,
                response =>
                {
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UpdateHp FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
        // ── POST /api/characters/buffs ───────────────────────────────────────
        /// <summary>
        /// Sync active buffs with the server.
        /// </summary>
        public void SyncBuffs(
            UpdatePlayerBuffsRequest request,
            Action onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog("SyncBuffs...");
            ApiClient.Instance.Post<UpdatePlayerBuffsRequest, object>(
                ApiConfig.CharacterBuffs,
                request,
                response =>
                {
                    SafeDebugLog("SyncBuffs OK");
                    onSuccess?.Invoke();
                },
                error =>
                {
                    SafeDebugError($"SyncBuffs FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void GetLevelUpOptions(
            Action<System.Collections.Generic.List<string>> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog("GetLevelUpOptions...");
            ApiClient.Instance.Get<System.Collections.Generic.List<string>>(
                ApiConfig.CharacterLevelUpOptions,
                response =>
                {
                    SafeDebugLog("GetLevelUpOptions OK");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetLevelUpOptions FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void AllocateStat(
            AllocateStatRequestDto request,
            Action<PlayerStatsResponse> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog($"AllocateStat {request.StatName}...");
            ApiClient.Instance.Post<AllocateStatRequestDto, PlayerStatsResponse>(
                ApiConfig.CharacterAllocateStat,
                request,
                response =>
                {
                    SafeDebugLog("AllocateStat OK");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"AllocateStat FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
