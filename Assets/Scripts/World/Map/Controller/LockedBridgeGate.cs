using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.Core.Utilities;
using UnityEngine;

// Executes mono behaviour operation.
[RequireComponent(typeof(WorldInteractable))]
public class LockedBridgeGate : MonoBehaviour
{
    [Header("Gate Settings")]
    [Tooltip("Quest ID for using Natalie's Mystic Key at the bridge.")]
    [SerializeField] private int requiredQuestId = 34;
    [SerializeField] private string objectKey = "AbandonedCastle.LockedBridgeGate";
    [SerializeField] private string displayName = "Locked Bridge";
    [SerializeField] private Collider2D blockingCollider2D;
    [SerializeField] private Collider2D additionalBlockingCollider2D;
    [SerializeField] private Collider blockingCollider;
    [SerializeField] private GameObject visualBarrier;

    [Header("Messages")]
    [SerializeField] private string lockedMessage = "The bridge is locked. Accept the bridge quest and bring Natalie's Mystic Key.";
    [SerializeField] private string unlockedMessage = "You used the Mystic Key to unlock the bridge!";

    private bool _isUnlocked;
    private bool _isUnlocking;
    private WorldInteractable _interactable;

    // Initializes internal component caches and dependencies for LockedBridgeGate upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (blockingCollider2D == null) blockingCollider2D = GetComponent<Collider2D>();
        if (blockingCollider == null) blockingCollider = GetComponent<Collider>();
        _interactable = GetComponent<WorldInteractable>();
        _interactable.ConfigureObject(objectKey, displayName, "Interact", requiredQuestId, 1, 2.75f);
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        WorldRuntimeEvents.QuestsChanged += CheckUnlockState;
        CheckUnlockState();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        WorldRuntimeEvents.QuestsChanged -= CheckUnlockState;
    }

    // Executes check unlock state operation.
    public void CheckUnlockState()
    {
        var state = QuestUIManager.Instance?.GetQuestState(requiredQuestId);
        var unlocked = state != null &&
            (string.Equals(state.status, "Completed", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(state.status, "Claimed", System.StringComparison.OrdinalIgnoreCase));

        SetGateUnlocked(unlocked, showNotice: false);
    }

    // Executes set gate unlocked operation.
    private void SetGateUnlocked(bool unlocked, bool showNotice)
    {
        _isUnlocked = unlocked;
        if (blockingCollider2D != null) blockingCollider2D.enabled = !unlocked;
        if (additionalBlockingCollider2D != null) additionalBlockingCollider2D.enabled = !unlocked;
        if (blockingCollider != null) blockingCollider.enabled = !unlocked;
        if (visualBarrier != null) visualBarrier.SetActive(!unlocked);
        if (showNotice) WorldRuntimeEvents.RaiseMessage(unlockedMessage);
    }

    // Executes notify locked operation.
    public void NotifyLocked()
    {
        if (!_isUnlocked)
            WorldRuntimeEvents.RaiseMessage(lockedMessage);
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isUnlocked && other.CompareTag("Player")) NotifyLocked();
    }

    // Executes on trigger enter operation.
    private void OnTriggerEnter(Collider other)
    {
        if (!_isUnlocked && other.CompareTag("Player")) NotifyLocked();
    }

    // Executes on collision enter2 d operation.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isUnlocked && collision.gameObject.CompareTag("Player")) NotifyLocked();
    }

    // Executes on collision enter operation.
    private void OnCollisionEnter(Collision collision)
    {
        if (!_isUnlocked && collision.gameObject.CompareTag("Player")) NotifyLocked();
    }

    // Executes interact with gate operation.
    public void InteractWithGate()
    {
        CheckUnlockState();
        if (_isUnlocked || _isUnlocking) return;

        var state = QuestUIManager.Instance?.GetQuestState(requiredQuestId);
        if (state == null || !string.Equals(state.status, "InProgress", System.StringComparison.OrdinalIgnoreCase))
        {
            NotifyLocked();
            return;
        }

        if (!ApiClient.Instance.HasToken())
        {
            NotifyLocked();
            return;
        }

        _isUnlocking = true;
        WorldApi.Instance.InteractObject(
            objectKey,
            "Interact",
            requiredQuestId,
            1,
            response =>
            {
                _isUnlocking = false;
                if (response?.Quest != null)
                    QuestUIManager.Instance?.ApplyServerQuestState(response.Quest);
                InventoryUIManager.RefreshAny(refreshStats: false);
                WorldRuntimeEvents.RaiseQuestsChanged();
                SetGateUnlocked(true, showNotice: true);
            },
            error =>
            {
                _isUnlocking = false;
                Debug.LogWarning($"[LockedBridgeGate] Unlock failed: {error.Message}");
                WorldRuntimeEvents.RaiseMessage(error.Message);
            });
    }
}
