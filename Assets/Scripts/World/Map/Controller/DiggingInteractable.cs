using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Gắn script này vào các GameObject có thể "đào" trong map (ví dụ: TreeEvil8 ở AbandonedCastle).
/// Khi quest liên quan đang InProgress:
///   - Hiện dấu "?" phía trên vật thể.
///   - Nhấn E → chiếu video đào → quest progress += 1 → vật phẩm vào túi (thông qua API InteractObject).
/// Sau khi đào xong, collider bị tắt (không tương tác lại được).
/// </summary>
[RequireComponent(typeof(WorldInteractable))]
public class DiggingInteractable : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────────

    [Header("Quest Link")]
    // Default trỏ tới quest "[Chapter 4] The Skull by the Well" (AbandonedCastle,
    // ObjectiveTarget = "Skull"). KHÔNG ghi số quest vào tooltip: mỗi lần chèn quest mới là số lệch,
    // và trước đây tooltip nói "Quest 24" trong khi field là 23 và quest thật lại là số khác nữa.
    [Tooltip("QuestId của nhiệm vụ cần đào. Scene sẽ override giá trị này.")]
    [SerializeField] private int linkedQuestId = 29;

    [Tooltip("ObjectKey gửi lên API. Phải khớp với ObjectKey mà backend nhận ra (ví dụ: 'AbandonedCastle.Skull').")]
    [SerializeField] private string objectKey = "AbandonedCastle.Skull";

    [Tooltip("Tên hiển thị khi player đứng gần (sẽ thay thế tên GameObject nếu không trống).")]
    [SerializeField] private string displayName = "Old Tree Root";

    [Header("Video")]
    [Tooltip("VideoPlayer cần chiếu khi đào. Nhớ set Render Mode = Camera Near Plane để che màn hình.")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("Thời gian tối đa chờ video (giây). Nếu video ngắn hơn thì sẽ kết thúc sớm hơn.")]
    [SerializeField] private float maxVideoWait = 8f;

    [Header("Behaviour")]
    [Tooltip("Sau khi đào xong, tắt collider để không tương tác lại.")]
    [SerializeField] private bool disableAfterDig = true;

    // ─── Private ───────────────────────────────────────────────────────────────

    private WorldInteractable _interactable;
    private bool _isDigging;
    private bool _dug;

    // ─── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _interactable = GetComponent<WorldInteractable>();
    }

    private void Start()
    {
        // Tự cấu hình WorldInteractable nếu chưa được set qua Inspector
        _interactable.ConfigureObject(
            key:           objectKey,
            objectName:    displayName,
            type:          "Interact",
            linkedQuestId: linkedQuestId,
            delta:         1,
            radius:        2.5f
        );

        // Override kind sang QuestItem để hệ thống ? hoạt động
        // (ConfigureObject đặt kind = Object, cần chuyển sang QuestItem để hiện ?)
        // Dùng reflection-free workaround: gọi ConfigureQuestItem.
        _interactable.ConfigureQuestItem(objectKey, displayName, linkedQuestId, 1, 2.5f);

        RefreshVisibility();
        WorldRuntimeEvents.QuestsChanged += RefreshVisibility;

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.loopPointReached += OnVideoFinished;
        }
    }

    private void OnDestroy()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshVisibility;
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Được gọi bởi WorldInteractable.OnSuccessfulInteraction() qua hook.
    /// Thực ra chúng ta hook trực tiếp qua PlayerWorldInteractor flow:
    /// PlayerWorldInteractor → InteractWithObject → WorldApi.InteractObject → callback → target.OnSuccessfulInteraction()
    /// Nhưng vì cần chiếu video TRƯỚC KHI gửi API, ta override flow bằng cách
    /// chặn interaction từ sớm thông qua component DiggingInteractable.
    /// </summary>
    public void StartDig()
    {
        if (_isDigging || _dug) return;

        var questState = QuestManager.Instance != null ? QuestManager.Instance.GetQuestState(linkedQuestId) : null;
        var questStatus = questState != null ? questState.status : string.Empty;

        if (string.Equals(questStatus, "Completed", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(questStatus, "Claimed", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        WorldInteractionPromptRuntime.Hide();

        if (QuestManager.Instance != null &&
            !string.Equals(questStatus, "InProgress", System.StringComparison.OrdinalIgnoreCase))
        {
            _isDigging = true;
            QuestManager.Instance.AcceptQuest(
                linkedQuestId,
                onSuccess: () =>
                {
                    _isDigging = false;
                    WorldRuntimeEvents.RaiseQuestsChanged();
                    BeginDigSequence();
                },
                onError: error =>
                {
                    Debug.LogWarning($"[DiggingInteractable] AcceptQuest failed: {error}");
                    _isDigging = false;
                    WorldRuntimeEvents.RaiseMessage("You need to complete and claim the reward for the previous quest first!");

                }
            );
            return;
        }

        BeginDigSequence();
    }

    private void BeginDigSequence()
    {
        if (_dug) return;
        _isDigging = true;
        StartCoroutine(DigSequence());
    }


    // ─── Private ───────────────────────────────────────────────────────────────

    private IEnumerator DigSequence()
    {
        // 1. Ẩn player (tuỳ chọn)
        var player = GameObject.FindGameObjectWithTag("Player");
        SpriteRenderer[] playerSprites = null;
        if (player != null)
        {
            playerSprites = player.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sp in playerSprites) sp.enabled = false;
        }

        // 2. Chiếu video đào
        bool videoPlayed = false;
        if (videoPlayer != null && videoPlayer.clip != null)
        {
            MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoStarted(videoPlayer);
            videoPlayer.gameObject.SetActive(true);
            videoPlayer.Play();
            videoPlayed = true;

            // Chờ video xong hoặc hết thời gian tối đa
            float elapsed = 0f;
            while (!_videoFinished && elapsed < maxVideoWait)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            _videoFinished = false;

            videoPlayer.Stop();
            videoPlayer.gameObject.SetActive(false);
            MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoEnded(videoPlayer);
        }

        else
        {
            // Nếu không có video, chờ 1 giây giả lập
            yield return new WaitForSeconds(1f);
        }

        // 3. Hiện lại player
        if (playerSprites != null)
            foreach (var sp in playerSprites) sp.enabled = true;

        // 4. Gửi API InteractObject → quest progress + item vào túi
        SendInteractApi();
    }

    private bool _videoFinished;
    private void OnVideoFinished(VideoPlayer vp) => _videoFinished = true;

    private void SendInteractApi()
    {
        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[DiggingInteractable] No API token.");
            FinalizeAfterDig();
            return;
        }

        WorldApi.Instance.InteractObject(
            objectKey,
            "Interact",
            linkedQuestId,
            1,
            response =>
            {
                Debug.Log($"[DiggingInteractable] Interact success: {response?.Message}");
                WorldRuntimeEvents.RaiseMessage(response?.Message ?? $"{displayName}: bạn đã đào được vật phẩm!");

                // Cập nhật quest progress trong client
                QuestManager.Instance?.AddProgress(linkedQuestId, 1);
                WorldRuntimeEvents.RaiseQuestsChanged();

                FinalizeAfterDig();
            },
            error =>
            {
                Debug.LogWarning($"[DiggingInteractable] InteractObject failed: {error.Message}");
                FinalizeAfterDig();
            }

        );
    }

    private void FinalizeAfterDig()
    {
        _isDigging = false;
        _dug = true;

        if (disableAfterDig)
        {
            // Tắt collider để không tương tác lại
            var col2D = GetComponent<Collider2D>();
            if (col2D != null) col2D.enabled = false;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Ẩn dấu ? trên đầu
            _interactable.UpdateOverheadUI();
        }
    }

    /// <summary>
    /// Hiện/ẩn dấu ? dựa vào trạng thái quest hiện tại.
    /// </summary>
    private void RefreshVisibility()
    {
        if (QuestManager.Instance == null) return;

        var quests = QuestManager.Instance.GetMainQuests();
        bool shouldShow = false;
        foreach (var q in quests)
        {
            if (q.QuestId == linkedQuestId && QuestManager.IsStatus(q, "InProgress"))
            {
                shouldShow = true;
                break;
            }
        }

        var col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = shouldShow && !_dug;

        _interactable.UpdateOverheadUI();
    }
}
