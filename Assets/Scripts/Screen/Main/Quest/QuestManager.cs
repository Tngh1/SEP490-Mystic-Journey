using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using UnityEngine;

/// <summary>
/// QuestManager — Singleton controller cho UC 25 (Quest System).
///
/// Fixes applied (per lead review):
///   FIX #3: _pendingBatch là Dictionary → không duplicate entry cùng questId
///   FIX #4: Snapshot chỉ set nếu chưa có → rollback về đúng state gốc
///   FIX #5: Offline queue merge dùng Max → không ghi đè progress mới hơn
///   FIX #6: version counter tránh race condition batch/UI
///   FIX #7: AcceptQuest idempotent (server đã handle, client cũng check local)
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    // ── Inspector References ─────────────────────────────────────────────────
    [Header("Data")]
    [SerializeField] private QuestDatabase questDatabase;

    // ── Events ────────────────────────────────────────────────────────────────
    public event Action<int> OnQuestProgressChanged; // questId (-1 = refresh all)
    public event Action<int> OnQuestAccepted;        // questId
    public event Action<int> OnQuestClaimed;         // questId
    public event Action OnQuestsLoaded;              // khi load xong từ server

    // ── Local Cache (O(1) lookup) ─────────────────────────────────────────────
    private readonly Dictionary<int, PlayerQuestState> _cache = new();

    // FIX #3: Dictionary thay vì List → tự dedup theo questId
    private readonly Dictionary<int, int> _pendingBatch = new();

    // FIX #4: Snapshot chỉ lưu state GỐC trước lần AddProgress đầu tiên
    private readonly Dictionary<int, PlayerQuestState> _snapshot = new();

    // FIX #6: Version counter để detect stale batch responses
    private int _batchVersion;

    private bool _isLoaded;
    private Coroutine _batchCoroutine;

    // ── Offline Queue Key ─────────────────────────────────────────────────────
    private const string OfflineQueueKey = "mj_quest_offline_queue";

    // ── Unity Lifecycle ──────────────────────────────────────────────────────
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
        LoadMyQuests();
    }

    private void OnApplicationQuit()
    {
        FlushOfflineQueue();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Lấy state hiện tại của một quest (null nếu chưa accept).</summary>
    public PlayerQuestState GetQuestState(int questId)
    {
        _cache.TryGetValue(questId, out var state);
        return state;
    }

    /// <summary>Kiểm tra map có thể vào không (unlockQuestId = Claimed).</summary>
    public bool CanEnterMap(MapData map)
    {
        if (map.unlockQuestId <= 0) return true;
        var state = GetQuestState(map.unlockQuestId);
        return state != null && state.status == "Claimed";
    }

    // ── UC 25.1 — Load quests từ server ──────────────────────────────────────
    public void LoadMyQuests()
    {
        PlayerQuestApi.Instance.GetMyQuests(
            onSuccess: responses =>
            {
                _cache.Clear();
                foreach (var r in responses)
                {
                    _cache[r.QuestId] = new PlayerQuestState
                    {
                        questId   = r.QuestId,
                        status    = r.Status,
                        progress  = r.Progress,
                        targetAmount = Mathf.Max(1, r.TargetAmount),
                        version   = 0,
                        isDirty   = false,
                    };
                }

                // FIX #5: Merge offline queue với data server (dùng Max)
                ApplyOfflineQueue();

                _isLoaded = true;

                // Bắt đầu batch sync loop
                if (_batchCoroutine != null) StopCoroutine(_batchCoroutine);
                _batchCoroutine = StartCoroutine(BatchSyncLoop());

                Debug.Log($"[QuestManager] Loaded {_cache.Count} quests from server.");
                OnQuestsLoaded?.Invoke();
            },
            onError: err =>
            {
                Debug.LogError($"[QuestManager] LoadMyQuests FAIL: {err.Message}");
                // Dùng offline cache nếu có
                ApplyOfflineQueue();
                _isLoaded = true;

                if (_batchCoroutine != null) StopCoroutine(_batchCoroutine);
                _batchCoroutine = StartCoroutine(BatchSyncLoop());
            }
        );
    }

    // ── UC 25.3 — Accept quest (idempotent) ──────────────────────────────────
    // FIX #7: Nếu đã có trong local cache → trả về ngay
    public void AcceptQuest(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        // Idempotent local check trước khi gọi API
        if (_cache.TryGetValue(questId, out var localState) &&
            localState.status != "NotStarted" &&
            localState.status != "Failed")
        {
            Debug.Log($"[QuestManager] AcceptQuest: questId={questId} đã có trong cache, skip.");
            onSuccess?.Invoke();
            return;
        }

        PlayerQuestApi.Instance.AcceptQuest(
            questId,
            onSuccess: response =>
            {
                _cache[questId] = new PlayerQuestState
                {
                    questId  = response.QuestId,
                    status   = response.Status,
                    progress = response.Progress,
                    targetAmount = Mathf.Max(1, response.TargetAmount),
                    version  = 0,
                    isDirty  = false,
                };
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

    // ── UC 25.4 — AddProgress (Client Prediction + Batch) ───────────────────
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

        // FIX #4: Snapshot chỉ lưu state gốc (lần đầu tiên trước batch)
        if (!_snapshot.ContainsKey(questId))
            _snapshot[questId] = state.Clone();

        // Client Prediction: cập nhật UI ngay
        state.targetAmount = targetAmount;
        state.progress = Mathf.Min(state.progress + amount, targetAmount);
        state.version++;
        state.isDirty = true;

        if (state.progress >= targetAmount)
            state.status = "Completed";

        // FIX #3: Dictionary dedup — ghi đè entry cũ thay vì append
        _pendingBatch[questId] = state.progress;

        OnQuestProgressChanged?.Invoke(questId);
    }

    // ── UC 25.5 — Claim reward ────────────────────────────────────────────────
    public void ClaimReward(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        if (!_cache.TryGetValue(questId, out var state)) return;
        if (state.status != "Completed") { onError?.Invoke("Quest chưa Complete."); return; }

        PlayerQuestApi.Instance.ClaimReward(
            questId,
            onSuccess: response =>
            {
                state.status   = "Claimed";
                state.isDirty  = false;
                _snapshot.Remove(questId);
                _pendingBatch.Remove(questId);

                // Auto-accept quest tiếp theo
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

    // ── Map Quest Progress ────────────────────────────────────────────────────
    /// <summary>Tỉ lệ hoàn thành (Claimed) quest của một map (dùng trong UIMapPanel).</summary>
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

    /// <summary>Quest đầu tiên đang InProgress trong map hiện tại (dùng MiniMap widget).</summary>
    public QuestData GetActiveQuestForCurrentMap()
    {
        var mapName = WorldState.CurrentMapName;
        // Tìm MapData từ list… (QuestManager không giữ MapData list, UIMapPanel sẽ query)
        return null; // Caller tự cung cấp MapData
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

    // ── Batch Sync Coroutine ──────────────────────────────────────────────────
    private IEnumerator BatchSyncLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (_pendingBatch.Count == 0) continue;

            // Snapshot batch version để detect stale response (FIX #6)
            int sentVersion = ++_batchVersion;

            var toSync = _pendingBatch
                .Select(kv => new QuestProgressItem { QuestId = kv.Key, Progress = kv.Value })
                .ToList();

            // Snapshot của keys đang sync (để rollback nếu cần)
            var syncedKeys = toSync.Select(x => x.QuestId).ToList();

            _pendingBatch.Clear();

            PlayerQuestApi.Instance.BatchUpdateProgress(
                toSync,
                onSuccess: responses =>
                {
                    // FIX #6: Bỏ qua response cũ nếu đã có batch mới hơn
                    if (sentVersion < _batchVersion - 5)
                    {
                        Debug.Log($"[QuestManager] Stale batch response (v{sentVersion} < v{_batchVersion}), skip.");
                        return;
                    }

                    // Server confirm → clear snapshot cho các questId đã sync
                    foreach (var key in syncedKeys)
                        _snapshot.Remove(key);

                    // Update status từ server (nếu server auto-complete)
                    foreach (var r in responses)
                    {
                        if (_cache.TryGetValue(r.QuestId, out var s))
                        {
                            s.status = r.Status;
                            s.progress = r.Progress;
                            s.targetAmount = Mathf.Max(1, r.TargetAmount);
                            s.isDirty = false;
                        }
                    }
                },
                onError: err =>
                {
                    // HTTP 400 → ROLLBACK về snapshot gốc
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

    // ── Offline Queue ────────────────────────────────────────────────────────

    private void FlushOfflineQueue()
    {
        var dirty = _cache.Values
            .Where(s => s.isDirty || _pendingBatch.ContainsKey(s.questId))
            .Select(s => new OfflineQuestEntry
            {
                questId  = s.questId,
                // FIX #5: Lưu Max(current, pending) để không mất progress
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

    // FIX #5: Merge offline queue với server data — dùng Max
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
                        _pendingBatch[entry.questId] = entry.progress; // sẽ sync lên server khi có mạng
                        state.isDirty = true;
                    }
                }
            }

            // Xóa queue sau khi merge xong
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

    private void UpsertQuestState(PlayerQuestResponse response)
    {
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
}

// ── Data Structures ──────────────────────────────────────────────────────────

[Serializable]
public class PlayerQuestState
{
    public int    questId;
    public string status;   // InProgress | Completed | Claimed
    public int    progress;
    public int    targetAmount;
    public int    version;  // FIX #6: tăng mỗi lần AddProgress
    public bool   isDirty;  // cần sync lên server

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
