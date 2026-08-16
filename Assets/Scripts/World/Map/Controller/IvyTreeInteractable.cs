using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;

[RequireComponent(typeof(WorldInteractable))]
public class IvyTreeInteractable : MonoBehaviour
{
    private const string CompletionMessage = "In the letter, she thanks you for bringing her remains back to her homeland and asks you to bury her beneath the ivy tree in her courtyard. In return, she reveals the cause of the ancient power leak and rewards you with the Mystic Key, which opens the castle on the abandoned island.";

    [Header("Quest Link")]
    // Default trỏ tới quest "[Chapter 4] Lay Natalie to Rest" (AbandonedCastle,
    // ObjectiveTarget = "Ivy Tree"). Không ghi số quest vào tooltip vì số lệch mỗi lần chèn quest.
    [Tooltip("QuestId của nhiệm vụ an nghỉ Natalie. Scene sẽ override giá trị này.")]
    [SerializeField] private int linkedQuestId = 33;

    [Tooltip("ObjectKey gửi lên API.")]
    [SerializeField] private string objectKey = "AbandonedCastle.IvyTree";

    [Tooltip("Tên hiển thị khi player đứng gần.")]
    [SerializeField] private string displayName = "Ivy Tree";

    private WorldInteractable _interactable;
    private bool _isInteracting;
    private bool _interacted;

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

    }


    private void OnDestroy()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshVisibility;
    }

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
    }

    private void RefreshVisibility()
    {
        // Luôn giữ GameObject của cây active để môi trường thiên nhiên hiển thị đầy đủ
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
