using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.Core.Services;
using MysticJourney.API.Models.Response;

// Executes i pointer click handler operation.
public class SkillSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    public static event System.Action<int, SkillData, PlayerSkillResponse> OnSkillEquipped;

    [Header("Master Data")]
    public SkillData[] allSkillsInGame;

    [Header("Slot Settings")]
    public int requiredLevel;
    public int playerLevel = 1;
    public int slotIndex;
    public Image equippedIcon;
    public GameObject lockImage;

    [Header("Cooldown UI")]
    public Image cooldownOverlay;
    public TextMeshProUGUI cooldownText;

    private bool _isCooldown = false;
    private float _cooldownTimer = 0f;
    private float _cooldownDuration = 1f;

    private TextMeshProUGUI hintLabel;

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        PlayerCombat.OnSkillCast += HandleSkillCast;
        OnSkillEquipped += HandleSkillEquipped;
        RefreshLockState();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDisable()
    {
        PlayerCombat.OnSkillCast -= HandleSkillCast;
        OnSkillEquipped -= HandleSkillEquipped;
    }

    // Executes refresh lock state operation.
    public void RefreshLockState()
    {
        if (slotIndex == 0) requiredLevel = 1;
        else if (slotIndex == 1) requiredLevel = 5;
        else if (slotIndex == 2) requiredLevel = 10;

        int currentLevel = GameStateService.Instance != null ? GameStateService.Instance.PlayerLevel : playerLevel;
        bool isLocked = currentLevel < requiredLevel;

        if (lockImage != null)
        {
            lockImage.SetActive(isLocked);

            var lockImgComp = lockImage.GetComponent<Image>();
            if (lockImgComp != null)
            {
                lockImgComp.enabled = true;
                lockImgComp.color = Color.white;
            }
        }

        if (isLocked)
        {
            if (equippedIcon != null)
            {
                equippedIcon.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        RefreshHintLabel(isLocked);
    }

    // Executes refresh hint label operation.
    private void RefreshHintLabel(bool isLocked)
    {
        bool hasSkill = equippedIcon != null && equippedIcon.sprite != null;
        if (!isLocked && hasSkill)
        {
            if (hintLabel != null) hintLabel.gameObject.SetActive(false);
            return;
        }

        if (hintLabel == null)
        {
            var go = new GameObject("HintLabel", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0.34f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            hintLabel = go.AddComponent<TextMeshProUGUI>();
            hintLabel.alignment = TextAlignmentOptions.Center;
            hintLabel.enableAutoSizing = true;
            hintLabel.fontSizeMin = 8f;
            hintLabel.fontSizeMax = 18f;
            hintLabel.raycastTarget = false;
            hintLabel.outlineWidth = 0.2f;
            hintLabel.outlineColor = new Color32(0, 0, 0, 255);
        }

        hintLabel.gameObject.SetActive(true);
        hintLabel.text = isLocked ? $"Lv {requiredLevel}" : "Empty";
        hintLabel.color = isLocked ? new Color(1f, 0.83f, 0.30f) : new Color(0.78f, 0.78f, 0.78f);
    }

    // Executes handle skill equipped operation.
    private void HandleSkillEquipped(int equippedSlotIndex, SkillData vData, PlayerSkillResponse sData)
    {
        if (equippedSlotIndex != this.slotIndex) return;

        RefreshLockState();

        int currentLevel = GameStateService.Instance != null ? GameStateService.Instance.PlayerLevel : playerLevel;
        if (currentLevel >= requiredLevel)
        {
            if (equippedIcon != null && vData != null && vData.skillIcon != null)
            {
                equippedIcon.sprite = vData.skillIcon;
                equippedIcon.color = _isCooldown ? new Color(0.35f, 0.35f, 0.35f, 1f) : Color.white;
                equippedIcon.gameObject.SetActive(true);
                equippedIcon.enabled = true;
                RefreshHintLabel(false);
            }
        }

        if (sData != null && !string.IsNullOrEmpty(sData.NextAvailableTime) &&
            System.DateTime.TryParse(sData.NextAvailableTime,
                                     System.Globalization.CultureInfo.InvariantCulture,
                                     System.Globalization.DateTimeStyles.AdjustToUniversal,
                                     out System.DateTime nextTime))
        {
            float remaining = (float)(nextTime - System.DateTime.UtcNow).TotalSeconds;
            if (remaining > 0f)
            {
                StartCooldown(remaining);
                return;
            }
        }

        _isCooldown = false;
        _cooldownTimer = 0f;
        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
        if (cooldownText != null) cooldownText.text = "";
        if (equippedIcon != null && currentLevel >= requiredLevel)
        {
            equippedIcon.color = Color.white;
        }
    }

    void Start()
    {
        RefreshLockState();

        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = 0f;
            cooldownOverlay.color = new Color(0f, 0f, 0f, 0.75f);
        }

        if (cooldownText != null)
        {
            cooldownText.text = "";
            cooldownText.alignment = TextAlignmentOptions.Center;
        }
    }

    // Per-frame update loop for SkillSlot.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (_isCooldown)
        {
            _cooldownTimer -= Time.deltaTime;

            if (cooldownOverlay != null)
            {
                // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
                cooldownOverlay.fillAmount = Mathf.Clamp01(_cooldownTimer / _cooldownDuration);
                cooldownOverlay.color = new Color(0f, 0f, 0f, 0.75f);
            }

            if (cooldownText != null)
            {
                int remainingInt = Mathf.CeilToInt(_cooldownTimer);
                cooldownText.text = $"<size=150%><color=#FFE042><b>{remainingInt}</b></color></size>";
            }

            if (equippedIcon != null)
            {
                equippedIcon.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            }

            if (_cooldownTimer <= 0)
            {
                _isCooldown = false;
                if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
                if (cooldownText != null) cooldownText.text = "";

                if (equippedIcon != null)
                {
                    equippedIcon.color = Color.white;
                }
            }
        }
    }

    // Executes handle skill cast operation.
    private void HandleSkillCast(int castedSlotIndex, float cooldownTime)
    {
        if (this.slotIndex == castedSlotIndex)
        {
            StartCooldown(cooldownTime);
        }
    }

    // Executes start cooldown operation.
    public void StartCooldown(float cooldownTime)
    {
        _isCooldown = true;
        _cooldownDuration = cooldownTime;
        _cooldownTimer = cooldownTime;

        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = 1f;
            cooldownOverlay.color = new Color(0f, 0f, 0f, 0.75f);
        }

        if (equippedIcon != null)
        {
            equippedIcon.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        }

        if (cooldownText != null)
        {
            int remainingInt = Mathf.CeilToInt(cooldownTime);
            cooldownText.text = $"<size=150%><color=#FFE042><b>{remainingInt}</b></color></size>";
        }
    }

    // Executes on drop operation.
    public void OnDrop(PointerEventData eventData)
    {
        int currentLevel = GameStateService.Instance != null ? GameStateService.Instance.PlayerLevel : playerLevel;
        if (currentLevel < requiredLevel)
        {
            Debug.LogWarning($"Cannot equip: skill slot {slotIndex} requires Player Level {requiredLevel}.");
            return;
        }

        if (_isCooldown)
        {
            Debug.LogWarning("Cannot equip: skill slot is currently on cooldown.");
            return;
        }

        SkillItem droppedSkill = null;
        if (eventData != null && eventData.pointerDrag != null)
        {
            droppedSkill = eventData.pointerDrag.GetComponent<SkillItem>() ??
                           eventData.pointerDrag.GetComponentInParent<SkillItem>();
        }

        if (droppedSkill == null)
        {
            droppedSkill = SkillItem.CurrentDraggedItem;
        }

        if (droppedSkill == null)
        {
            Debug.LogWarning($"[SkillSlot] Drop on slot {slotIndex} did not contain a SkillItem payload.");
            return;
        }
        if (droppedSkill != null && droppedSkill.serverData != null)
        {
            var allSlots = FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in allSlots)
            {
                if (s != this && s.equippedIcon != null && s.equippedIcon.sprite != null && droppedSkill.visualData != null)
                {
                    if (s.equippedIcon.sprite == droppedSkill.visualData.skillIcon)
                    {
                        Debug.LogWarning("Kỹ năng này đã được trang bị ở ô khác!");
                        return;
                    }
                }
            }
            // Supported player classes: Knight, Archer, or Mage; the class selects base stats, compatible skills, skins, and combat scaling.
            var playerClass = GameStateService.Instance?.PlayerClass ?? "";
            var requiredClass = droppedSkill.visualData != null ? droppedSkill.visualData.classRequirement : "";

            bool isAllClass = string.IsNullOrWhiteSpace(requiredClass) || requiredClass.Equals("All", System.StringComparison.OrdinalIgnoreCase);
            bool isMyClass = string.IsNullOrWhiteSpace(playerClass) ||
                             requiredClass.Equals(playerClass, System.StringComparison.OrdinalIgnoreCase);
            if (!isAllClass && !isMyClass)
            {
                Debug.LogWarning($"Cannot equip: skill requires class {requiredClass}.");
                return;
            }

            int targetPlayerSkillId = droppedSkill.serverData.PlayerSkillId;

            SkillApi.Instance.EquipPlayerSkill(
                targetPlayerSkillId,
                true,
                slotIndex,
                (response) =>
                {
                    if (equippedIcon != null && droppedSkill.visualData != null)
                    {
                        equippedIcon.sprite = droppedSkill.visualData.skillIcon;
                        equippedIcon.color = Color.white;
                    }
                    SkillSlot.BroadcastSkillEquipped(slotIndex, droppedSkill.visualData, response);

                    var qm = FindFirstObjectByType<QuestUIManager>();
                    if (qm != null)
                    {
                        qm.AutoCompleteEquipSkillQuest();
                    }
                },
                (error) => { Debug.LogError("Server rejected equip: " + error.Message); }
            );
        }
    }

    // Executes broadcast skill equipped operation.
    public static void BroadcastSkillEquipped(int slotIndex, SkillData visualData, PlayerSkillResponse serverData)
    {
        OnSkillEquipped?.Invoke(slotIndex, visualData, serverData);
    }

    // Executes on pointer click operation.
    public void OnPointerClick(PointerEventData eventData)
    {
        int currentLevel = GameStateService.Instance != null ? GameStateService.Instance.PlayerLevel : playerLevel;
        if (currentLevel < requiredLevel)
        {
            Debug.LogWarning($"Slot {slotIndex} is locked!");
            return;
        }

        if (equippedIcon != null && equippedIcon.sprite != null)
        {
            var combat = NetworkPlayer.Local != null
                ? NetworkPlayer.Local.GetComponent<PlayerCombat>()
                : PlayerEntity.Instance?.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                combat.RequestCastSkillBySlot(slotIndex);
            }
        }
    }
}
