using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro; // BẮT BUỘC THÊM DÒNG NÀY CHO TEXTMESHPRO
using MysticJourney.API.Endpoints;
using MysticJourney.Core.Services;
using MysticJourney.API.Models.Response;

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

    // 👇 ĐÃ ĐỔI SANG BIẾN CỦA TEXTMESHPRO
    public TextMeshProUGUI cooldownText;

    private bool _isCooldown = false;
    private float _cooldownTimer = 0f;
    private float _cooldownDuration = 1f;

    private void OnEnable() => PlayerCombat.OnSkillCast += HandleSkillCast;
    private void OnDisable() => PlayerCombat.OnSkillCast -= HandleSkillCast;

    void Start()
    {
        int currentLevel = GameStateService.Instance != null ? GameStateService.Instance.PlayerLevel : playerLevel;
        if (lockImage != null) lockImage.SetActive(currentLevel < requiredLevel);

        if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;

        // Ẩn số đếm ngược khi mới vào game
        if (cooldownText != null) cooldownText.text = "";
    }

    private void Update()
    {
        if (_isCooldown && cooldownOverlay != null)
        {
            _cooldownTimer -= Time.deltaTime;
            cooldownOverlay.fillAmount = _cooldownTimer / _cooldownDuration;

            // HIỂN THỊ SỐ ĐẾM NGƯỢC
            if (cooldownText != null)
            {
                cooldownText.text = Mathf.CeilToInt(_cooldownTimer).ToString();
            }

            if (_cooldownTimer <= 0)
            {
                _isCooldown = false;
                cooldownOverlay.fillAmount = 0f;

                // Ẩn số đếm ngược khi hồi xong
                if (cooldownText != null) cooldownText.text = "";
            }
        }
    }

    private void HandleSkillCast(int castedSlotIndex, float cooldownTime)
    {
        if (this.slotIndex == castedSlotIndex && cooldownOverlay != null)
        {
            StartCooldown(cooldownTime);
        }
    }

    public void StartCooldown(float cooldownTime)
    {
        _isCooldown = true;
        _cooldownDuration = cooldownTime;
        _cooldownTimer = cooldownTime;
        
        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = 1f;
        }

        if (cooldownText != null) cooldownText.text = Mathf.CeilToInt(cooldownTime).ToString();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_isCooldown)
        {
            Debug.LogWarning("Cannot equip: skill slot is currently on cooldown.");
            // OPTIONAL: Could show an ingame UI alert here
            return;
        }

        if (eventData == null || eventData.pointerDrag == null) return;

        SkillItem droppedSkill = eventData.pointerDrag.GetComponent<SkillItem>();
        if (droppedSkill != null && droppedSkill.serverData != null)
        {
            // --- FIX: CHỐNG TRANG BỊ TRÙNG LẶP KỸ NĂNG ---
            var allSlots = FindObjectsByType<SkillSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var s in allSlots)
            {
                if (s != this && s.equippedIcon != null && s.equippedIcon.sprite != null && droppedSkill.visualData != null)
                {
                    if (s.equippedIcon.sprite == droppedSkill.visualData.skillIcon)
                    {
                        Debug.LogWarning("Kỹ năng này đã được trang bị ở ô khác!");
                        return; // Chặn không cho trang bị trùng
                    }
                }
            }
            var playerClass = GameStateService.Instance?.PlayerClass ?? "";
            var requiredClass = droppedSkill.visualData != null ? droppedSkill.visualData.classRequirement : "";

            bool isAllClass = string.IsNullOrWhiteSpace(requiredClass) || requiredClass.Equals("All", System.StringComparison.OrdinalIgnoreCase);
            bool isMyClass = !string.IsNullOrWhiteSpace(playerClass) && requiredClass.Equals(playerClass, System.StringComparison.OrdinalIgnoreCase);
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

                    // Auto-complete EquipSkill quest
                    var qm = FindFirstObjectByType<QuestManager>();
                    if (qm != null)
                    {
                        qm.AutoCompleteEquipSkillQuest();
                    }
                },
                (error) => { Debug.LogError("Server rejected equip: " + error.Message); }
            );
        }
    }

    public static void BroadcastSkillEquipped(int slotIndex, SkillData visualData, PlayerSkillResponse serverData)
    {
        OnSkillEquipped?.Invoke(slotIndex, visualData, serverData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Cho phép click vào HUD để tung chiêu
        if (equippedIcon != null && equippedIcon.sprite != null)
        {
            var combat = PlayerEntity.Instance?.GetComponent<PlayerCombat>();
            if (combat != null)
            {
                combat.RequestCastSkillBySlot(slotIndex);
            }
        }
    }
}