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
    [SerializeField] private int linkedQuestId = 24;

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

        gameObject.SetActive(true);
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

        _isInteracting = true;
        WorldInteractionPromptRuntime.Hide();

        CheckHasSkullItem(hasSkull =>
        {
            if (!hasSkull)
            {
                _isInteracting = false;
                WorldRuntimeEvents.RaiseMessage("You need Natalie's remains (Spirit Skull) from the old well before burying her!");
                return;
            }

            if (QuestManager.Instance != null &&
                !string.Equals(questStatus, "InProgress", System.StringComparison.OrdinalIgnoreCase))
            {
                QuestManager.Instance.AcceptQuest(
                    linkedQuestId,
                    onSuccess: () =>
                    {
                        _isInteracting = false;
                        WorldRuntimeEvents.RaiseQuestsChanged();
                        BeginInteractionSequence();
                    },
                    onError: error =>
                    {
                        Debug.LogWarning($"[IvyTreeInteractable] AcceptQuest failed: {error}");
                        _isInteracting = false;
                        WorldRuntimeEvents.RaiseMessage("You need to complete and claim the reward for the previous quest first!");
                    }
                );
                return;
            }

            BeginInteractionSequence();
        });
    }

    private void CheckHasSkullItem(System.Action<bool> callback)
    {
        if (!ApiClient.Instance.HasToken())
        {
            callback?.Invoke(true);
            return;
        }

        InventoryApi.Instance.GetInventory(
            onSuccess: inv =>
            {
                if (inv?.BagItems != null)
                {
                    foreach (var item in inv.BagItems)
                    {
                        bool isSkull = (item.ItemId == 32) ||
                                       (item.ItemName != null && item.ItemName.IndexOf("Skull", System.StringComparison.OrdinalIgnoreCase) >= 0);
                        if (isSkull && item.Quantity > 0)
                        {
                            callback?.Invoke(true);
                            return;
                        }
                    }
                }
                callback?.Invoke(false);
            },
            onError: err =>
            {
                Debug.LogWarning($"[IvyTreeInteractable] CheckHasSkullItem error: {err.Message}");
                callback?.Invoke(true);
            }
        );
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
                Debug.LogWarning($"[IvyTreeInteractable] InteractObject failed: {error.Message}");
                WorldRuntimeEvents.RaiseMessage("Cannot interact. You must accept the quest first!");
                FinalizeAfterInteraction();
            }
        );
    }

    private void FinalizeAfterInteraction()
    {
        _isInteracting = false;
        _interacted = true;

        // Cây Thường Xuân giữ nguyên hình ảnh cảnh quan, chỉ tắt Collider để không bấm lại được
        gameObject.SetActive(true);

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
                    bool isSkull = (item.ItemId == 32) ||
                                   (item.ItemName != null &&
                                    (item.ItemName.IndexOf("Skull", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     item.ItemName.IndexOf(itemName, System.StringComparison.OrdinalIgnoreCase) >= 0));
                    if (isSkull && item.Quantity > 0)
                    {
                        InventoryApi.Instance.ConsumeItem(
                            item.InventoryItemId, 1,
                            _ =>
                            {
                                Debug.Log($"[IvyTreeInteractable] Consumed skull item '{item.ItemName}' (ID {item.ItemId}) from inventory.");
                                InventoryManager.RefreshAny(refreshStats: false);
                            },
                            err => Debug.LogWarning($"[IvyTreeInteractable] Failed to consume skull item: {err.Message}")
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
        // Luôn giữ GameObject của cây active để môi trường thiên nhiên hiển thị đầy đủ
        gameObject.SetActive(true);

        if (QuestManager.Instance == null) return;

        var quests = QuestManager.Instance.GetMainQuests();
        bool shouldShowPrompt = false;
        foreach (var q in quests)
        {
            if (q.QuestId == linkedQuestId &&
                (QuestManager.IsStatus(q, "NotStarted") || QuestManager.IsStatus(q, "InProgress")))
            {
                shouldShowPrompt = true;
                break;
            }
        }

        var col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = shouldShowPrompt && !_interacted;

        _interactable.UpdateOverheadUI();
    }

}
