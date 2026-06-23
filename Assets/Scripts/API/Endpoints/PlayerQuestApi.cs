using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class PlayerQuestApi : BaseApiService<PlayerQuestApi>
    {
        public void GetMyQuests(Action<List<PlayerQuestResponse>> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get<List<PlayerQuestResponse>>(
                ApiConfig.PlayerQuestMe,
                quests =>
                {
                    Debug.Log($"[PlayerQuestApi] GetMyQuests OK | Count={quests?.Count ?? 0}");
                    onSuccess?.Invoke(quests ?? new List<PlayerQuestResponse>());
                },
                error =>
                {
                    SafeDebugError($"GetMyQuests FAIL | {error.StatusCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void GetQuestDetail(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
        {
            var endpoint = string.Format(ApiConfig.PlayerQuestDetail, questId);
            ApiClient.Instance.Get<PlayerQuestResponse>(
                endpoint,
                quest =>
                {
                    Debug.Log($"[PlayerQuestApi] GetQuestDetail OK | questId={questId}");
                    onSuccess?.Invoke(quest);
                },
                error =>
                {
                    SafeDebugError($"GetQuestDetail FAIL | questId={questId} | {error.StatusCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void AcceptQuest(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new AcceptQuestRequest { QuestId = questId };
            ApiClient.Instance.Post<AcceptQuestRequest, PlayerQuestResponse>(
                ApiConfig.PlayerQuestAccept, body,
                quest =>
                {
                    Debug.Log($"[PlayerQuestApi] AcceptQuest OK | questId={questId} status={quest?.Status}");
                    onSuccess?.Invoke(quest);
                },
                error =>
                {
                    SafeDebugError($"AcceptQuest FAIL | questId={questId} | {error.StatusCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void BatchUpdateProgress(List<QuestProgressItem> updates, Action<List<PlayerQuestResponse>> onSuccess, Action<ApiException> onError)
        {
            if (updates == null || updates.Count == 0) { onSuccess?.Invoke(new List<PlayerQuestResponse>()); return; }
            var body = new BatchProgressRequest { Updates = updates };
            ApiClient.Instance.Put<BatchProgressRequest, List<PlayerQuestResponse>>(
                ApiConfig.PlayerQuestBatch, body,
                quests =>
                {
                    Debug.Log($"[PlayerQuestApi] BatchUpdateProgress OK | updated={quests?.Count ?? 0}");
                    onSuccess?.Invoke(quests ?? new List<PlayerQuestResponse>());
                },
                error =>
                {
                    SafeDebugError($"BatchUpdateProgress FAIL | {error.StatusCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void CompleteQuest(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new CompleteQuestRequest { QuestId = questId };
            ApiClient.Instance.Post<CompleteQuestRequest, PlayerQuestResponse>(
                ApiConfig.PlayerQuestComplete, body,
                quest =>
                {
                    Debug.Log($"[PlayerQuestApi] CompleteQuest OK | questId={questId}");
                    onSuccess?.Invoke(quest);
                },
                error =>
                {
                    SafeDebugError($"CompleteQuest FAIL | questId={questId} | {error.StatusCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        public void ClaimReward(int questId, Action<PlayerQuestResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new ClaimQuestRequest { QuestId = questId };
            ApiClient.Instance.Post<ClaimQuestRequest, PlayerQuestResponse>(
                ApiConfig.PlayerQuestClaim, body,
                quest =>
                {
                    Debug.Log($"[PlayerQuestApi] ClaimReward OK | questId={questId}");
                    onSuccess?.Invoke(quest);
                },
                error =>
                {
                    SafeDebugError($"ClaimReward FAIL | questId={questId} | {error.StatusCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
