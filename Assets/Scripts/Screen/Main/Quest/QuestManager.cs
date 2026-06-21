using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

/// <summary>
/// QuestManager â€” Singleton controller cho UC 25 (Quest System).
///
/// Fixes applied (per lead review):
///   FIX #3: _pendingBatch lÃ  Dictionary â†’ khÃ´ng duplicate entry cÃ¹ng questId
///   FIX #4: Snapshot chá»‰ set náº¿u chÆ°a cÃ³ â†’ rollback vá» Ä‘Ãºng state gá»‘c
///   FIX #5: Offline queue merge dÃ¹ng Max â†’ khÃ´ng ghi Ä‘Ã¨ progress má»›i hÆ¡n
///   FIX #6: version counter trÃ¡nh race condition batch/UI
///   FIX #7: AcceptQuest idempotent (server Ä‘Ã£ handle, client cÅ©ng check local)
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    // â”€â”€ Inspector References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    [Header("Data")]
    [SerializeField] private QuestDatabase questDatabase;

    // â”€â”€ Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public event Action<int> OnQuestProgressChanged; // questId (-1 = refresh all)
    public event Action<int> OnQuestAccepted;        // questId
    public event Action<int> OnQuestClaimed;         // questId
    public event Action OnQuestsLoaded;              // khi load xong tá»« server

    // â”€â”€ Local Cache (O(1) lookup) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private readonly Dictionary<int, PlayerQuestState> _cache = new();
    private readonly Dictionary<int, PlayerQuestResponse> _responses = new();

    // FIX #3: Dictionary thay vÃ¬ List â†’ tá»± dedup theo questId
    private readonly Dictionary<int, int> _pendingBatch = new();

    // FIX #4: Snapshot chá»‰ lÆ°u state Gá»C trÆ°á»›c láº§n AddProgress Ä‘áº§u tiÃªn
    private readonly Dictionary<int, PlayerQuestState> _snapshot = new();

    // FIX #6: Version counter Ä‘á»ƒ detect stale batch responses
    private int _batchVersion;

    private bool _isLoaded;
    private Coroutine _batchCoroutine;

    // â”€â”€ Offline Queue Key â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string OfflineQueueKey = "mj_quest_offline_queue";

    // â”€â”€ Unity Lifecycle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Public API â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Láº¥y state hiá»‡n táº¡i cá»§a má»™t quest (null náº¿u chÆ°a accept).</summary>
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
                _isLoaded = true;
                onError?.Invoke(err.Message);
            }
        );
    }

    public void TalkToNpc(int npcId, Action<TalkToNpcResponse> onSuccess, Action<string> onError = null)
    {
        WorldApi.Instance.TalkToNpc(
            npcId,
            onSuccess,
            err =>
            {
                Debug.LogError($"[QuestManager] TalkToNpc FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            }
        );
    }


    public void TurnInQuestItem(int npcId, int questId, Action<TurnInQuestItemResponse> onSuccess, Action<string> onError = null)
    {
        WorldApi.Instance.TurnInQuestItem(
            npcId,
            questId,
            response =>
            {
                if (response?.Quest != null)
                    UpsertQuestState(response.Quest);

                OnQuestProgressChanged?.Invoke(questId);
                onSuccess?.Invoke(response);
            },
            err =>
            {
                Debug.LogError($"[QuestManager] TurnInQuestItem FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            }
        );
    }
    /// <summary>Kiá»ƒm tra map cÃ³ thá»ƒ vÃ o khÃ´ng (unlockQuestId = Claimed).</summary>
    public bool CanEnterMap(MapData map)
    {
        if (map.unlockQuestId <= 0) return true;
        var state = GetQuestState(map.unlockQuestId);
        return state != null && state.status == "Claimed";
    }

    // â”€â”€ UC 25.1 â€” Load quests tá»« server â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public void LoadMyQuests()
    {
        if (!ApiClient.Instance.HasToken())
        {
            _cache.Clear();
            _responses.Clear();
            _isLoaded = false;
            OnQuestsLoaded?.Invoke();
            return;
        }

        PlayerQuestApi.Instance.GetMyQuests(
            onSuccess: HandleLoadedQuestResponses,
            onError: err =>
            {
                Debug.LogError($"[QuestManager] LoadMyQuests FAIL: {err.Message}");
                // DÃ¹ng offline cache náº¿u cÃ³
                ApplyOfflineQueue();
                _isLoaded = true;

                if (_batchCoroutine != null) StopCoroutine(_batchCoroutine);
                _batchCoroutine = StartCoroutine(BatchSyncLoop());
            }
        );
    }
    // â”€â”€ UC 25.3 â€” Accept quest (idempotent) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // FIX #7: Náº¿u Ä‘Ã£ cÃ³ trong local cache â†’ tráº£ vá» ngay
    public void AcceptQuest(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        // Idempotent local check trÆ°á»›c khi gá»i API
        if (_cache.TryGetValue(questId, out var localState) &&
            localState.status != "NotStarted" &&
            localState.status != "Failed")
        {
            Debug.Log($"[QuestManager] AcceptQuest: questId={questId} Ä‘Ã£ cÃ³ trong cache, skip.");
            onSuccess?.Invoke();
            return;
        }

        PlayerQuestApi.Instance.AcceptQuest(
            questId,
            onSuccess: response =>
            {
                if (response != null)
                    UpsertQuestState(response);
                Debug.Log($"[QuestManager] Accepted questId={questId}");
                OnQuestAccepted?.Invoke(questId);
                onSuccess?.Invoke();
            },
            onError: err =>
            {
                Debug.LogError($"[QuestManager] AcceptQuest FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            }
        );
    }

    // â”€â”€ UC 25.4 â€” AddProgress (Client Prediction + Batch) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public void GetQuestDetail(int questId, Action<PlayerQuestResponse> onSuccess, Action<string> onError = null)
    {
        PlayerQuestApi.Instance.GetQuestDetail(
            questId,
            onSuccess: response =>
            {
                if (response != null)
                    UpsertQuestState(response);
                onSuccess?.Invoke(response);
            },
            onError: err =>
            {
                Debug.LogError($"[QuestManager] GetQuestDetail FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            }
        );
    }

    public void CompleteQuest(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        PlayerQuestApi.Instance.CompleteQuest(
            questId,
            onSuccess: response =>
            {
                if (response != null)
                    UpsertQuestState(response);
                OnQuestProgressChanged?.Invoke(questId);
                onSuccess?.Invoke();
            },
            onError: err =>
            {
                Debug.LogError($"[QuestManager] CompleteQuest FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            }
        );
    }

    public void AddProgress(int questId, int amount = 1)
    {
        if (!_cache.TryGetValue(questId, out var state)) return;
        if (state.status != "InProgress") return;

        var quest = questDatabase != null ? questDatabase.GetById(questId) : null;
        var targetAmount = state.targetAmount > 0 ? state.targetAmount : (quest != null ? quest.targetAmount : 1);
        targetAmount = Mathf.Max(1, targetAmount);

        // FIX #4: Snapshot chá»‰ lÆ°u state gá»‘c (láº§n Ä‘áº§u tiÃªn trÆ°á»›c batch)
        if (!_snapshot.ContainsKey(questId))
            _snapshot[questId] = state.Clone();

        // Client Prediction: cáº­p nháº­t UI ngay
        state.targetAmount = targetAmount;
        state.progress = Mathf.Min(state.progress + amount, targetAmount);
        state.version++;
        state.isDirty = true;

        if (state.progress >= targetAmount)
            state.status = "Completed";

        // FIX #3: Dictionary dedup â€” ghi Ä‘Ã¨ entry cÅ© thay vÃ¬ append
        _pendingBatch[questId] = state.progress;

        OnQuestProgressChanged?.Invoke(questId);
    }

    // â”€â”€ UC 25.5 â€” Claim reward â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public void ClaimReward(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        if (!_cache.TryGetValue(questId, out var state)) return;
        if (state.status != "Completed") { onError?.Invoke("Quest chÆ°a Complete."); return; }

        PlayerQuestApi.Instance.ClaimReward(
            questId,
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

                // Auto-accept quest tiáº¿p theo
                var quest = questDatabase != null ? questDatabase.GetById(questId) : null;
                if (quest != null && quest.nextQuestId > 0)
                    AcceptQuest(quest.nextQuestId);

                Debug.Log($"[QuestManager] Claimed questId={questId}");
                OnQuestClaimed?.Invoke(questId);
                onSuccess?.Invoke();
            },
            onError: err =>
            {
                Debug.LogError($"[QuestManager] ClaimReward FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            }
        );
    }

    // â”€â”€ Map Quest Progress â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    public static List<PlayerQuestResponse> NormalizeMainQuests(IEnumerable<PlayerQuestResponse> source)
    {
        return (source ?? Enumerable.Empty<PlayerQuestResponse>())
            .Where(IsMainQuest)
            .OrderBy(QuestStatusPriority)
            .ThenBy(q => q.RequiredLevel)
            .ThenBy(q => q.QuestId)
            .ToList();
    }

    public static PlayerQuestResponse PickPreferredQuest(IEnumerable<PlayerQuestResponse> source)
    {
        var quests = source?.ToList() ?? new List<PlayerQuestResponse>();
        return quests.FirstOrDefault(q => IsStatus(q, "InProgress"))
               ?? quests.FirstOrDefault(q => IsStatus(q, "Completed"))
               ?? quests.FirstOrDefault(q => IsStatus(q, "NotStarted"))
               ?? quests.FirstOrDefault();
    }

    public static PlayerQuestResponse FindSameQuest(IEnumerable<PlayerQuestResponse> source, PlayerQuestResponse target)
    {
        if (target == null)
            return null;

        return source?.FirstOrDefault(q => q != null && q.QuestId == target.QuestId);
    }

    public static bool IsMainQuest(PlayerQuestResponse quest)
    {
        if (quest == null)
            return false;

        if (string.IsNullOrWhiteSpace(quest.QuestType))
            return true;

        var normalized = quest.QuestType.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        return string.Equals(normalized, "Main", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "MainQuest", StringComparison.OrdinalIgnoreCase) ||
               normalized.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool IsStatus(PlayerQuestResponse quest, string status)
    {
        return quest != null && string.Equals(quest.Status, status, StringComparison.OrdinalIgnoreCase);
    }

    private static int QuestStatusPriority(PlayerQuestResponse quest)
    {
        if (IsStatus(quest, "InProgress"))
            return 0;
        if (IsStatus(quest, "Completed"))
            return 1;
        if (IsStatus(quest, "NotStarted"))
            return 2;
        if (IsStatus(quest, "Claimed"))
            return 3;
        return 4;
    }

    /// <summary>Tá»‰ lá»‡ hoÃ n thÃ nh (Claimed) quest cá»§a má»™t map (dÃ¹ng trong UIMapPanel).</summary>
    public (int completed, int total) GetMapProgress(MapData map)
    {
        if (questDatabase == null || map == null)
            return (0, 0);

        var chain = questDatabase.GetChain(map.firstQuestId);
        int total     = chain.Count;
        int completed = chain.Count(q =>
            _cache.TryGetValue(q.questId, out var s) && s.status == "Claimed");
        return (completed, total);
    }

    /// <summary>Quest Ä‘áº§u tiÃªn Ä‘ang InProgress trong map hiá»‡n táº¡i (dÃ¹ng MiniMap widget).</summary>
    public QuestData GetActiveQuestForCurrentMap()
    {
        var mapName = WorldState.CurrentMapName;
        // TÃ¬m MapData tá»« listâ€¦ (QuestManager khÃ´ng giá»¯ MapData list, UIMapPanel sáº½ query)
        return null; // Caller tá»± cung cáº¥p MapData
    }

    public QuestData GetActiveQuestInChain(MapData map)
    {
        if (questDatabase == null || map == null)
            return null;

        var chain = questDatabase.GetChain(map.firstQuestId);
        foreach (var q in chain)
        {
            if (_cache.TryGetValue(q.questId, out var s) && s.status == "InProgress")
                return q;
        }
        return null;
    }

    // â”€â”€ Batch Sync Coroutine â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private IEnumerator BatchSyncLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (_pendingBatch.Count == 0) continue;

            // Snapshot batch version Ä‘á»ƒ detect stale response (FIX #6)
            int sentVersion = ++_batchVersion;

            var toSync = _pendingBatch
                .Select(kv => new QuestProgressItem { QuestId = kv.Key, Progress = kv.Value })
                .ToList();

            // Snapshot cá»§a keys Ä‘ang sync (Ä‘á»ƒ rollback náº¿u cáº§n)
            var syncedKeys = toSync.Select(x => x.QuestId).ToList();

            _pendingBatch.Clear();

            PlayerQuestApi.Instance.BatchUpdateProgress(
                toSync,
                onSuccess: responses =>
                {
                    // FIX #6: Bá» qua response cÅ© náº¿u Ä‘Ã£ cÃ³ batch má»›i hÆ¡n
                    if (sentVersion < _batchVersion - 5)
                    {
                        Debug.Log($"[QuestManager] Stale batch response (v{sentVersion} < v{_batchVersion}), skip.");
                        return;
                    }

                    // Server confirm â†’ clear snapshot cho cÃ¡c questId Ä‘Ã£ sync
                    foreach (var key in syncedKeys)
                        _snapshot.Remove(key);

                    // Update status tá»« server (náº¿u server auto-complete)
                    foreach (var r in responses)
                    {
                        if (r != null)
                            UpsertQuestState(r);
                    }
                },
                onError: err =>
                {
                    // HTTP 400 â†’ ROLLBACK vá» snapshot gá»‘c
                    Debug.LogWarning($"[QuestManager] Batch FAIL (v{sentVersion}), rolling back: {err.Message}");
                    foreach (var key in syncedKeys)
                    {
                        if (_snapshot.TryGetValue(key, out var snap))
                        {
                            _cache[key] = snap;
                            _snapshot.Remove(key);
                        }
                    }
                    OnQuestProgressChanged?.Invoke(-1); // refresh all UI
                }
            );
        }
    }

    // â”€â”€ Offline Queue â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void FlushOfflineQueue()
    {
        var dirty = _cache.Values
            .Where(s => s.isDirty || _pendingBatch.ContainsKey(s.questId))
            .Select(s => new OfflineQuestEntry
            {
                questId  = s.questId,
                // FIX #5: LÆ°u Max(current, pending) Ä‘á»ƒ khÃ´ng máº¥t progress
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

    // FIX #5: Merge offline queue vá»›i server data â€” dÃ¹ng Max
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
                    // FIX #5: Max(serverProgress, offlineProgress)
                    if (entry.progress > state.progress)
                    {
                        state.progress = entry.progress;
                        _pendingBatch[entry.questId] = entry.progress; // sáº½ sync lÃªn server khi cÃ³ máº¡ng
                        state.isDirty = true;
                    }
                }
            }

            // XÃ³a queue sau khi merge xong
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
            UpsertQuestState(response);

        // FIX #5: Merge offline queue vá»›i data server (dÃ¹ng Max)
        ApplyOfflineQueue();

        _isLoaded = true;

        // Báº¯t Ä‘áº§u batch sync loop
        if (_batchCoroutine != null) StopCoroutine(_batchCoroutine);
        _batchCoroutine = StartCoroutine(BatchSyncLoop());

        Debug.Log($"[QuestManager] Loaded {_cache.Count} quests from server.");
        OnQuestsLoaded?.Invoke();
    }

    private void UpsertQuestState(PlayerQuestResponse response)
    {
        if (response == null)
            return;

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
    }}

// â”€â”€ Data Structures â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

[Serializable]
public class PlayerQuestState
{
    public int    questId;
    public string status;   // InProgress | Completed | Claimed
    public int    progress;
    public int    targetAmount;
    public int    version;  // FIX #6: tÄƒng má»—i láº§n AddProgress
    public bool   isDirty;  // cáº§n sync lÃªn server

    public PlayerQuestState Clone() => new()
    {
        questId  = questId,
        status   = status,
        progress = progress,
        targetAmount = targetAmount,
        version  = version,
        isDirty  = isDirty,
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





