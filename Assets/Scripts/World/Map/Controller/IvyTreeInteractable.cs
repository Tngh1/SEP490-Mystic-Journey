using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;

// Executes mono behaviour operation.
[RequireComponent(typeof(WorldInteractable))]
public class IvyTreeInteractable : MonoBehaviour
{
    private const string CompletionMessage = "In the letter, she thanks you for bringing her remains back to her homeland and asks you to bury her beneath the ivy tree in her courtyard. In return, she reveals the cause of the ancient power leak and rewards you with the Mystic Key, which opens the castle on the abandoned island.";

    [Header("Quest Link")]
    [Tooltip("QuestId của nhiệm vụ an nghỉ Natalie. Scene sẽ override giá trị này.")]
    [SerializeField] private int linkedQuestId = 33;

    [Tooltip("ObjectKey gửi lên API.")]
    [SerializeField] private string objectKey = "AbandonedCastle.IvyTree";

    [Tooltip("Tên hiển thị khi player đứng gần.")]
    [SerializeField] private string displayName = "Ivy Tree";

    private WorldInteractable _interactable;
    private bool _isInteracting;
    private bool _interacted;

    // Initializes internal component caches and dependencies for IvyTreeInteractable upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _interactable = GetComponent<WorldInteractable>();
    }

    // Performs startup initialization for IvyTreeInteractable on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        _interactable.ConfigureObject(
            key: objectKey,
            objectName: displayName,
            type: "Interact",
            linkedQuestId: linkedQuestId,
            delta: 1,
            radius: 3.5f
        );

        gameObject.SetActive(true);
        RefreshVisibility();
        WorldRuntimeEvents.QuestsChanged += RefreshVisibility;

    }


    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshVisibility;
    }

    // Executes start interaction operation.
    public void StartInteraction()
    {
        if (_isInteracting || _interacted) return;

        var questState = QuestUIManager.Instance != null ? QuestUIManager.Instance.GetQuestState(linkedQuestId) : null;
        var questStatus = questState != null ? questState.status : string.Empty;

        if (string.Equals(questStatus, "Completed", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(questStatus, "Claimed", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(questStatus, "InProgress", System.StringComparison.OrdinalIgnoreCase))
        {
            WorldRuntimeEvents.RaiseMessage("Accept Natalie's burial quest first.");
            return;
        }

        _isInteracting = true;
        WorldInteractionPromptRuntime.Hide();
        SendInteractApi();
    }

    // Executes send interact api operation.
    private void SendInteractApi()
    {
        if (!ApiClient.Instance.HasToken())
        {
            _isInteracting = false;
            WorldRuntimeEvents.RaiseMessage("Cannot bury Natalie while offline.");
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
                if (response?.Quest != null)
                    QuestUIManager.Instance?.ApplyServerQuestState(response.Quest);
                InventoryUIManager.RefreshAny(refreshStats: false);
                WorldRuntimeEvents.RaiseQuestsChanged();
                FinalizeAfterInteraction();
            },
            error =>
            {
                Debug.LogWarning($"[IvyTreeInteractable] InteractObject failed: {error.Message}");
                _isInteracting = false;
                WorldRuntimeEvents.RaiseMessage(error.Message);
            }
        );
    }

    // Executes finalize after interaction operation.
    private void FinalizeAfterInteraction()
    {
        _isInteracting = false;
        _interacted = true;

        gameObject.SetActive(true);

        var col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        _interactable.UpdateOverheadUI();
    }

    // Executes refresh visibility operation.
    private void RefreshVisibility()
    {
        gameObject.SetActive(true);

        if (QuestUIManager.Instance == null) return;

        var quests = QuestUIManager.Instance.GetMainQuests();
        bool shouldShowPrompt = false;
        foreach (var q in quests)
        {
            if (q.QuestId == linkedQuestId &&
                (QuestUIManager.IsStatus(q, "NotStarted") || QuestUIManager.IsStatus(q, "InProgress")))
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
