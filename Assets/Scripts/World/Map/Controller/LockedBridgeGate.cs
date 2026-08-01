using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.Core.Utilities;
using UnityEngine;

/// <summary>
/// Server-authoritative gate for the bridge to the deserted island.
/// Quest state and Mystic Key consumption are owned by the backend so unlocks do not leak between accounts.
/// </summary>
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

    private void Awake()
    {
        if (blockingCollider2D == null) blockingCollider2D = GetComponent<Collider2D>();
        if (blockingCollider == null) blockingCollider = GetComponent<Collider>();
        _interactable = GetComponent<WorldInteractable>();
        _interactable.ConfigureObject(objectKey, displayName, "Interact", requiredQuestId, 1, 2.75f);
    }

    private void OnEnable()
    {
        WorldRuntimeEvents.QuestsChanged += CheckUnlockState;
        CheckUnlockState();
    }

    private void OnDisable()
    {
        WorldRuntimeEvents.QuestsChanged -= CheckUnlockState;
    }

    public void CheckUnlockState()
    {
        var state = QuestManager.Instance?.GetQuestState(requiredQuestId);
        var unlocked = state != null &&
            (string.Equals(state.status, "Completed", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(state.status, "Claimed", System.StringComparison.OrdinalIgnoreCase));

        SetGateUnlocked(unlocked, showNotice: false);
    }

    private void SetGateUnlocked(bool unlocked, bool showNotice)
    {
        _isUnlocked = unlocked;
        if (blockingCollider2D != null) blockingCollider2D.enabled = !unlocked;
        if (additionalBlockingCollider2D != null) additionalBlockingCollider2D.enabled = !unlocked;
        if (blockingCollider != null) blockingCollider.enabled = !unlocked;
        if (visualBarrier != null) visualBarrier.SetActive(!unlocked);
        if (showNotice) WorldRuntimeEvents.RaiseMessage(unlockedMessage);
    }

    public void NotifyLocked()
    {
        if (!_isUnlocked)
            WorldRuntimeEvents.RaiseMessage(lockedMessage);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isUnlocked && other.CompareTag("Player")) NotifyLocked();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isUnlocked && other.CompareTag("Player")) NotifyLocked();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isUnlocked && collision.gameObject.CompareTag("Player")) NotifyLocked();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_isUnlocked && collision.gameObject.CompareTag("Player")) NotifyLocked();
    }

    public void InteractWithGate()
    {
        CheckUnlockState();
        if (_isUnlocked || _isUnlocking) return;

        var state = QuestManager.Instance?.GetQuestState(requiredQuestId);
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
                    QuestManager.Instance?.ApplyServerQuestState(response.Quest);
                InventoryManager.RefreshAny(refreshStats: false);
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
