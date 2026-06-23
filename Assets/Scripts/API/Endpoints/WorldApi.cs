using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;
using MysticJourney.Core.Utilities;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    public class WorldApi : BaseApiService<WorldApi>
    {
        public void GetState(Action<WorldStateResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get<ApiResponse<WorldStateResponse>>(
                ApiConfig.WorldState,
                response =>
                {
                    if (response.Success && response.Data != null)
                    {
                        var state = GameStateService.Instance;
                        state.PlayerProfileId = response.Data.PlayerProfileId;
                        ApplyWorldPosition(response.Data.Position);
                    }
                    onSuccess?.Invoke(response.Data);
                },
                onError,
                requiresAuth: true
            );
        }

        public void UpdatePosition(
            string mapName,
            Vector3 position,
            Action<PlayerWorldPositionResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new UpdateWorldPositionRequest
            {
                MapName = string.IsNullOrWhiteSpace(mapName) ? GameConstants.WorldDefaults.DefaultMap : mapName.Trim(),
                PositionX = position.x,
                PositionY = position.y
            };

            ApiClient.Instance.Put<UpdateWorldPositionRequest, ApiResponse<PlayerWorldPositionResponse>>(
                ApiConfig.WorldPosition,
                body,
                response =>
                {
                    if (response.Success && response.Data != null)
                        ApplyWorldPosition(response.Data);
                    onSuccess?.Invoke(response.Data);
                },
                onError,
                requiresAuth: true
            );
        }

        public void TalkToNpc(int npcId, Action<TalkToNpcResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new TalkToNpcRequest { NPCId = npcId };
            ApiClient.Instance.Post<TalkToNpcRequest, ApiResponse<TalkToNpcResponse>>(
                ApiConfig.WorldNpcTalk, body,
                response => onSuccess?.Invoke(response.Data),
                onError, requiresAuth: true);
        }

        public void TurnInQuestItem(
            int npcId,
            int questId,
            Action<TurnInQuestItemResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new TurnInQuestItemRequest { NPCId = npcId, QuestId = questId };
            ApiClient.Instance.Post<TurnInQuestItemRequest, ApiResponse<TurnInQuestItemResponse>>(
                ApiConfig.WorldNpcTurnIn, body,
                response => onSuccess?.Invoke(response.Data),
                onError, requiresAuth: true);
        }

        public void InteractObject(
            string objectKey,
            string interactionType,
            int? questId,
            int progressDelta,
            Action<InteractObjectResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new InteractObjectRequest
            {
                MapName = string.IsNullOrWhiteSpace(WorldState.CurrentMapName) ? "ElfForest" : WorldState.CurrentMapName,
                ObjectKey = objectKey,
                InteractionType = string.IsNullOrWhiteSpace(interactionType) ? "Interact" : interactionType,
                QuestId = questId,
                ProgressDelta = Mathf.Max(1, progressDelta)
            };
            ApiClient.Instance.Post<InteractObjectRequest, ApiResponse<InteractObjectResponse>>(
                ApiConfig.WorldInteractObject, body,
                response => onSuccess?.Invoke(response.Data),
                onError, requiresAuth: true);
        }

        public void OpenChestByChestId(int chestId, Action<OpenChestResponse> onSuccess, Action<ApiException> onError)
            => OpenChest(new OpenWorldChestRequest { ChestId = chestId }, onSuccess, onError);

        public void OpenChestByPlayerChestId(int playerChestId, Action<OpenChestResponse> onSuccess, Action<ApiException> onError)
            => OpenChest(new OpenWorldChestRequest { PlayerChestId = playerChestId }, onSuccess, onError);

        public void ClaimDailyLoginReward(Action<ClaimDailyRewardResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.PostEmpty<ApiResponse<ClaimDailyRewardResponse>>(
                ApiConfig.WorldDailyLoginClaim,
                response => onSuccess?.Invoke(response.Data),
                onError, requiresAuth: true);
        }

        public void GetDailyLoginStatus(Action<PlayerDailyLoginResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get<ApiResponse<PlayerDailyLoginResponse>>(
                ApiConfig.DailyLoginStatus,
                response => onSuccess?.Invoke(response.Data),
                onError, requiresAuth: true);
        }

        private void OpenChest(OpenWorldChestRequest body, Action<OpenChestResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Post<OpenWorldChestRequest, ApiResponse<OpenChestResponse>>(
                ApiConfig.WorldChestOpen, body,
                response => onSuccess?.Invoke(response.Data),
                onError, requiresAuth: true);
        }

        private static void ApplyWorldPosition(PlayerWorldPositionResponse position)
        {
            if (position == null) return;

            var state = GameStateService.Instance;
            var mapName = string.IsNullOrWhiteSpace(position.MapName) ? GameConstants.WorldDefaults.DefaultMap : position.MapName.Trim();
            var vector = new Vector3((float)position.PositionX, (float)position.PositionY, 0f);

            state.CurrentMapName = mapName;
            state.LastPosition = vector;

            PlayerPrefs.SetString(ApiConfig.LastMapNameKey, mapName);
            PlayerPrefs.SetFloat(ApiConfig.PositionXKey, vector.x);
            PlayerPrefs.SetFloat(ApiConfig.PositionYKey, vector.y);
            PlayerPrefs.Save();
        }
    }
}
