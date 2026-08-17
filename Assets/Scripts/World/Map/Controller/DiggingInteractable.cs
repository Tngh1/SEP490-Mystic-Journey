using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.Video;

// Executes mono behaviour operation.
[RequireComponent(typeof(WorldInteractable))]
public class DiggingInteractable : MonoBehaviour
{

    [Header("Quest Link")]
    [Tooltip("QuestId của nhiệm vụ cần đào. Scene sẽ override giá trị này.")]
    [SerializeField] private int linkedQuestId = 30;

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


    private WorldInteractable _interactable;
    private bool _isDigging;
    private bool _dug;


    // Initializes internal component caches and dependencies for DiggingInteractable upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _interactable = GetComponent<WorldInteractable>();
    }

    // Performs startup initialization for DiggingInteractable on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        _interactable.ConfigureObject(
            key:           objectKey,
            objectName:    displayName,
            type:          "Interact",
            linkedQuestId: linkedQuestId,
            delta:         1,
            radius:        2.5f
        );

        _interactable.ConfigureQuestItem(objectKey, displayName, linkedQuestId, 1, 2.5f);

        RefreshVisibility();
        WorldRuntimeEvents.QuestsChanged += RefreshVisibility;

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.loopPointReached += OnVideoFinished;
        }
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshVisibility;
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }


    // Executes start dig operation.
    public void StartDig()
    {
        if (_isDigging || _dug) return;

        var questState = QuestUIManager.Instance != null ? QuestUIManager.Instance.GetQuestState(linkedQuestId) : null;
        var questStatus = questState != null ? questState.status : string.Empty;

        if (string.Equals(questStatus, "Completed", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(questStatus, "Claimed", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(questStatus, "InProgress", System.StringComparison.OrdinalIgnoreCase))
        {
            WorldRuntimeEvents.RaiseMessage("Accept the digging quest from Natalie first.");
            return;
        }

        WorldInteractionPromptRuntime.Hide();
        BeginDigSequence();
    }

    // Executes begin dig sequence operation.
    private void BeginDigSequence()
    {
        if (_dug) return;
        _isDigging = true;
        // Execute this timed sequence as a coroutine so delayed work yields between frames without blocking Unity's main thread.
        StartCoroutine(DigSequence());
    }



    // Executes dig sequence operation.
    private IEnumerator DigSequence()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        SpriteRenderer[] playerSprites = null;
        if (player != null)
        {
            playerSprites = player.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sp in playerSprites) sp.enabled = false;
        }

        if (videoPlayer != null && videoPlayer.clip != null)
        {
            MysticJourney.Features.Quest.QuestVideoManager.NotifyVideoStarted(videoPlayer);
            videoPlayer.gameObject.SetActive(true);
            videoPlayer.Play();

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
            yield return new WaitForSeconds(1f);
        }

        if (playerSprites != null)
            foreach (var sp in playerSprites) sp.enabled = true;

        SendInteractApi();
    }

    private bool _videoFinished;
    // Executes on video finished operation.
    private void OnVideoFinished(VideoPlayer vp) => _videoFinished = true;

    // Executes send interact api operation.
    private void SendInteractApi()
    {
        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[DiggingInteractable] No API token.");
            _isDigging = false;
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

                if (response?.Quest != null)
                    QuestUIManager.Instance?.ApplyServerQuestState(response.Quest);
                if (response != null && response.CollectedItemId.HasValue)
                    InventoryUIManager.RefreshAny(refreshStats: false);
                WorldRuntimeEvents.RaiseQuestsChanged();

                FinalizeAfterDig();
            },
            error =>
            {
                Debug.LogWarning($"[DiggingInteractable] InteractObject failed: {error.Message}");
                _isDigging = false;
                WorldRuntimeEvents.RaiseMessage("Digging failed. Please try again.");
            }

        );
    }

    // Executes finalize after dig operation.
    private void FinalizeAfterDig()
    {
        _isDigging = false;
        _dug = true;

        if (disableAfterDig)
        {
            var col2D = GetComponent<Collider2D>();
            if (col2D != null) col2D.enabled = false;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            _interactable.UpdateOverheadUI();
        }
    }

    // Executes refresh visibility operation.
    private void RefreshVisibility()
    {
        if (QuestUIManager.Instance == null) return;

        var quests = QuestUIManager.Instance.GetMainQuests();
        bool shouldShow = false;
        foreach (var q in quests)
        {
            if (q.QuestId == linkedQuestId && QuestUIManager.IsStatus(q, "InProgress"))
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
