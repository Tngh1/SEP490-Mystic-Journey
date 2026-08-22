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

// Executes core business logic for mono behaviour.
public class QuestUIManager : MonoBehaviour
{
    // Executes core business logic for instance.
    public static QuestUIManager Instance { get; private set; }

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

    private readonly Dictionary<int, float> _completing = new();
    private readonly Dictionary<int, float> _claiming = new();
    private const float InFlightTimeout = ApiConfig.Timeout + 5f;

    // Executes core business logic for try begin in flight.
    // Returns a boolean indicating operation success.
    private static bool TryBeginInFlight(Dictionary<int, float> inFlight, int questId)
    {
        if (inFlight.TryGetValue(questId, out var startedAt) &&
            Time.realtimeSinceStartup - startedAt < InFlightTimeout)
            return false;

        inFlight[questId] = Time.realtimeSinceStartup;
        return true;
    }
    private int _questLoadGeneration;
    private int _batchVersion;
    private Coroutine _batchCoroutine;

    private readonly HashSet<int> _silentClaimRefetched = new();

    private const string OfflineQueueKey = "mj_quest_offline_queue";

    // Initializes singleton instance, persists across scene loads, and loads local quest database.
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; } // Prevent duplicate manager
        Instance = this;
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject); // Keep alive across scene transitions
        if (questDatabase != null)
            questDatabase.Initialize(); // Cache quest templates and storyline prerequisites
    }

    // Performs initial quest synchronization on game launch if authenticated.
    private void Start()
    {
        if (ApiClient.Instance.HasToken())
            LoadMyQuests(); // Fetch active and available quest list from backend
    }

    // Flushes pending quest progress mutations to disk on game exit.
    private void OnApplicationQuit()
    {
        FlushOfflineQueue(); // Persist unsent progress to PlayerPrefs cache
    }

    // Queries active state (NotStarted, InProgress, Completed, Claimed) for a quest ID.
    public PlayerQuestState GetQuestState(int questId)
    {
        _cache.TryGetValue(questId, out var state);
        return state;
    }

    // Retrieves full quest DTO model (objectives, reward counts) by quest ID.
    public PlayerQuestResponse GetQuestResponse(int questId)
    {
        _responses.TryGetValue(questId, out var response);
        return response;
    }

    // Filters and orders the list of active main storyline quests.
    public List<PlayerQuestResponse> GetMainQuests()
    {
        return NormalizeMainQuests(_responses.Values); // Extract main storyline quests
    }

    // Loads main quests from backend with generation tracking to prevent race conditions.
    public void LoadMainQuests(Action<List<PlayerQuestResponse>, PlayerQuestResponse> onSuccess, Action<string> onError = null)
    {
        if (!ApiClient.Instance.HasToken())
        {
            _responses.Clear();
            _cache.Clear();
            onSuccess?.Invoke(new List<PlayerQuestResponse>(), null);
            OnQuestsLoaded?.Invoke();
            return; // Exit early if unauthenticated
        }

        int generation = ++_questLoadGeneration; // Increment sequence counter
        PlayerQuestApi.Instance.GetMyQuests(
            onSuccess: responses =>
            {
                if (generation != _questLoadGeneration)
                {
                    Debug.Log($"[QuestUIManager] Ignoring stale LoadMainQuests response generation={generation}, latest={_questLoadGeneration}.");
                    return; // Ignore stale asynchronous response
                }

                HandleLoadedQuestResponses(responses); // Update cache and dispatch UI events
                var mainQuests = GetMainQuests();
                onSuccess?.Invoke(mainQuests, PickPreferredQuest(mainQuests)); // Return filtered list and active quest
            },
            onError: err =>
            {
                if (generation != _questLoadGeneration) return;
                Debug.LogError($"[QuestUIManager] LoadMainQuests FAIL: {err.Message}");
                ApplyOfflineQueue(); // Apply cached offline progress if request fails
                onError?.Invoke(err.Message);
            }
        );
    }

    // Triggers dialogue interaction with an NPC in the active zone.
    public void TalkToNpc(int npcId, Action<TalkToNpcResponse> onSuccess, Action<string> onError = null)
    {
        WorldApi.Instance.TalkToNpc(npcId, onSuccess,
            err => { Debug.LogError($"[QuestUIManager] TalkToNpc FAIL: {err.Message}"); onError?.Invoke(err.Message); }); // POST /api/world/npc/talk
    }

    // Executes core business logic for turn in quest item.
    public void TurnInQuestItem(int npcId, int questId, Action<TurnInQuestItemResponse> onSuccess, Action<string> onError = null)
    {
        WorldApi.Instance.TurnInQuestItem(npcId, questId,
            response =>
            {
                if (response?.Quest != null) UpsertQuestState(response.Quest);
                if (response?.Success == true && response.ConsumedQuantity > 0)
                    InventoryUIManager.RefreshAny(refreshStats: false);
                OnQuestProgressChanged?.Invoke(questId);
                onSuccess?.Invoke(response);
            },
            err => { Debug.LogError($"[QuestUIManager] TurnInQuestItem FAIL: {err.Message}"); onError?.Invoke(err.Message); });
    }

    // Executes core business logic for can enter map.
    // Logic details: validates numeric boundary constraints.
    // Returns a boolean indicating operation success.
    public bool CanEnterMap(MapData map)
    {
        if (map == null || map.unlockQuestId <= 0) return true;
        var state = GetQuestState(map.unlockQuestId);
        return state != null && string.Equals(state.status, "Claimed", StringComparison.OrdinalIgnoreCase);
    }

    // Executes core business logic for load my quests.
    public void LoadMyQuests(Action onSuccess = null, Action<string> onError = null)
    {
        if (!ApiClient.Instance.HasToken())
        {
            _cache.Clear();
            _responses.Clear();
            OnQuestsLoaded?.Invoke();
            onSuccess?.Invoke();
            return;
        }

        int generation = ++_questLoadGeneration;
        PlayerQuestApi.Instance.GetMyQuests(
            onSuccess: responses =>
            {
                if (generation != _questLoadGeneration)
                {
                    const string message = "Quest load was superseded by a newer request.";
                    Debug.Log($"[QuestUIManager] Ignoring stale LoadMyQuests response generation={generation}, latest={_questLoadGeneration}.");
                    onError?.Invoke(message);
                    return;
                }

                HandleLoadedQuestResponses(responses);
                onSuccess?.Invoke();
            },
            onError: err =>
            {
                if (generation != _questLoadGeneration)
                {
                    onError?.Invoke("Quest load was superseded by a newer request.");
                    return;
                }

                Debug.LogError($"[QuestUIManager] LoadMyQuests FAIL: {err.Message}");
                ApplyOfflineQueue();
                if (_batchCoroutine != null) StopCoroutine(_batchCoroutine);
                // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
                _batchCoroutine = StartCoroutine(BatchSyncLoop());
                onError?.Invoke(err.Message);
            }
        );
    }

    // Executes core business logic for accept quest.
    // Logic details: validates required non-empty string arguments.
    public void AcceptQuest(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        if (_cache.TryGetValue(questId, out var existingState) &&
            existingState.status != "NotStarted" &&
            existingState.status != "Failed" &&
            existingState.status != null)
        {
            Debug.Log($"[QuestUIManager] AcceptQuest: questId={questId} already {existingState.status}, skipping.");
            onSuccess?.Invoke();
            return;
        }

        PlayerQuestApi.Instance.AcceptQuest(questId,
            onSuccess: response =>
            {
                if (response != null)
                {
                    UpsertQuestState(response);
                }
                else if (_cache.TryGetValue(questId, out var cached))
                {
                    cached.status = "InProgress";
                    cached.isDirty = false;
                }

                Debug.Log($"[QuestUIManager] Accepted questId={questId} -> InProgress");
                MysticJourney.Features.Quest.QuestWaypointManager.IsTrackingEnabled = true;

                string qTitle = response?.QuestTitle ?? GetQuestTitle(questId);
                if (MainQuestPanelRuntime.Instance != null && !string.IsNullOrWhiteSpace(qTitle))
                    MainQuestPanelRuntime.Instance.ShowPaperPopup(qTitle, UIPaperPopupView.PaperPopupKind.Accepted);

                OnQuestAccepted?.Invoke(questId);
                WorldRuntimeEvents.RaiseQuestsChanged();
                onSuccess?.Invoke();
            },
            onError: err =>
            {
                Debug.LogWarning($"[QuestUIManager] AcceptQuest FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            });

    }

    // Executes core business logic for get quest detail.
    public void GetQuestDetail(int questId, Action<PlayerQuestResponse> onSuccess, Action<string> onError = null)
    {
        PlayerQuestApi.Instance.GetQuestDetail(questId,
            onSuccess: response =>
            {
                if (response != null) UpsertQuestState(response);
                onSuccess?.Invoke(response);
            },
            onError: err => { Debug.LogError($"[QuestUIManager] GetQuestDetail FAIL: {err.Message}"); onError?.Invoke(err.Message); });
    }

    // Executes core business logic for complete quest.
    public void CompleteQuest(int questId, Action onSuccess = null, Action<string> onError = null)
    {
        if (!TryBeginInFlight(_completing, questId))
        {
            Debug.Log($"[QuestUIManager] CompleteQuest: questId={questId} already in-flight, skipping.");
            return;
        }

        PlayerQuestApi.Instance.CompleteQuest(questId,
            onSuccess: response =>
            {
                _completing.Remove(questId);
                if (response != null) UpsertQuestState(response);
                InventoryUIManager.RefreshAny(refreshStats: false);

                OnQuestProgressChanged?.Invoke(questId);
                onSuccess?.Invoke();
            },
            onError: err =>
            {
                _completing.Remove(questId);
                Debug.LogError($"[QuestUIManager] CompleteQuest FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            });
    }

    // Executes core business logic for add progress.
    public void AddProgress(int questId, int amount = 1)
    {
        if (!_cache.TryGetValue(questId, out var state)) return;
        if (state.status != "InProgress") return;

        var response = GetQuestResponse(questId);
        var isCollect = string.Equals(response?.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase);
        var quest = questDatabase != null ? questDatabase.GetById(questId) : null;
        var targetAmount = state.targetAmount > 0 ? state.targetAmount : (quest != null ? quest.targetAmount : 1);
        targetAmount = Mathf.Max(1, targetAmount);

        if (!isCollect && !_snapshot.ContainsKey(questId))
            _snapshot[questId] = state.Clone();

        state.targetAmount = targetAmount;
        state.progress = Mathf.Min(state.progress + amount, targetAmount);
        state.version++;
        state.isDirty = !isCollect;

        MirrorProgressToResponse(questId, state.progress, targetAmount);

        if (!isCollect)
            _pendingBatch[questId] = state.progress;
        OnQuestProgressChanged?.Invoke(questId);
    }

    // Executes core business logic for mirror progress to response.
    // Logic details: validates required non-empty string arguments.
    private void MirrorProgressToResponse(int questId, int progress, int targetAmount)
    {
        if (!_responses.TryGetValue(questId, out var response) || response == null) return;
        response.TargetAmount = targetAmount;
        if (progress > response.Progress) response.Progress = progress;
    }

    // Executes core business logic for claim reward.
    // Logic details: validates required non-empty string arguments.
    public void ClaimReward(int questId, Action onSuccess = null, Action<string> onError = null, bool silent = false)
    {
        if (!_cache.TryGetValue(questId, out var state)) { onError?.Invoke("Quest not found."); return; }
        if (state.status != "Completed") { onError?.Invoke("Quest chua Complete."); return; }

        if (!TryBeginInFlight(_claiming, questId))
        {
            Debug.Log($"[QuestUIManager] ClaimReward: questId={questId} already in-flight, skipping.");
            return;
        }

        PlayerQuestApi.Instance.ClaimReward(questId,
            onSuccess: response =>
            {
                _claiming.Remove(questId);
                if (response != null)
                    UpsertQuestState(response);
                else
                {
                    state.status = "Claimed";
                    state.isDirty = false;
                }

                _snapshot.Remove(questId);
                _pendingBatch.Remove(questId);
                if (!silent || _silentClaimRefetched.Add(questId))
                    LoadMyQuests();


                Debug.Log($"[QuestUIManager] Claimed questId={questId}");
                InventoryUIManager.RefreshAny(refreshStats: false);
                WorldRuntimeEvents.RaiseCurrencyChanged();

                if (!silent)
                {
                    string qTitle = response?.QuestTitle ?? GetQuestTitle(questId);
                    if (MainQuestPanelRuntime.Instance != null && !string.IsNullOrWhiteSpace(qTitle))
                        MainQuestPanelRuntime.Instance.ShowPaperPopup(qTitle, UIPaperPopupView.PaperPopupKind.Claimed);
                }

                OnQuestClaimed?.Invoke(questId);


                WorldRuntimeEvents.RaiseQuestsChanged();

                WorldRuntimeEvents.RaiseMapCompleted(questId);

                onSuccess?.Invoke();
            },
            onError: err =>
            {
                _claiming.Remove(questId);
                Debug.LogError($"[QuestUIManager] ClaimReward FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            });
    }

    // Executes core business logic for public.
    public (int completed, int total) GetMapProgress(MapData map)
    {
        if (questDatabase == null || map == null) return (0, 0);
        var chain = questDatabase.GetChain(map.firstQuestId);
        int total = chain.Count;
        int completed = chain.Count(q => _cache.TryGetValue(q.questId, out var s) && s.status == "Claimed");
        return (completed, total);
    }

    // Executes core business logic for get all responses.
    public IReadOnlyDictionary<int, PlayerQuestResponse> GetAllResponses() => _responses;

    // Executes core business logic for get active quest for current map.
    public QuestData GetActiveQuestForCurrentMap()
    {
        return null;
    }

    // Executes core business logic for get active quest in chain.
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

    // Executes core business logic for batch sync loop.
    private IEnumerator BatchSyncLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            PushPendingBatch();
        }
    }

    // Executes core business logic for flush pending progress now.
    public void FlushPendingProgressNow() => PushPendingBatch();

    // Snapshot pending quest progress, send one batch update, merge non-stale responses into the cache, auto-complete eligible objectives, and restore snapshots when the request fails.
    private void PushPendingBatch()
    {
        {
            if (_pendingBatch.Count == 0) return;

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
                        Debug.Log($"[QuestUIManager] Stale batch response (v{sentVersion} < v{_batchVersion}), skip.");
                        return;
                    }
                    foreach (var key in syncedKeys)
                        _snapshot.Remove(key);
                    foreach (var r in responses)
                        if (r != null) UpsertQuestState(r);

                    if (responses != null && responses.Count > 0)
                        OnQuestProgressChanged?.Invoke(-1);

                    foreach (var r in responses)
                    {
                        if (r == null) continue;
                        // Supported quest objectives: Explore, Defeat, Collect, Talk, OpenChest, Interact, EquipSkill, or Kill; the value selects progress-tracking behavior.
                        var objectiveType = r.ObjectiveType ?? string.Empty;
                        var isAutoComplete =
                            string.Equals(objectiveType, "Defeat",    StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Kill",      StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Explore",   StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Interact",  StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "OpenChest", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Talk",      StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "EquipSkill",StringComparison.OrdinalIgnoreCase);


                        if (!isAutoComplete) continue;

                        bool isFinished = string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                                          (r.Progress >= Mathf.Max(1, r.TargetAmount));

                        if (!isFinished) continue;

                        var qid = r.QuestId;
                        Debug.Log($"[QuestUIManager] Auto-completing questId={qid} ({objectiveType})");
                        CompleteQuest(qid,
                            onSuccess: () =>
                            {
                                Debug.Log($"[QuestUIManager] Auto-complete done questId={qid}");

                                ClaimReward(qid,
                                    onSuccess: () => WorldRuntimeEvents.RaiseQuestsChanged(),
                                    onError: err =>
                                    {
                                        Debug.LogWarning($"[QuestUIManager] Auto-claim fail questId={qid}: {err}");
                                        WorldRuntimeEvents.RaiseQuestsChanged();
                                    });
                            },
                            onError: err => Debug.LogWarning($"[QuestUIManager] Auto-complete fail questId={qid}: {err}"));
                    }
                },
                onError: err =>
                {
                    Debug.LogWarning($"[QuestUIManager] Batch FAIL (v{sentVersion}), rolling back: {err.Message}");
                    foreach (var key in syncedKeys)
                    {
                        if (_snapshot.TryGetValue(key, out var snap))
                        {
                            _cache[key] = snap;
                            if (_responses.TryGetValue(key, out var resp) && resp != null)
                                resp.Progress = snap.progress;
                            _snapshot.Remove(key);
                        }
                    }
                    OnQuestProgressChanged?.Invoke(-1);
                });
        }
    }

    // Collect dirty quest progress, merge pending values with cached progress, serialize the entries, and persist them in PlayerPrefs for a later reconnect.
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
            Debug.Log($"[QuestUIManager] Saved offline queue: {dirty.Count} entries.");
        }
    }

    // Load saved offline quest progress, merge each entry into the runtime cache and pending batch, then delete the persisted queue after successful parsing.
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
                var response = GetQuestResponse(entry.questId);
                if (string.Equals(response?.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase))
                    continue;

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
            Debug.Log($"[QuestUIManager] Applied offline queue: {wrapper.entries.Count} entries.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[QuestUIManager] ApplyOfflineQueue parse error: {ex.Message}");
            PlayerPrefs.DeleteKey(OfflineQueueKey);
        }
    }

    // Executes core business logic for handle loaded quest responses.
    private void HandleLoadedQuestResponses(List<PlayerQuestResponse> responses)
    {
        var oldFinishedQuests = _responses.Values
            .Where(q => string.Equals(q.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(q.Status, "Claimed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _cache.Clear();
        _responses.Clear();
        _pendingBatch.Clear();
        _snapshot.Clear();
        foreach (var response in responses ?? new List<PlayerQuestResponse>())
        {
            if (string.Equals(response.Status, "InProgress", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase)
                && response.Progress < Mathf.Max(1, response.TargetAmount))
                response.Progress = 0;

            UpsertQuestState(response);

            // Supported quest objectives: Explore, Defeat, Collect, Talk, OpenChest, Interact, EquipSkill, or Kill; the value selects progress-tracking behavior.
            var objectiveType = response.ObjectiveType ?? string.Empty;
            var qid = response.QuestId;
            bool isFinished = string.Equals(response.Status, "Completed", StringComparison.OrdinalIgnoreCase);
            bool canComplete = string.Equals(response.Status, "InProgress", StringComparison.OrdinalIgnoreCase) &&
                               response.Progress >= Mathf.Max(1, response.TargetAmount);

            if (isFinished && !string.Equals(objectiveType, "Collect", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[QuestUIManager] Auto-claiming completed questId={qid}");
                int claimId = qid;
                ClaimReward(claimId,
                    onSuccess: () => { },
                    onError: err => Debug.LogWarning($"[QuestUIManager] Auto-claim on load fail questId={claimId}: {err}"),
                    silent: true);
            }
            else if (canComplete && !string.Equals(objectiveType, "Collect", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[QuestUIManager] Auto-completing loaded questId={qid}");
                int completeId = qid;
                CompleteQuest(completeId,
                    onSuccess: () =>
                    {
                        ClaimReward(completeId,
                            onSuccess: () => { },
                            onError: err => Debug.LogWarning($"[QuestUIManager] Auto-claim on load fail questId={completeId}: {err}"),
                            silent: true);
                    },
                    onError: err => Debug.LogWarning($"[QuestUIManager] Auto-complete on load fail questId={completeId}: {err}"));
            }
        }




        foreach (var oldQuest in oldFinishedQuests)
        {
            if (!_responses.ContainsKey(oldQuest.QuestId))
            {
                UpsertQuestState(oldQuest);
            }
        }

        ApplyOfflineQueue();

        if (_batchCoroutine != null) StopCoroutine(_batchCoroutine);
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        _batchCoroutine = StartCoroutine(BatchSyncLoop());

        Debug.Log($"[QuestUIManager] Loaded {_cache.Count} quests from server.");
        OnQuestsLoaded?.Invoke();
        WorldRuntimeEvents.RaiseQuestsChanged();
    }

    // Executes core business logic for apply server quest state.
    public void ApplyServerQuestState(PlayerQuestResponse response)
    {
        if (response == null) return;
        UpsertQuestState(response);
        _pendingBatch.Remove(response.QuestId);
        OnQuestProgressChanged?.Invoke(response.QuestId);

        if (QuestUtils.IsStatus(response, "Completed")
            && !string.Equals(response.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase))
        {
            int claimId = response.QuestId;
            ClaimReward(claimId,
                onError: err => Debug.LogWarning($"[QuestUIManager] Auto-claim fail questId={claimId}: {err}"));
        }
    }

    // Executes core business logic for upsert quest state.
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

    // Executes core business logic for auto complete equip skill quest.
    public void AutoCompleteEquipSkillQuest()
    {
        foreach (var q in _responses.Values.ToList())
        {
            if (QuestUtils.IsStatus(q, "InProgress") && string.Equals(q.ObjectiveType, "EquipSkill", StringComparison.OrdinalIgnoreCase))
            {
                CompleteQuest(q.QuestId,
                    onSuccess: () =>
                    {
                        ClaimReward(q.QuestId,
                            onSuccess: () => WorldRuntimeEvents.RaiseQuestsChanged(),
                            onError: err => Debug.LogWarning($"[QuestUIManager] Auto-claim EquipSkill fail: {err}"));
                    },
                    onError: err => Debug.LogWarning($"[QuestUIManager] Auto-complete EquipSkill fail: {err}"));
            }
        }
    }

    // Executes core business logic for normalize main quests.
    // Logic details: validates required non-empty string arguments.
    public static List<PlayerQuestResponse> NormalizeMainQuests(IEnumerable<PlayerQuestResponse> source)
        => QuestUtils.NormalizeMainQuests(source);

    // Executes core business logic for pick preferred quest.
    // Logic details: validates required non-empty string arguments.
    public static PlayerQuestResponse PickPreferredQuest(IEnumerable<PlayerQuestResponse> source)
        => QuestUtils.PickPreferredQuest(source);

    // Executes core business logic for find same quest.
    // Logic details: validates required non-empty string arguments.
    public static PlayerQuestResponse FindSameQuest(IEnumerable<PlayerQuestResponse> source, PlayerQuestResponse target)
        => QuestUtils.FindSameQuest(source, target);

    // Executes core business logic for is main quest.
    // Logic details: validates required non-empty string arguments.
    // Returns a boolean indicating operation success.
    public static bool IsMainQuest(PlayerQuestResponse quest)
        => QuestUtils.IsMainQuest(quest);

    // Executes core business logic for is status.
    // Logic details: validates required non-empty string arguments.
    // Returns a boolean indicating operation success.
    public static bool IsStatus(PlayerQuestResponse quest, string status)
        => QuestUtils.IsStatus(quest, status);

    // Executes core business logic for status label.
    // Logic details: validates required non-empty string arguments.
    public static string StatusLabel(PlayerQuestResponse quest)
        => QuestUtils.StatusLabel(quest);

    // Executes core business logic for objective line.
    // Logic details: validates required non-empty string arguments.
    public static string ObjectiveLine(PlayerQuestResponse quest)
        => QuestUtils.ObjectiveLine(quest);

    // Executes core business logic for reward line.
    // Logic details: validates required non-empty string arguments.
    public static string RewardLine(PlayerQuestResponse quest)
        => QuestUtils.RewardLine(quest);

    // Executes core business logic for get quest title.
    // Logic details: validates required non-empty string arguments.
    private string GetQuestTitle(int questId)
    {
        if (_responses != null && _responses.TryGetValue(questId, out var r) && !string.IsNullOrWhiteSpace(r?.QuestTitle))
            return r.QuestTitle;
        return "Quest";
    }
}

// Executes core business logic for player quest state.
[Serializable]
public class PlayerQuestState
{
    public int questId;
    // Supported player quest states: NotStarted, InProgress, Completed, Claimed, or Failed; the state controls progression and reward claiming.
    public string status;
    public int progress;
    public int targetAmount;
    public int version;
    public bool isDirty;

    // Executes core business logic for clone.
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

// Executes core business logic for offline quest entry.
[Serializable]
public class OfflineQuestEntry
{
    public int questId;
    public int progress;
}

// Executes core business logic for offline queue wrapper.
[Serializable]
public class OfflineQueueWrapper
{
    public List<OfflineQuestEntry> entries;
}
