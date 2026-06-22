using System;
using System.Text;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class SkillApi : BaseApiService<SkillApi>
    {
        public void GetAll(int page, int pageSize, Action<PagedResultResponse<SkillResponse>> onSuccess, Action<ApiException> onError, string search = null, string type = null, bool? isActive = null)
        {
            var endpoint = $"{ApiConfig.SkillAll}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={UnityEngine.Networking.UnityWebRequest.EscapeURL(search)}";
            if (!string.IsNullOrEmpty(type)) endpoint += $"&type={UnityEngine.Networking.UnityWebRequest.EscapeURL(type)}";
            if (isActive.HasValue) endpoint += $"&isActive={isActive.Value}";

            ApiClient.Instance.Get<PagedResultResponse<SkillResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetAll OK | TotalCount={response.TotalCount}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }

        public void GetById(int skillId, Action<SkillResponse> onSuccess, Action<ApiException> onError)
        {
            var endpoint = string.Format(ApiConfig.SkillById, skillId);
            ApiClient.Instance.Get<SkillResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetById OK | skillId={skillId} | Name={response.Name}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetById FAIL | skillId={skillId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }

        public void GetMySkills(Action<PlayerMeSkillsResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog("GetMySkills...");
            ApiClient.Instance.Get<PlayerMeSkillsResponse>(
                ApiConfig.PlayerMeSkills,
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

        public void UpgradePlayerSkill(int playerSkillId, Action<PlayerSkillResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"UpgradePlayerSkill → playerSkillId={playerSkillId}");
            var body = new UpgradePlayerSkillRequest { PlayerSkillId = playerSkillId };
            ApiClient.Instance.Post<UpgradePlayerSkillRequest, PlayerSkillResponse>(
                ApiConfig.PlayerSkillUpgrade,
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

        public void EquipPlayerSkill(int playerSkillId, bool isEquipped, int? slotIndex, Action<PlayerSkillResponse> onSuccess, Action<ApiException> onError)
        {
            SafeDebugLog($"EquipPlayerSkill → playerSkillId={playerSkillId}, isEquipped={isEquipped}, slotIndex={slotIndex}");

            // Tạo gói dữ liệu DTO gửi lên Server
            var body = new EquipSkillRequest { PlayerSkillId = playerSkillId, IsEquipped = isEquipped, SlotIndex = slotIndex };

            ApiClient.Instance.Post<EquipSkillRequest, PlayerSkillResponse>(
                ApiConfig.PlayerSkillEquip,
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
    }
}
