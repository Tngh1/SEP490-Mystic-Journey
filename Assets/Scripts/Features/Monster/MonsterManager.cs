using System;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

// Executes core business logic for mono behaviour.
public class MonsterManager : MonoBehaviour
{
    private static MonsterManager _instance;
    // Executes core business logic for instance.
    public static MonsterManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[MonsterManager]");
                _instance = go.AddComponent<MonsterManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    public event Action OnSpawnsLoaded;
    public event Action OnCatalogLoaded;
    public event Action<MonsterDefeatResponse> OnMonsterDefeated;

    private readonly Dictionary<int, MonsterDetailResponse> _monsterCache = new();
    private readonly Dictionary<int, PlayerMonsterCatalogItem> _catalogCache = new();
    private MonsterSpawnResponse[] _currentSpawns = Array.Empty<MonsterSpawnResponse>();
    private string _currentMapName;

    // Initializes internal component caches and dependencies for MonsterManager upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Executes core business logic for get current spawns.
    public MonsterSpawnResponse[] GetCurrentSpawns() => _currentSpawns;

    // Executes core business logic for get catalog item.
    public PlayerMonsterCatalogItem GetCatalogItem(int monsterId)
    {
        _catalogCache.TryGetValue(monsterId, out var item);
        return item;
    }

    // Executes core business logic for get cached monster.
    public MonsterDetailResponse GetCachedMonster(int monsterId)
    {
        _monsterCache.TryGetValue(monsterId, out var monster);
        return monster;
    }

    // Executes core business logic for load spawns for map.
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

    // Executes core business logic for load catalog.
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

    // Executes core business logic for discover monster.
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

    // Executes core business logic for report defeat.
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

    // Executes core business logic for load monster detail.
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

    // Executes core business logic for cache monsters from spawns.
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
