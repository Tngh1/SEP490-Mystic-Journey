using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(WorldInteractable))]
public class OriginTreeInteractable : MonoBehaviour
{
    [Header("Quest Link")]
    [Tooltip("Quest ID for healing the Origin Tree (e.g. 30).")]
    [SerializeField] private int linkedQuestId = 30;

    [Tooltip("ObjectKey gửi lên API.")]
    [SerializeField] private string objectKey = "ElfForest.OriginTree";

    [Tooltip("Tên hiển thị khi player đứng gần.")]
    [SerializeField] private string displayName = "Origin Tree";

    [Header("Video")]
    [Tooltip("Video to play when healing the tree.")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("Maximum time to wait for the video.")]
    [SerializeField] private float maxVideoWait = 10f;

    private WorldInteractable _interactable;
    private bool _isHealing;
    private bool _healed;
    private bool _videoFinished;

    private void Awake()
    {
        _interactable = GetComponent<WorldInteractable>();
    }

    private void Start()
    {
        _interactable.ConfigureObject(
            key: objectKey,
            objectName: displayName,
            type: "Heal",
            linkedQuestId: linkedQuestId,
            delta: 1,
            radius: 3.5f
        );

        _interactable.ConfigureQuestItem(objectKey, displayName, linkedQuestId, 1, 3.5f);

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

    public void StartHeal()
    {
        if (_isHealing || _healed) return;

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
            _isHealing = true;
            QuestManager.Instance.AcceptQuest(
                linkedQuestId,
                onSuccess: () =>
                {
                    _isHealing = false;
                    WorldRuntimeEvents.RaiseQuestsChanged();
                    BeginHealSequence();
                },
                onError: error =>
                {
                    Debug.LogWarning($"[OriginTreeInteractable] AcceptQuest failed: {error}");
                    _isHealing = false;
                    WorldRuntimeEvents.RaiseMessage("You need to complete and claim the reward for the previous quest first!");

                }
            );
            return;
        }

        BeginHealSequence();
    }

    private void BeginHealSequence()
    {
        if (_healed) return;
        _isHealing = true;
        StartCoroutine(HealSequence());
    }


    private IEnumerator HealSequence()
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
            yield return new WaitForSeconds(2f);
        }

        if (playerSprites != null)
            foreach (var sp in playerSprites) sp.enabled = true;

        SendInteractApi();
    }

    private void OnVideoFinished(VideoPlayer vp) => _videoFinished = true;

    private void SendInteractApi()
    {
        if (!ApiClient.Instance.HasToken())
        {
            FinalizeAfterHeal();
            return;
        }

        WorldApi.Instance.InteractObject(
            objectKey,
            "Interact",
            linkedQuestId,
            1,
            response =>
            {
                WorldRuntimeEvents.RaiseMessage(response?.Message ?? "The Origin Tree is cleansed! Talk to Lyra.");
                QuestManager.Instance?.AddProgress(linkedQuestId, 1);
                WorldRuntimeEvents.RaiseQuestsChanged();
                FinalizeAfterHeal();
            },
            error =>
            {
                Debug.LogWarning($"[OriginTreeInteractable] InteractObject failed: {error.Message}");
                FinalizeAfterHeal();
            }

        );
    }

    private void FinalizeAfterHeal()
    {
        _isHealing = false;
        _healed = true;

        var col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        _interactable.UpdateOverheadUI();
    }

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
        if (col2D != null) col2D.enabled = shouldShow && !_healed;

        _interactable.UpdateOverheadUI();
    }
}
