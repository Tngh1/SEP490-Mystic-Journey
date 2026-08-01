using System.Collections;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using UnityEngine;

[RequireComponent(typeof(WorldInteractable))]
public class OriginTreeInteractable : MonoBehaviour
{
    [Header("Quest Link")]
    [SerializeField] private int linkedQuestId = 45;
    [SerializeField] private string objectKey = "ElfForest.OriginTree";
    [SerializeField] private string displayName = "Origin Tree";

    [Header("Healing Visual")]
    [SerializeField] private float healingDuration = 2.5f;
    [SerializeField] private Color healedColor = new Color(0.72f, 1f, 0.72f, 1f);
    [SerializeField] private float pulseScale = 1.04f;

    private WorldInteractable _interactable;
    private SpriteRenderer _treeRenderer;
    private Vector3 _baseScale;
    private bool _isHealing;
    private bool _healed;

    private void Awake()
    {
        _interactable = GetComponent<WorldInteractable>();
        _treeRenderer = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
    }

    private void Start()
    {
        _interactable.ConfigureQuestItem(objectKey, displayName, linkedQuestId, 1, 3.5f);
        RefreshVisibility();
        WorldRuntimeEvents.QuestsChanged += RefreshVisibility;
    }

    private void OnDestroy()
    {
        WorldRuntimeEvents.QuestsChanged -= RefreshVisibility;
    }

    public void StartHeal()
    {
        if (_isHealing || _healed) return;

        var questState = QuestManager.Instance?.GetQuestState(linkedQuestId);
        if (questState == null ||
            !string.Equals(questState.status, "InProgress", System.StringComparison.OrdinalIgnoreCase))
        {
            WorldRuntimeEvents.RaiseMessage("Speak with Lyra and accept the healing rite first.");
            return;
        }

        if (!ApiClient.Instance.HasToken())
        {
            WorldRuntimeEvents.RaiseMessage("The healing rite requires a connection to the world.");
            return;
        }

        _isHealing = true;
        WorldInteractionPromptRuntime.Hide();
        WorldApi.Instance.InteractObject(
            objectKey,
            "Interact",
            linkedQuestId,
            1,
            response =>
            {
                if (response?.Quest != null)
                    QuestManager.Instance?.ApplyServerQuestState(response.Quest);
                InventoryManager.RefreshAny(refreshStats: false);
                WorldRuntimeEvents.RaiseQuestsChanged();
                StartCoroutine(HealingSequence());
            },
            error =>
            {
                _isHealing = false;
                Debug.LogWarning($"[OriginTreeInteractable] InteractObject failed: {error.Message}");
                WorldRuntimeEvents.RaiseMessage(error.Message);
                RefreshVisibility();
            });
    }

    private IEnumerator HealingSequence()
    {
        var startColor = _treeRenderer != null ? _treeRenderer.color : Color.white;
        var elapsed = 0f;
        while (elapsed < healingDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / healingDuration);
            var pulse = Mathf.Sin(t * Mathf.PI) * (pulseScale - 1f);
            transform.localScale = _baseScale * (1f + pulse);
            if (_treeRenderer != null)
                _treeRenderer.color = Color.Lerp(startColor, healedColor, t);
            yield return null;
        }

        transform.localScale = _baseScale;
        ApplyHealedVisual();
        _isHealing = false;
        _healed = true;
        SetColliderEnabled(false);
        _interactable.UpdateOverheadUI();
        WorldRuntimeEvents.RaiseMessage("The Origin Tree is healing. Talk to Lyra.");
    }

    private void RefreshVisibility()
    {
        if (QuestManager.Instance == null) return;

        var state = QuestManager.Instance.GetQuestState(linkedQuestId);
        var status = state?.status ?? string.Empty;
        _healed = string.Equals(status, "Completed", System.StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(status, "Claimed", System.StringComparison.OrdinalIgnoreCase);

        if (_healed)
            ApplyHealedVisual();

        var inProgress = string.Equals(status, "InProgress", System.StringComparison.OrdinalIgnoreCase);
        SetColliderEnabled(inProgress && !_healed && !_isHealing);
        _interactable.UpdateOverheadUI();
    }

    private void ApplyHealedVisual()
    {
        transform.localScale = _baseScale;
        if (_treeRenderer != null)
            _treeRenderer.color = healedColor;
    }

    private void SetColliderEnabled(bool enabled)
    {
        var col2D = GetComponent<Collider2D>();
        if (col2D != null) col2D.enabled = enabled;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = enabled;
    }
}
