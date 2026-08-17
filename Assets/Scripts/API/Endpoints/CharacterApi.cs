using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class CharacterApi : BaseApiService<CharacterApi>
    {
        // ─── Player APIs ───────────────────────────────────────────────────────
        // Execute character creation inside one transaction so profile, stats, starter skill, and default skin are committed together or rolled back together.
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

        // Load my stats using on success and on error; it sends the GET API request and guards invalid or unavailable states.
        public void GetMyStats(
            Action<PlayerStatsResponse> onSuccess,
            Action<ApiException> onError)
        {
            SafeDebugLog("GetMyStats...");
            ApiClient.Instance.Get<PlayerStatsResponse>(
                ApiConfig.CharacterStats,
                response =>
                {
                    if (response != null)  // Entity exists — proceed with conditional branch
                    {
                        SafeDebugLog($"GetMyStats OK | HP={response.MaxHp} | ATK={response.Atk} | SKP={response.SkillPoints} | ASPD={response.AttackSpeed}");
                        onSuccess?.Invoke(response);
                    }
                },
                error =>
                {
                    SafeDebugError($"GetMyStats FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Update hp using current hp, on success, and on error; it sends the PUT API request.
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
        // Replace the player's persisted buff rows with the supplied active buffs, save the new set, and return the recalculated effective stats.
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

    // Load level up options using on success and on error; it sends the GET API request.
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

    // Process allocate stat using request, on success, and on error; it sends the POST API request.
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
