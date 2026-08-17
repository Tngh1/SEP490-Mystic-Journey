using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class MonsterApi : BaseApiService<MonsterApi>
    {
        // Executes get by id operation.
        public void GetById(int monsterId, Action<MonsterDetailResponse> onSuccess, Action<ApiException> onError)
        {
            var endpoint = string.Format(ApiConfig.MonsterById, monsterId);
            ApiClient.Instance.Get<MonsterDetailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetById OK | monsterId={monsterId} | Name={response.Name}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetById FAIL | monsterId={monsterId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Executes get by id for player operation.
        public void GetByIdForPlayer(int monsterId, Action<MonsterDetailResponse> onSuccess, Action<ApiException> onError)
        {
            var endpoint = string.Format(ApiConfig.MonsterByIdForPlayer, monsterId);
            ApiClient.Instance.Get<MonsterDetailResponse>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetByIdForPlayer OK | monsterId={monsterId} | Name={response.Name}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetByIdForPlayer FAIL | monsterId={monsterId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Load catalog for player using page, page size, on success, and on error; it sends the GET API request and guards invalid or unavailable states.
        public void GetCatalogForPlayer(
            int page,
            int pageSize,
            Action<PagedResultResponse<PlayerMonsterCatalogItem>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            // Supported monster types: Normal, Elite, or Boss; the type controls presentation and encounter behavior.
            string type = null)
        {
            var endpoint = $"{ApiConfig.MonsterCatalogForPlayer}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={UnityEngine.Networking.UnityWebRequest.EscapeURL(search)}";
            if (!string.IsNullOrEmpty(type)) endpoint += $"&type={UnityEngine.Networking.UnityWebRequest.EscapeURL(type)}";

            ApiClient.Instance.Get<PagedResultResponse<PlayerMonsterCatalogItem>>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetCatalogForPlayer OK | TotalCount={response.TotalCount}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetCatalogForPlayer FAIL | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Load spawns for map using map name, on success, on error, and region name; it sends the GET API request and guards invalid or unavailable states.
        public void GetSpawnsForMap(
            string mapName,
            Action<MonsterSpawnResponse[]> onSuccess,
            Action<ApiException> onError,
            string regionName = null,
            int? dungeonId = null)
        {
            var endpoint = $"{ApiConfig.MonsterSpawns}?mapName={UnityEngine.Networking.UnityWebRequest.EscapeURL(mapName)}";
            if (!string.IsNullOrEmpty(regionName))
                endpoint += $"&regionName={UnityEngine.Networking.UnityWebRequest.EscapeURL(regionName)}";
            if (dungeonId.HasValue)
                endpoint += $"&dungeonId={dungeonId.Value}";

            ApiClient.Instance.Get<MonsterSpawnResponse[]>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"GetSpawnsForMap OK | map={mapName} | count={response?.Length ?? 0}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"GetSpawnsForMap FAIL | map={mapName} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Executes discover operation.
        public void Discover(int monsterId, Action<PlayerMonsterCatalogItem> onSuccess, Action<ApiException> onError)
        {
            var endpoint = string.Format(ApiConfig.MonsterDiscover, monsterId);
            ApiClient.Instance.PostEmpty<PlayerMonsterCatalogItem>(
                endpoint,
                response =>
                {
                    SafeDebugLog($"Discover OK | monsterId={monsterId} | Name={response.Name}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Discover FAIL | monsterId={monsterId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }

        // Process defeat using monster id, body, on success, and on error; it sends the POST API request.
        public void Defeat(
            int monsterId,
            MonsterDefeatRequest body,
            Action<MonsterDefeatResponse> onSuccess,
            Action<ApiException> onError)
        {
            var endpoint = string.Format(ApiConfig.MonsterDefeat, monsterId);
            ApiClient.Instance.Post<MonsterDefeatRequest, MonsterDefeatResponse>(
                endpoint,
                body ?? new MonsterDefeatRequest(),
                response =>
                {
                    SafeDebugLog($"Defeat OK | monsterId={monsterId} | XP={response.ExperienceEarned} | Gold={response.GoldEarned}");
                    onSuccess?.Invoke(response);
                },
                error =>
                {
                    SafeDebugError($"Defeat FAIL | monsterId={monsterId} | {error.StatusCode} {error.ErrorCode}: {error.Message}");
                    onError?.Invoke(error);
                },
                requiresAuth: true);
        }
    }
}
