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

    // Chống gọi trùng: nhiều nguồn (load, batch sync, NPC panel) cùng auto-complete/claim
    // một quest trước khi API đầu tiên trả về. Nếu không chặn sẽ gửi request thừa hoặc lỗi
    // "đã claim". Xóa key trong cả onSuccess lẫn onError.
    // Lưu THỜI ĐIỂM gửi, không phải bool: nếu coroutine HTTP bị chết giữa đường (đổi scene,
    // GameObject host bị Destroy, request không bao giờ resolve) thì không callback nào chạy và
    // key sẽ kẹt vĩnh viễn → mọi lần complete/claim sau đó bị skip, quest treo InProgress mãi.
    // Quá InFlightTimeout thì coi như request đã mất và cho phép gửi lại.
    private readonly Dictionary<int, float> _completing = new();
    private readonly Dictionary<int, float> _claiming = new();
    private const float InFlightTimeout = ApiConfig.Timeout + 5f;

    // true nếu được phép gửi request mới (và đã đánh dấu in-flight).
    private static bool TryBeginInFlight(Dictionary<int, float> inFlight, int questId)
    {
        if (inFlight.TryGetValue(questId, out var startedAt) &&
            Time.realtimeSinceStartup - startedAt < InFlightTimeout)
            return false;

        inFlight[questId] = Time.realtimeSinceStartup;
        return true;
    }
    private int _batchVersion;
    private Coroutine _batchCoroutine;

    // Một silent claim (dọn lúc load) mở quest kế tiếp trong chain, nhưng BE chỉ tạo bản ghi
    // quest đó ở lần GetMyQuests SAU. Reload đúng 1 lần cho mỗi questId đã silent-claim để lấy
    // quest mới unlock — không thì đi qua portal sang map mới sẽ bị "No quest available".
    // Theo questId (không phải bool) nên tự chặn reload storm: mỗi quest chỉ claim được 1 lần.
    private readonly HashSet<int> _silentClaimRefetched = new();

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
                if (response?.Success == true && response.ConsumedQuantity > 0)
                    InventoryManager.RefreshAny(refreshStats: false);
                OnQuestProgressChanged?.Invoke(questId);
                onSuccess?.Invoke(response);
            },
            err => { Debug.LogError($"[QuestManager] TurnInQuestItem FAIL: {err.Message}"); onError?.Invoke(err.Message); });
    }

    public bool CanEnterMap(MapData map)
    {
        if (map == null || map.unlockQuestId <= 0) return true;
        var state = GetQuestState(map.unlockQuestId);
        // Phải là "Claimed" đúng như BE (IsMainQuestUnlocked). Nếu cho qua khi mới "Completed",
        // người chơi sang map mới nhưng BE chưa mở quest kế tiếp -> map trống, kẹt không có nhiệm vụ.
        return state != null && string.Equals(state.status, "Claimed", StringComparison.OrdinalIgnoreCase);
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
        // Chỉ bỏ qua khi đã ở trạng thái đang tiến hành / hoàn thành / nhận thưởng
        // NotStarted phải luôn gọi API để server cập nhật status -> InProgress.
        if (_cache.TryGetValue(questId, out var existingState) &&
            existingState.status != "NotStarted" &&
            existingState.status != "Failed" &&
            existingState.status != null)
        {
            Debug.Log($"[QuestManager] AcceptQuest: questId={questId} already {existingState.status}, skipping.");
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
                    // Fallback: server trả null nhưng vẫn thành công -> ép InProgress
                    cached.status = "InProgress";
                    cached.isDirty = false;
                }

                Debug.Log($"[QuestManager] Accepted questId={questId} -> InProgress");
                MysticJourney.Features.Quest.QuestWaypointManager.IsTrackingEnabled = true;
                
                string qTitle = response?.QuestTitle ?? GetQuestTitle(questId);
                if (MainQuestPanelRuntime.Instance != null && !string.IsNullOrWhiteSpace(qTitle))
                    MainQuestPanelRuntime.Instance.ShowQuestPopup(qTitle, UIQuestPopupView.QuestPopupKind.Accepted);

                OnQuestAccepted?.Invoke(questId);
                WorldRuntimeEvents.RaiseQuestsChanged();
                onSuccess?.Invoke();
            },
            onError: err =>
            { 
                Debug.LogWarning($"[QuestManager] AcceptQuest FAIL: {err.Message}"); 
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
        if (!TryBeginInFlight(_completing, questId))
        {
            Debug.Log($"[QuestManager] CompleteQuest: questId={questId} already in-flight, skipping.");
            return;
        }

        PlayerQuestApi.Instance.CompleteQuest(questId,
            onSuccess: response =>
            {
                _completing.Remove(questId);
                if (response != null) UpsertQuestState(response);
                InventoryManager.RefreshAny(refreshStats: false);

                // KHÔNG bắn popup ở đây: CompleteQuest luôn là bước trung gian, ngay sau đó
                // ClaimReward bắn popup kết thúc duy nhất. Bắn ở cả hai gây popup chồng.
                OnQuestProgressChanged?.Invoke(questId);
                onSuccess?.Invoke();
            },
            onError: err =>
            {
                _completing.Remove(questId);
                Debug.LogError($"[QuestManager] CompleteQuest FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            });
    }

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

        // Đẩy luôn sang _responses: UI (tracker + quest panel) render từ GetMainQuests() tức
        // _responses, KHÔNG phải _cache. Nếu chỉ ghi _cache thì progress chỉ hiện sau khi
        // BatchSyncLoop round-trip xong -> tracker luôn chậm ĐÚNG MỘT MẠNG, và khi 2 mạng cùng
        // rơi vào 1 tick 1s thì lần repaint kế tiếp nhảy +2 (nhìn như "giết 1 mà tính 2", thực ra
        // đếm vẫn đúng, chỉ là hiển thị bị trễ rồi bù một cục).
        MirrorProgressToResponse(questId, state.progress, targetAmount);

        // KHÔNG complete/claim tại đây. Progress này mới ở local — server chưa nhận (BatchSyncLoop
        // sync sau). Nếu gọi CompleteQuest ngay, server thấy Progress < target → 400, và tệ hơn là
        // giữ lock _completing khiến cú CompleteQuest của batch sync bị skip → quest kẹt InProgress
        // server-side, không bao giờ Claimed → main quest kế tiếp không unlock.
        // BatchSyncLoop là đường DUY NHẤT hoàn thành quest in-world: nó đẩy progress lên server
        // TRƯỚC, rồi auto CompleteQuest + ClaimReward khi server xác nhận Progress >= target.
        // Collect là ngoại lệ: hoàn thành qua turn-in ở NPC, không tính từ world progress.
        // Collect progress chỉ tồn tại trong phiên chơi. Không đưa vào batch/offline queue:
        // thoát trước khi đủ thì phiên sau bắt đầu lại từ progress trên server (luôn là 0).
        if (!isCollect)
            _pendingBatch[questId] = state.progress;
        OnQuestProgressChanged?.Invoke(questId);
    }

    // Giữ _responses (nguồn dữ liệu của UI) khớp với progress local trong _cache.
    // Chỉ đi LÊN: rollback batch và server response đi qua UpsertQuestState, không qua đây.
    private void MirrorProgressToResponse(int questId, int progress, int targetAmount)
    {
        if (!_responses.TryGetValue(questId, out var response) || response == null) return;
        response.TargetAmount = targetAmount;
        if (progress > response.Progress) response.Progress = progress;
    }

    // silent=true: claim nền lúc load (không popup, không LoadMyQuests). Dùng khi dọn các quest
    // Completed tồn từ phiên trước để tránh popup spam + reload storm khi vừa đăng nhập.
    public void ClaimReward(int questId, Action onSuccess = null, Action<string> onError = null, bool silent = false)
    {
        if (!_cache.TryGetValue(questId, out var state)) { onError?.Invoke("Quest not found."); return; }
        if (state.status != "Completed") { onError?.Invoke("Quest chua Complete."); return; }

        if (!TryBeginInFlight(_claiming, questId))
        {
            Debug.Log($"[QuestManager] ClaimReward: questId={questId} already in-flight, skipping.");
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

                // Không tự động AcceptQuest tiếp theo: người chơi phải về gặp NPC QuestGiver
                // để đọc dialogue rồi mới nhận nhiệm vụ kế.
                Debug.Log($"[QuestManager] Claimed questId={questId}");
                InventoryManager.RefreshAny(refreshStats: false);

                if (!silent)
                {
                    string qTitle = response?.QuestTitle ?? GetQuestTitle(questId);
                    if (MainQuestPanelRuntime.Instance != null && !string.IsNullOrWhiteSpace(qTitle))
                        MainQuestPanelRuntime.Instance.ShowQuestPopup(qTitle, UIQuestPopupView.QuestPopupKind.Claimed);
                }

                OnQuestClaimed?.Invoke(questId);

                // silent (dọn lúc load): reload ĐÚNG 1 LẦN. BE chỉ mở quest kế tiếp khi quest
                // trước đã "Claimed" và chỉ tạo bản ghi ở lần GetMyQuests kế — không reload thì
                // quest vừa unlock (vd sang AutumnPumpkin sau khi claim quest 8) không bao giờ
                // xuất hiện. Cờ _silentClaimRefetched chặn reload storm.
                if (!silent)
                {
                    LoadMyQuests();
                }
                else if (_silentClaimRefetched.Add(questId))
                {
                    LoadMyQuests();
                }

                // Refresh all quest-driven world links so NPC visibility updates immediately.
                WorldRuntimeEvents.RaiseQuestsChanged();

                // Notify rằng 1 quest vừa Claimed — ai đó có thể check xem map mới có mở không
                WorldRuntimeEvents.RaiseMapCompleted(questId);

                onSuccess?.Invoke();
            },
            onError: err =>
            {
                _claiming.Remove(questId);
                Debug.LogError($"[QuestManager] ClaimReward FAIL: {err.Message}");
                onError?.Invoke(err.Message);
            });
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
            PushPendingBatch();
        }
    }

    /// <summary>
    /// Đẩy ngay progress đang chờ lên server, không đợi tick 1s của BatchSyncLoop.
    /// Bắt buộc gọi trước khi unload scene (đổi map/portal): coroutine chạy trên
    /// QuestManager (DontDestroyOnLoad) nhưng nếu chưa kịp tick thì
    /// HandleLoadedQuestResponses của map mới sẽ _pendingBatch.Clear() và mất progress.
    /// </summary>
    public void FlushPendingProgressNow() => PushPendingBatch();

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
                        Debug.Log($"[QuestManager] Stale batch response (v{sentVersion} < v{_batchVersion}), skip.");
                        return;
                    }
                    foreach (var key in syncedKeys)
                        _snapshot.Remove(key);
                    foreach (var r in responses)
                        if (r != null) UpsertQuestState(r);

                    // Server vừa xác nhận -> repaint. UpsertQuestState chỉ ghi dictionary, không tự
                    // bắn event, nên thiếu dòng này thì mọi hiệu chỉnh từ server (kể cả trường hợp
                    // server kẹp progress thấp hơn client) chỉ hiện ra ở lần AddProgress kế tiếp.
                    if (responses != null && responses.Count > 0)
                        OnQuestProgressChanged?.Invoke(-1);

                    // Auto-complete Collect/Defeat quests that reached target —
                    // player should not need to press Complete manually.
                    foreach (var r in responses)
                    {
                        if (r == null) continue;
                        var objectiveType = r.ObjectiveType ?? string.Empty;
                        // Collect is intentionally excluded: it's completed by turning items in at
                        // the NPC quest giver, not auto-finished from world progress.
                        var isAutoComplete =
                            string.Equals(objectiveType, "Defeat",    StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Kill",      StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Explore",   StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Interact",  StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "OpenChest", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "Talk",      StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(objectiveType, "EquipSkill",StringComparison.OrdinalIgnoreCase);


                        if (!isAutoComplete) continue;
                        
                        // [FIX] Kiểm tra Progress >= TargetAmount thay vì chỉ check Status == "Completed"
                        // vì server BatchUpdateProgress không tự động chuyển Status sang Completed.
                        bool isFinished = string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase) || 
                                          (r.Progress >= Mathf.Max(1, r.TargetAmount));
                                          
                        if (!isFinished) continue;

                        var qid = r.QuestId;
                        Debug.Log($"[QuestManager] Auto-completing questId={qid} ({objectiveType})");
                        CompleteQuest(qid,
                            onSuccess: () =>
                            {
                                Debug.Log($"[QuestManager] Auto-complete done questId={qid}");

                                // ClaimReward (non-silent) tự bắn popup "Reward Claimed!" — không bắn
                                // thêm popup ở đây để tránh chồng 2 popup cho cùng 1 lần hoàn thành.
                                ClaimReward(qid,
                                    onSuccess: () => WorldRuntimeEvents.RaiseQuestsChanged(),
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
                            // Rollback phải kéo _responses về theo, nếu không UI vẫn giữ con số lạc
                            // quan mà server đã từ chối. Đây là đường DUY NHẤT progress đi xuống,
                            // nên set thẳng chứ không dùng MirrorProgressToResponse (hàm đó chỉ tăng).
                            if (_responses.TryGetValue(key, out var resp) && resp != null)
                                resp.Progress = snap.progress;
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
        // Preserve quests that are already Completed or Claimed locally,
        // in case the API only returns active quests and drops them.
        var oldFinishedQuests = _responses.Values
            .Where(q => string.Equals(q.Status, "Completed", StringComparison.OrdinalIgnoreCase) || 
                        string.Equals(q.Status, "Claimed", StringComparison.OrdinalIgnoreCase))
            .ToList();

        _cache.Clear();
        _responses.Clear();
        // Server vừa trả state mới nhất → bỏ progress đang chờ/snapshot cũ. Nếu giữ lại,
        // BatchSyncLoop kế tiếp sẽ đẩy progress cũ đè lên state tươi → desync. ApplyOfflineQueue
        // bên dưới sẽ re-populate _pendingBatch nếu thật sự có progress offline chưa sync.
        _pendingBatch.Clear();
        _snapshot.Clear();
        foreach (var response in responses ?? new List<PlayerQuestResponse>())
        {
            // Collect dở dang chỉ tồn tại trong RAM của phiên chơi. Dữ liệu cũ từng được lưu
            // bởi client trước đây cũng phải hiển thị lại từ 0, nhưng không ghi ngược xuống DB.
            if (string.Equals(response.Status, "InProgress", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.ObjectiveType, "Collect", StringComparison.OrdinalIgnoreCase)
                && response.Progress < Mathf.Max(1, response.TargetAmount))
                response.Progress = 0;

            UpsertQuestState(response);
            
            var objectiveType = response.ObjectiveType ?? string.Empty;
            var qid = response.QuestId;
            bool isFinished = string.Equals(response.Status, "Completed", StringComparison.OrdinalIgnoreCase);
            bool canComplete = string.Equals(response.Status, "InProgress", StringComparison.OrdinalIgnoreCase) && 
                               response.Progress >= Mathf.Max(1, response.TargetAmount);

            // Tự động nhận thưởng (ClaimReward) với mọi quest đã Completed (như Quest 24) ngoại trừ Collect (nộp cho NPC)
            if (isFinished && !string.Equals(objectiveType, "Collect", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[QuestManager] Auto-claiming completed questId={qid}");
                int claimId = qid;
                ClaimReward(claimId,
                    onSuccess: () => { },
                    onError: err => Debug.LogWarning($"[QuestManager] Auto-claim on load fail questId={claimId}: {err}"),
                    silent: true);
            }
            else if (canComplete && !string.Equals(objectiveType, "Collect", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[QuestManager] Auto-completing loaded questId={qid}");
                int completeId = qid;
                CompleteQuest(completeId,
                    onSuccess: () =>
                    {
                        ClaimReward(completeId,
                            onSuccess: () => { },
                            onError: err => Debug.LogWarning($"[QuestManager] Auto-claim on load fail questId={completeId}: {err}"),
                            silent: true);
                    },
                    onError: err => Debug.LogWarning($"[QuestManager] Auto-complete on load fail questId={completeId}: {err}"));
            }
        }




        // Restore missing finished quests to the local cache
        foreach (var oldQuest in oldFinishedQuests)
        {
            if (!_responses.ContainsKey(oldQuest.QuestId))
            {
                UpsertQuestState(oldQuest);
            }
        }

        ApplyOfflineQueue();

        if (_batchCoroutine != null) StopCoroutine(_batchCoroutine);
        _batchCoroutine = StartCoroutine(BatchSyncLoop());

        Debug.Log($"[QuestManager] Loaded {_cache.Count} quests from server.");
        OnQuestsLoaded?.Invoke();
        WorldRuntimeEvents.RaiseQuestsChanged();
    }

    // Áp trạng thái quest do server trả (vd InteractObject trả Quest đã cộng progress) vào cache
    // và thông báo UI. Dùng khi server là nguồn sự thật cho progress, tránh lệch với local.
    public void ApplyServerQuestState(PlayerQuestResponse response)
    {
        if (response == null) return;
        UpsertQuestState(response);
        _pendingBatch.Remove(response.QuestId);
        OnQuestProgressChanged?.Invoke(response.QuestId);
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
        // Snapshot: CompleteQuest/ClaimReward -> UpsertQuestState writes _responses, and their
        // callbacks can run synchronously on a cached/failed request -> "Collection was modified".
        foreach (var q in _responses.Values.ToList())
        {
            if (QuestUtils.IsStatus(q, "InProgress") && string.Equals(q.ObjectiveType, "EquipSkill", StringComparison.OrdinalIgnoreCase))
            {
                CompleteQuest(q.QuestId,
                    onSuccess: () =>
                    {
                        // ClaimReward (non-silent) tự bắn popup — không bắn thêm ở đây.
                        ClaimReward(q.QuestId,
                            onSuccess: () => WorldRuntimeEvents.RaiseQuestsChanged(),
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

    private string GetQuestTitle(int questId)
    {
        if (_responses != null && _responses.TryGetValue(questId, out var r) && !string.IsNullOrWhiteSpace(r?.QuestTitle))
            return r.QuestTitle;
        return "Quest";
    }
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
