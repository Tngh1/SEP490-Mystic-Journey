using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class DungeonApi : BaseApiService<DungeonApi>
    {
        // ── Catalog (không cần auth) ──────────────────────────────────────────

        public void GetAll(
            int page,
            int pageSize,
            Action<PaginatedResponse<DungeonResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string type = null,
            bool? isActive = null)
        {
            string endpoint = $"{ApiConfig.DungeonAll}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={search}";
            if (!string.IsNullOrEmpty(type)) endpoint += $"&type={type}";
            if (isActive.HasValue) endpoint += $"&isActive={isActive.Value}";

            SafeDebugLog($"GetAll → page={page} pageSize={pageSize}");
            ApiClient.Instance.Get<PaginatedResponse<DungeonResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetAll OK | TotalCount={response.TotalCount} | Page={response.Page}/{response.TotalPages}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetAll FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }

        public void GetById(int dungeonConfigId, Action<DungeonResponse> onSuccess, Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonById, dungeonConfigId);
            SafeDebugLog($"GetById → dungeonConfigId={dungeonConfigId}");
            ApiClient.Instance.Get<DungeonResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetById OK | Name={response.Name} | EnergyCost={response.EnergyCost} | Difficulty={response.Difficulty}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetById FAIL | dungeonConfigId={dungeonConfigId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: false);
        }

        // ── Session (yêu cầu auth) ────────────────────────────────────────────

        /// <summary>
        /// POST /api/dungeons/{dungeonId}/enter
        /// Validate nhân vật + dungeon + energy. Energy chưa bị trừ.
        /// Tạo DungeonSession với Status="Active".
        /// </summary>
        public void Enter(
            int dungeonConfigId,
            Action<ApiResponse<EnterDungeonResponse>> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonEnter, dungeonConfigId);
            SafeDebugLog($"Enter → dungeonConfigId={dungeonConfigId}");
            ApiClient.Instance.PostEmpty<ApiResponse<EnterDungeonResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"Enter OK | SessionId={response.Data?.DungeonSessionId} | Energy={response.Data?.PlayerCurrentEnergy}/{response.Data?.EnergyCost}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Enter FAIL | dungeonConfigId={dungeonConfigId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        /// <summary>
        /// POST /api/dungeons/session/{sessionId}/progress
        /// Cập nhật tiến trình chiến đấu: quái đã giết, boss, % hoàn thành.
        /// Session phải đang Active (BR-07).
        /// </summary>
        public void UpdateProgress(
            int sessionId,
            UpdateDungeonProgressRequest body,
            Action<ApiResponse<DungeonProgressResponse>> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonProgress, sessionId);
            SafeDebugLog($"UpdateProgress → sessionId={sessionId} | Monsters={body?.MonstersKilled} | Boss={body?.BossKilled} | %={body?.CompletionPercentage}");
            ApiClient.Instance.Post<UpdateDungeonProgressRequest, ApiResponse<DungeonProgressResponse>>(
                endpoint,
                body,
                response =>
                {
                    SafeDebugLog($"UpdateProgress OK | Boss={response.Data?.BossKilled} | %={response.Data?.CompletionPercentage}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UpdateProgress FAIL | sessionId={sessionId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        /// <summary>
        /// POST /api/dungeons/session/{sessionId}/complete
        /// Đánh dấu dungeon hoàn thành, trả về preview chest.
        /// KHÔNG cấp reward – phải gọi ClaimReward() sau.
        /// Boss phải đã bị giết (BossKilled=true).
        /// </summary>
        public void Complete(
            int sessionId,
            Action<ApiResponse<CompleteDungeonResponse>> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonComplete, sessionId);
            SafeDebugLog($"Complete → sessionId={sessionId}");
            ApiClient.Instance.PostEmpty<ApiResponse<CompleteDungeonResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"Complete OK | Status={response.Data?.Status} | Chest={response.Data?.RewardChest?.Name}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Complete FAIL | sessionId={sessionId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        /// <summary>
        /// POST /api/dungeons/session/{sessionId}/claim-reward
        /// Trừ Energy + tạo reward + lưu inventory (TRANSACTIONAL).
        /// Session phải Status="Completed" và chưa claimed.
        /// Nếu thất bại → rollback toàn bộ, không mất gì.
        /// </summary>
        public void ClaimReward(
            int sessionId,
            Action<ApiResponse<ClaimDungeonRewardResponse>> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonClaimReward, sessionId);
            SafeDebugLog($"ClaimReward → sessionId={sessionId}");
            ApiClient.Instance.PostEmpty<ApiResponse<ClaimDungeonRewardResponse>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"ClaimReward OK | Gold={response.Data?.GoldEarned} | XP={response.Data?.ExperienceEarned} | Items={response.Data?.Items?.Length}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"ClaimReward FAIL | sessionId={sessionId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
