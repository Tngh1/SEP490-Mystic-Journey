using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;
using UnityEngine.Video;

[RequireComponent(typeof(WorldInteractable))]
public class IvyTreeInteractable : MonoBehaviour
{
    private const string CompletionMessage = "In the letter, she thanks you for bringing her remains back to her homeland and asks you to bury her beneath the ivy tree in her courtyard. In return, she reveals the cause of the ancient power leak and rewards you with the Mystic Key, which opens the castle on the abandoned island.";

    [Header("Quest Link")]
    [Tooltip("Quest ID for Rest in Peace (e.g. 25).")]
    [SerializeField] private int linkedQuestId = 25;

    [Tooltip("ObjectKey gửi lên API.")]
    [SerializeField] private string objectKey = "AbandonedCastle.IvyTree";

    [Tooltip("Tên hiển thị khi player đứng gần.")]
    [SerializeField] private string displayName = "Ivy Tree";

    [Header("Video")]
    [Tooltip("Video to play when interacting with Ivy Tree (Letter & Burial).")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("Maximum time to wait for the video.")]
    [SerializeField] private float maxVideoWait = 15f;

    private WorldInteractable _interactable;
    private bool _isInteracting;
    private bool _interacted;
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
            type: "Interact",
            linkedQuestId: linkedQuestId,
            delta: 1,
            radius: 3.5f // Bán kính lớn cho cây to
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

    public void StartInteraction()
    {
        if (_isInteracting || _interacted) return;

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
            _isInteracting = true;
            QuestManager.Instance.AcceptQuest(
                linkedQuestId,
                () =>
                {
                    WorldRuntimeEvents.RaiseQuestsChanged();
                    BeginInteractionSequence();
                },
                error =>
                {
                    Debug.LogWarning($"[IvyTreeInteractable] AcceptQuest failed: {error}");
                    _isInteracting = false;
                }
            );
            return;
        }

        BeginInteractionSequence();
    }

    private void BeginInteractionSequence()
    {
        if (_interacted) return;
        _isInteracting = true;
        StartCoroutine(InteractionSequence());
    }

    private IEnumerator InteractionSequence()
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
            FinalizeAfterInteraction();
            return;
        }

        WorldApi.Instance.InteractObject(
            objectKey,
            "Interact",
            linkedQuestId,
            1,
            response =>
            {
                WorldRuntimeEvents.RaiseMessage(CompletionMessage);
                QuestManager.Instance?.AddProgress(linkedQuestId, 1);
                WorldRuntimeEvents.RaiseQuestsChanged();
                FinalizeAfterInteraction();
            },
            error =>
            {
                WorldRuntimeEvents.RaiseMessage(CompletionMessage);
                QuestManager.Instance?.AddProgress(linkedQuestId, 1);
                WorldRuntimeEvents.RaiseQuestsChanged();
                FinalizeAfterInteraction();
            }
        );
    }

    private void FinalizeAfterInteraction()
    {
        _isInteracting = false;
        _interacted = true;

        var col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        _interactable.UpdateOverheadUI();

        RemoveQuestItemFromInventory("Spirit Skull");
    }

    private void RemoveQuestItemFromInventory(string itemName)
    {
        if (!ApiClient.Instance.HasToken()) return;

        InventoryApi.Instance.GetInventory(
            onSuccess: inv =>
            {
                if (inv?.BagItems == null) return;

                foreach (var item in inv.BagItems)
                {
                    if (item.ItemName != null &&
                        item.ItemName.IndexOf(itemName, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                        item.Quantity > 0)
                    {
                        InventoryApi.Instance.ConsumeItem(
                            item.InventoryItemId, 1,
                            _ =>
                            {
                                Debug.Log($"[IvyTreeInteractable] Removed '{itemName}' from inventory.");
                                InventoryManager.RefreshAny(refreshStats: false);
                            },
                            err => Debug.LogWarning($"[IvyTreeInteractable] Failed to remove '{itemName}': {err.Message}")
                        );
                        break;
                    }
                }
            },
            onError: err => Debug.LogWarning($"[IvyTreeInteractable] GetInventory failed: {err.Message}")
        );
    }

    private void RefreshVisibility()
    {
        if (QuestManager.Instance == null) return;

        var quests = QuestManager.Instance.GetMainQuests();
        bool shouldShow = false;
        foreach (var q in quests)
        {
            if (q.QuestId == linkedQuestId &&
                (QuestManager.IsStatus(q, "NotStarted") || QuestManager.IsStatus(q, "InProgress")))
            {
                shouldShow = true;
                break;
            }
        }

        var col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = shouldShow && !_interacted;

        _interactable.UpdateOverheadUI();
    }
}
