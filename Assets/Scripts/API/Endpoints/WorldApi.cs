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
        private const string DefaultMap = "Map001";


        // Executes get state operation.
        public void GetState(Action<WorldStateResponse> onSuccess, Action<ApiException> onError)
        {
            var stateAtRequest = GameStateService.Instance;
            var mapAtRequest = stateAtRequest?.CurrentMapName;

            ApiClient.Instance.Get<WorldStateResponse>(
                ApiConfig.WorldState,
                response =>
                {
                    var state = GameStateService.Instance;
                    if (state != null)  // Entity exists — proceed with conditional branch
                    {
                        state.PlayerProfileId = response.PlayerProfileId;
                        ApplyMapProgression(state, response);
                        var currentMap = state.CurrentMapName;
                        var responseMap = response?.Position?.MapName;
                        var mapChangedWhileWaiting = !MapNamesEqual(currentMap, mapAtRequest);
                        var responseMatchesCurrentMap = MapNamesEqual(responseMap, currentMap);

                        if (!mapChangedWhileWaiting || responseMatchesCurrentMap)
                        {
                            ApplyWorldPosition(response.Position);
                        }
                        else
                        {
                            Debug.Log($"[WorldApi] Ignored stale GetState position for '{responseMap}' " +
                                      $"because player moved from '{mapAtRequest}' to '{currentMap}'.");
                        }
                    }
                    onSuccess?.Invoke(response);
                },
                onError,
                requiresAuth: true
            );
        }

        // Executes get position operation.
        public void GetPosition(Action<PlayerWorldPositionResponse> onSuccess, Action<ApiException> onError)
        {
            ApiClient.Instance.Get<PlayerWorldPositionResponse>(
                ApiConfig.WorldPosition,
                response =>
                {
                    ApplyWorldPosition(response);
                    onSuccess?.Invoke(response);
                },
                onError,
                requiresAuth: true
            );
        }

        // Process the supplied values: normalizes or validates the text before returning the derived result.
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

        // Executes talk to npc operation.
        public void TalkToNpc(int npcId, Action<TalkToNpcResponse> onSuccess, Action<ApiException> onError)
        {
            var body = new TalkToNpcRequest { NPCId = npcId };
            ApiClient.Instance.Post<TalkToNpcRequest, TalkToNpcResponse>(
                ApiConfig.WorldNpcTalk, body,
                response => onSuccess?.Invoke(response),
                onError, requiresAuth: true);
        }

        // Process the supplied values: maps the input discriminator to the corresponding domain value and fallback.
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

        // Process the supplied values: normalizes or validates the text before returning the derived result and maps the input discriminator to the corresponding domain value and fallback.
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

        // Executes apply map progression operation.
        private static void ApplyMapProgression(GameStateService state, WorldStateResponse response)
        {
            int highestUnlocked = Mathf.Max(
                MapProgressionRules.FirstMapId,
                state.HighestUnlockedMapId);
            if (response?.Maps != null)
            {
                foreach (var map in response.Maps)
                {
                    if (map == null || !map.IsUnlocked) continue;
                    highestUnlocked = Mathf.Max(highestUnlocked, MapProgressionRules.GetMapId(map.MapName));
                }
            }

            state.HighestUnlockedMapId = highestUnlocked;
            PlayerPresence.RefreshLocal();
        }

        // Executes map names equal operation.
        // Validates input parameters against null or empty values.
        private static bool MapNamesEqual(string left, string right)
        {
            return string.Equals(NormalizeMapName(left), NormalizeMapName(right), StringComparison.OrdinalIgnoreCase);
        }

        // Normalizes world map names and maps aliases (such as ElfForest) to canonical map identifiers.
        private static string NormalizeMapName(string mapName)
        {
            if (string.IsNullOrWhiteSpace(mapName))  // Mandatory string argument is blank — fail fast
                return string.Empty;

            var value = mapName.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
            return string.Equals(value, "ElfLand", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Map1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Chapter1", StringComparison.OrdinalIgnoreCase)
                ? "ElfForest"
                : value;
        }

        // Executes apply world position operation.
        private static void ApplyWorldPosition(PlayerWorldPositionResponse position)
        {
            if (position == null) return;  // Entity not found — short-circuit with appropriate error result

            var state = GameStateService.Instance;
            if (state == null) return;  // Entity not found — short-circuit with appropriate error result

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
