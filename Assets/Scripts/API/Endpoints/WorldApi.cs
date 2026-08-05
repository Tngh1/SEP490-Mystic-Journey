using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;
using UnityEngine;

namespace MysticJourney.API.Endpoints
{
    // ═══════════════════════════════════════════════════════════════════════
    // WORLD API - Thế giới game
    // ═══════════════════════════════════════════════════════════════════════
    public class WorldApi : BaseApiService<WorldApi>
    {
        private const string DefaultMap = "Map001";

        // ═══════════════════════════════════════════════════════════════════════
        // GAME APIs (Người chơi)
        // ═══════════════════════════════════════════════════════════════════════

        // ── Lấy trạng thái world ──────────────────────
        public void GetState(Action<WorldStateResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get<WorldStateResponse>(
                ApiConfig.WorldState,
                response =>
                {
                    var state = GameStateService.Instance;
                    if (state != null)
                    {
                        state.PlayerProfileId = response.PlayerProfileId;
                        ApplyWorldPosition(response.Position);
                    }
                    onSuccess?.Invoke(response);
                },
                onError,
                requiresAuth: true
            );
        }

        // ── Cập nhật vị trí world ────────────────────
        public void UpdatePosition(
            string mapName,
            Vector3 position,
            Action<PlayerWorldPositionResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new UpdateWorldPositionRequest
            {
                MapName = string.IsNullOrWhiteSpace(mapName) ? DefaultMap : mapName.Trim(),
                PositionX = position.x,
                PositionY = position.y
            };

            ApiClient.Instance.Put<UpdateWorldPositionRequest, PlayerWorldPositionResponse>(
                ApiConfig.WorldPosition,
                body,
                response =>
                {
                    ApplyWorldPosition(response);
                    onSuccess?.Invoke(response);
                },
                onError,
                requiresAuth: true
            );
        }

        // ── Nói chuyện với NPC ───────────────────────
        public void TalkToNpc(int npcId, Action<TalkToNpcResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new TalkToNpcRequest { NPCId = npcId };
            ApiClient.Instance.Post<TalkToNpcRequest, TalkToNpcResponse>(
                ApiConfig.WorldNpcTalk, body,
                response => onSuccess?.Invoke(response),
                onError, requiresAuth: true);
        }

        // ── Nộp quest cho NPC ─────────────────────────
        public void TurnInQuestItem(
            int npcId,
            int questId,
            Action<TurnInQuestItemResponse> onSuccess,
            Action<ApiException> onError)
        {
            var body = new TurnInQuestItemRequest { NPCId = npcId, QuestId = questId };
            ApiClient.Instance.Post<TurnInQuestItemRequest, TurnInQuestItemResponse>(
                ApiConfig.WorldNpcTurnIn, body,
                response => onSuccess?.Invoke(response),
                onError, requiresAuth: true);
        }

        // ── Tương tác với object ─────────────────────
        public void InteractObject(
            string objectKey,
            string interactionType,
            int? questId,
            int progressDelta,
            Action<InteractObjectResponse> onSuccess,
            Action<ApiException> onError)
        {
            var state = GameStateService.Instance;
            var currentMap = state != null ? state.CurrentMapName : DefaultMap;

            var body = new InteractObjectRequest
            {
                MapName = string.IsNullOrWhiteSpace(currentMap) ? DefaultMap : currentMap,
                ObjectKey = objectKey,
                InteractionType = string.IsNullOrWhiteSpace(interactionType) ? "Interact" : interactionType,
                QuestId = questId,
                ProgressDelta = Mathf.Max(1, progressDelta)
            };
            ApiClient.Instance.Post<InteractObjectRequest, InteractObjectResponse>(
                ApiConfig.WorldInteract, body,
                response => onSuccess?.Invoke(response),
                onError, requiresAuth: true);
        }

        [Serializable]
        public class ClaimDropRequest
        {
            public string DropType;
            public int Quantity;
            public int ItemId;
            public string ItemName;
        }

        [Serializable]
        public class ClaimDropResponse
        {
            public bool Success;
            public string Message;
            public double NewGold;
            public int NewExperience;
            public int NewLevel;
        }

        public void ClaimDrop(string dropType, int quantity, int itemId, string itemName, Action<ClaimDropResponse> onSuccess, Action<ApiException> onError = null)
        {
            var body = new ClaimDropRequest
            {
                DropType = dropType ?? "",
                Quantity = quantity,
                ItemId = itemId,
                ItemName = itemName ?? ""
            };

            ApiClient.Instance.Post<ClaimDropRequest, ClaimDropResponse>(
                ApiConfig.WorldClaimDrop, body,
                response => onSuccess?.Invoke(response),
                onError, requiresAuth: true);
        }

        // ── Private: Áp dụng vị trí world ────────────
        private static void ApplyWorldPosition(PlayerWorldPositionResponse position)
        {
            if (position == null) return;

            var state = GameStateService.Instance;
            if (state == null) return;

            var mapName = string.IsNullOrWhiteSpace(position.MapName) ? DefaultMap : position.MapName.Trim();
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
