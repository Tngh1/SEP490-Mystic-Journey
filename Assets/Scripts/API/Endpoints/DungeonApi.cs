using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class DungeonApi : BaseApiService<DungeonApi>
    {

        // ─── Player APIs ───────────────────────────────────────────────────────
        // Load all using page, page size, on success, and on error; it sends the GET API request and guards invalid or unavailable states.
        public void GetAll(
            int page,
            int pageSize,
            Action<PaginatedResponse<DungeonResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            // Dungeon type is a free-form category with Normal as the current default; the backend does not enforce a closed allowlist.
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
                requiresAuth: true);
        }

        // Executes get by id operation.
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
                requiresAuth: true);
        }

        // Process enter using dungeon config id, party members, on success, and on error; it sends the POST API request.
        public void Enter(
            int dungeonConfigId,
            List<string> partyMembers,
            Action<EnterDungeonResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonEnter, dungeonConfigId);
            SafeDebugLog($"Enter → dungeonConfigId={dungeonConfigId} | PartyCount={partyMembers?.Count ?? 0}");

            var body = new EnterDungeonRequest { PartyMembers = partyMembers ?? new List<string>() };

            ApiClient.Instance.Post<EnterDungeonRequest, EnterDungeonResponse>(
                endpoint,
                body,
                response =>
                {
                    SafeDebugLog($"Enter OK | SessionId={response.DungeonSessionId} | Energy={response.PlayerCurrentEnergy}/{response.EnergyCost}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Enter FAIL | dungeonConfigId={dungeonConfigId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Update progress using session id, body, on success, and on error; it sends the POST API request.
        public void UpdateProgress(
            int sessionId,
            UpdateDungeonProgressRequest body,
            Action<DungeonProgressResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonSessionProgress, sessionId);
            SafeDebugLog($"UpdateProgress → sessionId={sessionId} | Monsters={body?.MonstersKilled} | Boss={body?.BossKilled} | %={body?.CompletionPercentage}");
            ApiClient.Instance.Post<UpdateDungeonProgressRequest, DungeonProgressResponse>(
                endpoint,
                body,
                response =>
                {
                    SafeDebugLog($"UpdateProgress OK | Boss={response.BossKilled} | %={response.CompletionPercentage}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"UpdateProgress FAIL | sessionId={sessionId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Process complete using session id, on success, and on error; it sends the POST API request.
        public void Complete(
            int sessionId,
            Action<CompleteDungeonResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonSessionComplete, sessionId);
            SafeDebugLog($"Complete → sessionId={sessionId}");
            ApiClient.Instance.PostEmpty<CompleteDungeonResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"Complete OK | Status={response.Status} | Chest={response.RewardChest?.Name}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Complete FAIL | sessionId={sessionId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Process claim reward using session id, on success, and on error; it sends the POST API request.
        public void ClaimReward(
            int sessionId,
            Action<ClaimDungeonRewardResponse> onSuccess,
            Action<ApiException> onError)
        {
            string endpoint = string.Format(ApiConfig.DungeonSessionClaimReward, sessionId);
            SafeDebugLog($"ClaimReward → sessionId={sessionId}");
            ApiClient.Instance.PostEmpty<ClaimDungeonRewardResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"ClaimReward OK | Gold={response.GoldEarned} | XP={response.ExperienceEarned} | Items={response.Items?.Length}");
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
