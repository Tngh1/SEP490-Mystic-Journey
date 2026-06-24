using System;
using MysticJourney.API.Core;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;

namespace MysticJourney.API.Endpoints
{
    public class MonsterApi : BaseApiService<MonsterApi>
    {
        public void GetAll(
            int page,
            int pageSize,
            Action<PagedResultResponse<MonsterResponse>> onSuccess,
            Action<ApiException> onError,
            string search = null,
            string type = null,
            bool? isActive = null)
        {
            var endpoint = $"{ApiConfig.MonsterAll}?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(search)) endpoint += $"&search={UnityEngine.Networking.UnityWebRequest.EscapeURL(search)}";
            if (!string.IsNullOrEmpty(type)) endpoint += $"&type={UnityEngine.Networking.UnityWebRequest.EscapeURL(type)}";
            if (isActive.HasValue) endpoint += $"&isActive={isActive.Value}";

            ApiClient.Instance.Get<PagedResultResponse<MonsterResponse>>(
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
                requiresAuth: false);
        }

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

        public void GetCatalogForPlayer(
            int page,
            int pageSize,
            Action<PagedResultResponse<PlayerMonsterCatalogItem>> onSuccess,
            Action<ApiException> onError,
            string search = null,
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
