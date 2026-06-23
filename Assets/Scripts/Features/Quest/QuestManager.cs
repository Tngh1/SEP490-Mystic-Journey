using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private QuestDatabase questDatabase;

    public event Action<int> OnQuestProgressChanged;
    public event Action<int> OnQuestAccepted;
    public event Action<int> OnQuestClaimed;
    public event Action OnQuestsLoaded;

    private readonly Dictionary<int, PlayerQuestState> _cache = new();
    private readonly Dictionary<int, PlayerQuestResponse> _responses = new();
    private readonly Dictionary<int, int> _pendingBatch = new();
    private readonly Dictionary<int, PlayerQuestState> _snapshot = new();
    private int _batchVersion;
    private Coroutine _batchCoroutine;

    private const string OfflineQueueKey = "mj_quest_offline_queue";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (questDatabase != null)
            questDatabase.Initialize();
    }

    private void Start()
    {
        if (ApiClient.Instance.HasToken())
            LoadMyQuests();
    }

    private void OnApplicationQuit()
    {
        FlushOfflineQueue();
    }

    public PlayerQuestState GetQuestState(int questId)
    {
        _cache.TryGetValue(questId, out var state);
        return state;
    }

    public PlayerQuestResponse GetQuestResponse(int questId)
    {
        _responses.TryGetValue(questId, out var response);
        return response;
    }

    public List<PlayerQuestResponse> GetMainQuests()
    {
        return NormalizeMainQuests(_responses.Values);
    }

    public void LoadMainQuests(Action<List<PlayerQuestResponse>, PlayerQuestResponse> onSuccess, Action<string> onError = null)
    {
        if (!ApiClient.Instance.HasToken())
        {
            _responses.Clear();
            _cache.Clear();
            onSuccess?.Invoke(new List<PlayerQuestResponse>(), null);
            OnQuestsLoaded?.Invoke();
            return;
        }

        PlayerQuestApi.Instance.GetMyQuests(
            onSuccess: responses =>
            {
                HandleLoadedQuestResponses(responses);
                var mainQuests = GetMainQuests();
                onSuccess?.Invoke(mainQuests, PickPreferredQuest(mainQuests));
            },
            onError: err =>
            {
                Debug.LogError($"[QuestManager] LoadMainQuests FAIL: {err.Message}");
                ApplyOfflineQueue();
                onError?.Invoke(err.Message);
            }
        );
    }

    public void TalkToNpc(int npcId, Action<TalkToNpcResponse> onSuccess, Action<string> onError = null)
    {
        WorldApi.Instance.TalkToNpc(npcId, onSuccess,
            err => { Debug.LogError($"[QuestManager] TalkToNpc FAIL: {err.Message}"); onError?.Invoke(err.Message); });
    }

    public void TurnInQuestItem(int npcId, int questId, Action<TurnInQuestItemResponse> onSuccess, Action<string> onError = null)
    {
        WorldApi.Instance.TurnInQuestItem(npcId, questId,
            response =>
            {
                if (response?.Quest != null) UpsertQuestState(response.Quest);
                OnQuestProgressChanged?.Invoke(questId);
                onSuccess?.Invoke(response);
            },
            err => { Debug.LogError($"[QuestManager] TurnInQuestItem FAIL: {err.Message}"); onError?.Invoke(err.Message); });
    }

    public bool CanEnterMap(MapData map)
    {
        if (map.unlockQuestId <= 0) return true;
        var state = GetQuestState(map.unlockQuestId);
        return state != null && state.status == "Claimed";
    }

    public void LoadMyQuests()
    {
        if (!ApiClient.Instance.HasToken())
        {
            _cache.Clear();
            _responses.Clear();
            OnQuestsLoaded?.Invoke();
            return;
        }

        PlayerQuestApi.Instance.GetMyQuests(
            onSuccess: HandleLoadedQuestResponses,
            onError: err =>
            {
                Debug.LogError($"[QuestManager] LoadMyQuests FAIL: {err.Message}");
                ApplyOfflineQueue();
                if (_batchCoroutine != null) StopCoroutine(_batchCoroutine);
                _batchCoroutine = StartCoroutine(BatchSyncLoop());
            }
        );
    }

    public void AcceptQuest(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        if (_cache.TryGetValue(questId, out var localState) &&
            localState.status != "NotStarted" && localState.status != "Failed")
        {
            Debug.Log($"[QuestManager] AcceptQuest: questId={questId} already in cache, skipping.");
            onSuccess?.Invoke();
            return;
        }

        PlayerQuestApi.Instance.AcceptQuest(questId,
            onSuccess: response =>
            {
                if (response != null) UpsertQuestState(response);
                Debug.Log($"[QuestManager] Accepted questId={questId}");
                OnQuestAccepted?.Invoke(questId);
                onSuccess?.Invoke();
            },
            onError: err => 
            { 
                Debug.LogError($"[QuestManager] AcceptQuest FAIL: {err.Message}"); 
                PlayerQuestApi.Instance.GetMyQuests(
                    res => { HandleLoadedQuestResponses(res); },
                    reloadErr => { }
                );
                onError?.Invoke(err.Message); 
            });
    }

    public void GetQuestDetail(int questId, Action<PlayerQuestResponse> onSuccess, Action<string> onError = null)
    {
        PlayerQuestApi.Instance.GetQuestDetail(questId,
            onSuccess: response =>
            {
                if (response != null) UpsertQuestState(response);
                onSuccess?.Invoke(response);
            },
            onError: err => { Debug.LogError($"[QuestManager] GetQuestDetail FAIL: {err.Message}"); onError?.Invoke(err.Message); });
    }

    public void CompleteQuest(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        PlayerQuestApi.Instance.CompleteQuest(questId,
            onSuccess: response =>
            {
                if (response != null) UpsertQuestState(response);
                OnQuestProgressChanged?.Invoke(questId);
                onSuccess?.Invoke();
            },
            onError: err => { Debug.LogError($"[QuestManager] CompleteQuest FAIL: {err.Message}"); onError?.Invoke(err.Message); });
    }

    public void AddProgress(int questId, int amount = 1)
    {
        if (!_cache.TryGetValue(questId, out var state)) return;
        if (state.status != "InProgress") return;

        var quest = questDatabase != null ? questDatabase.GetById(questId) : null;
        var targetAmount = state.targetAmount > 0 ? state.targetAmount : (quest != null ? quest.targetAmount : 1);
        targetAmount = Mathf.Max(1, targetAmount);

        if (!_snapshot.ContainsKey(questId))
            _snapshot[questId] = state.Clone();

        state.targetAmount = targetAmount;
        state.progress = Mathf.Min(state.progress + amount, targetAmount);
        state.version++;
        state.isDirty = true;

        if (state.progress >= targetAmount)
            state.status = "Completed";

        _pendingBatch[questId] = state.progress;
        OnQuestProgressChanged?.Invoke(questId);
    }

    public void ClaimReward(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        if (!_cache.TryGetValue(questId, out var state)) return;
        if (state.status != "Completed") { onError?.Invoke("Quest chua Complete."); return; }

        PlayerQuestApi.Instance.ClaimReward(questId,
            onSuccess: response =>
            {
                if (response != null)
                    UpsertQuestState(response);
                else
                {
                    state.status = "Claimed";
                    state.isDirty = false;
                }

                _snapshot.Remove(questId);
                _pendingBatch.Remove(questId);

                var quest = questDatabase != null ? questDatabase.GetById(questId) : null;
                if (quest != null && quest.nextQuestId > 0)
                    AcceptQuest(quest.nextQuestId);

                Debug.Log($"[QuestManager] Claimed questId={questId}");
                OnQuestClaimed?.Invoke(questId);
                onSuccess?.Invoke();
            },
            onError: err => { Debug.LogError($"[QuestManager] ClaimReward FAIL: {err.Message}"); onError?.Invoke(err.Message); });
    }

    public (int completed, int total) GetMapProgress(MapData map)
    {
        if (questDatabase == null || map == null) return (0, 0);
        var chain = questDatabase.GetChain(map.firstQuestId);
        int total = chain.Count;
        int completed = chain.Count(q => _cache.TryGetValue(q.questId, out var s) && s.status == "Claimed");
        return (completed, total);
    }

    public IReadOnlyDictionary<int, PlayerQuestResponse> GetAllResponses() => _responses;

    public QuestData GetActiveQuestForCurrentMap()
    {
        return null;
    }

    public QuestData GetActiveQuestInChain(MapData map)
    {
        if (questDatabase == null || map == null) return null;
        var chain = questDatabase.GetChain(map.firstQuestId);
        foreach (var q in chain)
        {
            if (_cache.TryGetValue(q.questId, out var s) && s.status == "InProgress")
                return q;
        }
        return null;
    }

    private IEnumerator BatchSyncLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (_pendingBatch.Count == 0) continue;

            int sentVersion = ++_batchVersion;
            var toSync = _pendingBatch
                .Select(kv => new QuestProgressItem { QuestId = kv.Key, Progress = kv.Value })
                .ToList();
            var syncedKeys = toSync.Select(x => x.QuestId).ToList();
            _pendingBatch.Clear();

            PlayerQuestApi.Instance.BatchUpdateProgress(toSync,
                onSuccess: responses =>
                {
                    if (sentVersion < _batchVersion - 5)
                    {
                        Debug.Log($"[QuestManager] Stale batch response (v{sentVersion} < v{_batchVersion}), skip.");
                        return;
                    }
                    foreach (var key in syncedKeys)
                        _snapshot.Remove(key);
                    foreach (var r in responses)
                        if (r != null) UpsertQuestState(r);

                    // Auto-complete Collect/Defeat quests that reached target —
                    // player should not need to press Complete manually.
                    foreach (var r in responses)
                    {
                        if (r == null) continue;
                        var objectiveType = r.ObjectiveType ?? string.Empty;
                        var isAutoComplete =
                            string.Equals(objectiveType, "Collect",  StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Defeat",   StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Explore",  StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Interact", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "OpenChest",StringComparison.OrdinalIgnoreCase);

                        if (!isAutoComplete) continue;
                        if (!string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase)) continue;

                        var qid = r.QuestId;
                        Debug.Log($"[QuestManager] Auto-completing questId={qid} ({objectiveType})");
                        CompleteQuest(qid,
                            onSuccess: () =>
                            {
                                Debug.Log($"[QuestManager] Auto-complete done questId={qid}");
                                var qp = MainQuestPanelRuntime.Instance;
                                if (qp != null) qp.ShowQuestPopup("Quest completed!");

                                ClaimReward(qid,
                                    onSuccess: () =>
                                    {
                                        if (qp != null) qp.ShowQuestPopup("Reward claimed! Your next quest is ready.");
                                        WorldRuntimeEvents.RaiseQuestsChanged();
                                    },
                                    onError: err =>
                                    {
                                        Debug.LogWarning($"[QuestManager] Auto-claim fail questId={qid}: {err}");
                                        WorldRuntimeEvents.RaiseQuestsChanged();
                                    });
                            },
                            onError: err => Debug.LogWarning($"[QuestManager] Auto-complete fail questId={qid}: {err}"));
                    }
                },
                onError: err =>
                {
                    Debug.LogWarning($"[QuestManager] Batch FAIL (v{sentVersion}), rolling back: {err.Message}");
                    foreach (var key in syncedKeys)
                    {
                        if (_snapshot.TryGetValue(key, out var snap))
                        {
                            _cache[key] = snap;
                            _snapshot.Remove(key);
                        }
                    }
                    OnQuestProgressChanged?.Invoke(-1);
                });
        }
    }

    private void FlushOfflineQueue()
    {
        var dirty = _cache.Values
            .Where(s => s.isDirty || _pendingBatch.ContainsKey(s.questId))
            .Select(s => new OfflineQuestEntry
            {
                questId = s.questId,
                progress = _pendingBatch.TryGetValue(s.questId, out var p)
                    ? Mathf.Max(s.progress, p) : s.progress,
            })
            .ToList();

        if (dirty.Count > 0)
        {
            var json = JsonUtility.ToJson(new OfflineQueueWrapper { entries = dirty });
            PlayerPrefs.SetString(OfflineQueueKey, json);
            PlayerPrefs.Save();
            Debug.Log($"[QuestManager] Saved offline queue: {dirty.Count} entries.");
        }
    }

    private void ApplyOfflineQueue()
    {
        var json = PlayerPrefs.GetString(OfflineQueueKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var wrapper = JsonUtility.FromJson<OfflineQueueWrapper>(json);
            if (wrapper?.entries == null) return;

            foreach (var entry in wrapper.entries)
            {
                if (_cache.TryGetValue(entry.questId, out var state))
                {
                    if (entry.progress > state.progress)
                    {
                        state.progress = entry.progress;
                        _pendingBatch[entry.questId] = entry.progress;
                        state.isDirty = true;
                    }
                }
            }

            PlayerPrefs.DeleteKey(OfflineQueueKey);
            PlayerPrefs.Save();
            Debug.Log($"[QuestManager] Applied offline queue: {wrapper.entries.Count} entries.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[QuestManager] ApplyOfflineQueue parse error: {ex.Message}");
            PlayerPrefs.DeleteKey(OfflineQueueKey);
        }
    }

    private void HandleLoadedQuestResponses(List<PlayerQuestResponse> responses)
    {
        _cache.Clear();
        _responses.Clear();
        foreach (var response in responses ?? new List<PlayerQuestResponse>())
        {
            UpsertQuestState(response);
            
            var objectiveType = response.ObjectiveType ?? string.Empty;
            var isAutoComplete =
                string.Equals(objectiveType, "Collect",  StringComparison.OrdinalIgnoreCase) ||
                string.Equals(objectiveType, "Defeat",   StringComparison.OrdinalIgnoreCase) ||
                string.Equals(objectiveType, "Explore",  StringComparison.OrdinalIgnoreCase) ||
                string.Equals(objectiveType, "Interact", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(objectiveType, "OpenChest",StringComparison.OrdinalIgnoreCase);

            if (isAutoComplete && string.Equals(response.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                var qid = response.QuestId;
                ClaimReward(qid,
                    onSuccess: () =>
                    {
                        var qp = MainQuestPanelRuntime.Instance;
                        if (qp != null) qp.ShowQuestPopup("Reward claimed! Your next quest is ready.");
                        WorldRuntimeEvents.RaiseQuestsChanged();
                    },
                    onError: err => Debug.LogWarning($"[QuestManager] Auto-claim on load fail questId={qid}: {err}"));
            }
        }

        ApplyOfflineQueue();

        if (_batchCoroutine != null) StopCoroutine(_batchCoroutine);
        _batchCoroutine = StartCoroutine(BatchSyncLoop());

        Debug.Log($"[QuestManager] Loaded {_cache.Count} quests from server.");
        OnQuestsLoaded?.Invoke();
    }

    private void UpsertQuestState(PlayerQuestResponse response)
    {
        if (response == null) return;
        _responses[response.QuestId] = response;
        _cache[response.QuestId] = new PlayerQuestState
        {
            questId = response.QuestId,
            status = response.Status,
            progress = response.Progress,
            targetAmount = Mathf.Max(1, response.TargetAmount),
            version = 0,
            isDirty = false,
        };
    }

    public void AutoCompleteEquipSkillQuest()
    {
        foreach (var kvp in _responses)
        {
            var q = kvp.Value;
            if (QuestUtils.IsStatus(q, "InProgress") && string.Equals(q.ObjectiveType, "EquipSkill", StringComparison.OrdinalIgnoreCase))
            {
                CompleteQuest(q.QuestId,
                    onSuccess: () =>
                    {
                        var qp = MainQuestPanelRuntime.Instance;
                        if (qp != null) qp.ShowQuestPopup("Quest completed!");

                        ClaimReward(q.QuestId,
                            onSuccess: () =>
                            {
                                if (qp != null) qp.ShowQuestPopup("Reward claimed! Your next quest is ready.");
                                WorldRuntimeEvents.RaiseQuestsChanged();
                            },
                            onError: err => Debug.LogWarning($"[QuestManager] Auto-claim EquipSkill fail: {err}"));
                    },
                    onError: err => Debug.LogWarning($"[QuestManager] Auto-complete EquipSkill fail: {err}"));
            }
        }
    }

    // ── Static Utility Methods (delegated to QuestUtils) ─────────────────────────
    public static List<PlayerQuestResponse> NormalizeMainQuests(IEnumerable<PlayerQuestResponse> source)
        => QuestUtils.NormalizeMainQuests(source);

    public static PlayerQuestResponse PickPreferredQuest(IEnumerable<PlayerQuestResponse> source)
        => QuestUtils.PickPreferredQuest(source);

    public static PlayerQuestResponse FindSameQuest(IEnumerable<PlayerQuestResponse> source, PlayerQuestResponse target)
        => QuestUtils.FindSameQuest(source, target);

    public static bool IsMainQuest(PlayerQuestResponse quest)
        => QuestUtils.IsMainQuest(quest);

    public static bool IsStatus(PlayerQuestResponse quest, string status)
        => QuestUtils.IsStatus(quest, status);

    public static string StatusLabel(PlayerQuestResponse quest)
        => QuestUtils.StatusLabel(quest);

    public static string ObjectiveLine(PlayerQuestResponse quest)
        => QuestUtils.ObjectiveLine(quest);

    public static string RewardLine(PlayerQuestResponse quest)
        => QuestUtils.RewardLine(quest);
}

[Serializable]
public class PlayerQuestState
{
    public int questId;
    public string status;
    public int progress;
    public int targetAmount;
    public int version;
    public bool isDirty;

    public PlayerQuestState Clone() => new()
    {
        questId = questId,
        status = status,
        progress = progress,
        targetAmount = targetAmount,
        version = version,
        isDirty = isDirty,
    };
}

[Serializable]
public class OfflineQuestEntry
{
    public int questId;
    public int progress;
}

[Serializable]
public class OfflineQueueWrapper
{
    public List<OfflineQuestEntry> entries;
}
