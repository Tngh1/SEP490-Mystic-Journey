using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

/// <summary>
/// Quản lý dữ liệu quái từ API: spawn theo map/dungeon, bestiary, phần thưởng khi hạ quái.
/// </summary>
public class MonsterManager : MonoBehaviour
{
    public static MonsterManager Instance { get; private set; }

    public event Action OnSpawnsLoaded;
    public event Action OnCatalogLoaded;
    public event Action<MonsterDefeatResponse> OnMonsterDefeated;

    private readonly Dictionary<int, MonsterDetailResponse> _monsterCache = new();
    private readonly Dictionary<int, PlayerMonsterCatalogItem> _catalogCache = new();
    private MonsterSpawnResponse[] _currentSpawns = Array.Empty<MonsterSpawnResponse>();
    private string _currentMapName;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public MonsterSpawnResponse[] GetCurrentSpawns() => _currentSpawns;

    public PlayerMonsterCatalogItem GetCatalogItem(int monsterId)
    {
        _catalogCache.TryGetValue(monsterId, out var item);
        return item;
    }

    public MonsterDetailResponse GetCachedMonster(int monsterId)
    {
        _monsterCache.TryGetValue(monsterId, out var monster);
        return monster;
    }

    public void LoadSpawnsForMap(string mapName, string regionName = null, int? dungeonId = null, Action onComplete = null)
    {
        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[MonsterManager] Cần đăng nhập để tải spawn quái.");
            onComplete?.Invoke();
            return;
        }

        _currentMapName = mapName;
        MonsterApi.Instance.GetSpawnsForMap(
            mapName,
            spawns =>
            {
                _currentSpawns = spawns ?? Array.Empty<MonsterSpawnResponse>();
                CacheMonstersFromSpawns(_currentSpawns);
                OnSpawnsLoaded?.Invoke();
                onComplete?.Invoke();
            },
            error =>
            {
                Debug.LogError($"[MonsterManager] LoadSpawnsForMap failed: {error.Message}");
                onComplete?.Invoke();
            },
            regionName,
            dungeonId);
    }

    public void LoadCatalog(int page = 1, int pageSize = 50, Action onComplete = null)
    {
        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[MonsterManager] Cần đăng nhập để tải bestiary.");
            onComplete?.Invoke();
            return;
        }

        MonsterApi.Instance.GetCatalogForPlayer(
            page,
            pageSize,
            result =>
            {
                if (result?.Items != null)
                {
                    foreach (var item in result.Items)
                        _catalogCache[item.MonsterId] = item;
                }

                OnCatalogLoaded?.Invoke();
                onComplete?.Invoke();
            },
            error =>
            {
                Debug.LogError($"[MonsterManager] LoadCatalog failed: {error.Message}");
                onComplete?.Invoke();
            });
    }

    public void DiscoverMonster(int monsterId, Action<PlayerMonsterCatalogItem> onSuccess = null)
    {
        MonsterApi.Instance.Discover(
            monsterId,
            item =>
            {
                _catalogCache[monsterId] = item;
                onSuccess?.Invoke(item);
            },
            error => Debug.LogError($"[MonsterManager] Discover failed: {error.Message}"));
    }

    public void ReportDefeat(int monsterId, int? monsterSpawnId = null, int? dungeonSessionId = null)
    {
        MonsterApi.Instance.Defeat(
            monsterId,
            new MonsterDefeatRequest
            {
                MonsterSpawnId = monsterSpawnId,
                DungeonSessionId = dungeonSessionId
            },
            response =>
            {
                if (_catalogCache.TryGetValue(monsterId, out var catalog))
                {
                    catalog.IsDiscovered = true;
                    catalog.TimesDefeated += 1;
                }

                OnMonsterDefeated?.Invoke(response);
            },
            error => Debug.LogError($"[MonsterManager] Defeat failed: {error.Message}"));
    }

    public void LoadMonsterDetail(int monsterId, bool forPlayer, Action<MonsterDetailResponse> onSuccess = null)
    {
        if (forPlayer)
        {
            MonsterApi.Instance.GetByIdForPlayer(
                monsterId,
                detail =>
                {
                    _monsterCache[monsterId] = detail;
                    onSuccess?.Invoke(detail);
                },
                error => Debug.LogError($"[MonsterManager] GetByIdForPlayer failed: {error.Message}"));
            return;
        }

        MonsterApi.Instance.GetById(
            monsterId,
            detail =>
            {
                _monsterCache[monsterId] = detail;
                onSuccess?.Invoke(detail);
            },
            error => Debug.LogError($"[MonsterManager] GetById failed: {error.Message}"));
    }

    private void CacheMonstersFromSpawns(IEnumerable<MonsterSpawnResponse> spawns)
    {
        foreach (var spawn in spawns)
        {
            if (spawn?.Monster == null)
                continue;

            _monsterCache[spawn.MonsterId] = new MonsterDetailResponse
            {
                MonsterId = spawn.Monster.MonsterId,
                Name = spawn.Monster.Name,
                Type = spawn.Monster.Type,
                Description = spawn.Monster.Description,
                Level = spawn.Monster.Level,
                MaxHp = spawn.Monster.MaxHp,
                Atk = spawn.Monster.Atk,
                Def = spawn.Monster.Def,
                MoveSpeed = spawn.Monster.MoveSpeed,
                AttackSpeed = spawn.Monster.AttackSpeed,
                CritRate = spawn.Monster.CritRate,
                CritDamage = spawn.Monster.CritDamage,
                ExperienceReward = spawn.Monster.ExperienceReward,
                GoldReward = spawn.Monster.GoldReward,
                ImageUrl = spawn.Monster.ImageUrl,
                IsActive = spawn.Monster.IsActive
            };
        }
    }
}
