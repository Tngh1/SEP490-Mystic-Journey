using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    /// <summary>
    /// API service cho Character: tạo nhân vật, xem chỉ số, nâng cấp attribute.
    /// Tất cả endpoint đều yêu cầu JWT (requiresAuth = true).
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
            Action<ApiResponse<CharacterResponse>> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog($"CreateCharacter → Name={body?.CharacterName} | Class={body?.SelectedClass}");
            ApiClient.Instance.Post<CreateCharacterRequest, ApiResponse<CharacterResponse>>(
                ApiConfig.CharacterCreate,
                body,
                response =>
                {
                    SafeDebugLog($"CreateCharacter OK | Class={response.Data?.PlayerClass} | Level={response.Data?.Level}");
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
            Action<ApiResponse<PlayerStatsResponse>> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog("GetMyStats...");
            ApiClient.Instance.Get<ApiResponse<PlayerStatsResponse>>(
                ApiConfig.CharacterStats,
                response =>
                {
                    SafeDebugLog($"GetMyStats OK | HP={response.Data?.MaxHp} | ATK={response.Data?.Atk} | SKP={response.Data?.SkillPoints}");
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
            Action<ApiResponse<UpgradeAttributeResponse>> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog($"UpgradeAttribute → {body?.AttributeName} x{body?.Amount}");
            ApiClient.Instance.Post<UpgradeAttributeRequest, ApiResponse<UpgradeAttributeResponse>>(
                ApiConfig.CharacterUpgrade,
                body,
                response =>
                {
                    SafeDebugLog($"UpgradeAttribute OK | {response.Data?.UpgradedAttribute} +{response.Data?.AmountSpent} | SKP left={response.Data?.RemainingSkillPoints}");
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
            Action<ApiResponse<object>> onSuccess,
            Action<ApiException> onError)
        {
            var body = new UpdateHpRequest { currentHp = currentHp };
            ApiClient.Instance.Put<UpdateHpRequest, ApiResponse<object>>(
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
    }
}
