using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
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
    public TextMeshProUGUI cooldownText;

    private bool _isCooldown = false;
    private float _cooldownTimer = 0f;
    private float _cooldownDuration = 1f;

    // Nhãn nhỏ dưới đáy ô: "Lv 5" khi bị khóa theo level, "Empty" khi mở nhưng chưa
    // trang bị gì. Tạo bằng code để không phải sửa prefab/scene, và tách khỏi
    // cooldownText (nằm giữa ô, trùng vị trí ổ khóa).
    private TextMeshProUGUI hintLabel;

    private void OnEnable()
    {
        PlayerCombat.OnSkillCast += HandleSkillCast;
        OnSkillEquipped += HandleSkillEquipped;
        RefreshLockState();
    }

    private void OnDisable()
    {
        PlayerCombat.OnSkillCast -= HandleSkillCast;
        OnSkillEquipped -= HandleSkillEquipped;
    }

    public void RefreshLockState()
    {
        // Gán Level yêu cầu chuẩn theo slotIndex (0 = Lv 1, 1 = Lv 5, 2 = Lv 10)
        if (slotIndex == 0) requiredLevel = 1;
        else if (slotIndex == 1) requiredLevel = 5;
        else if (slotIndex == 2) requiredLevel = 10;

        int currentLevel = GameStateService.Instance != null ? GameStateService.Instance.PlayerLevel : playerLevel;
        bool isLocked = currentLevel < requiredLevel;

        if (lockImage != null)
        {
            lockImage.SetActive(isLocked);

            // Đảm bảo Image ổ khóa bật hiển thị rõ ràng trên UI
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
                equippedIcon.color = new Color(1f, 1f, 1f, 0f); // Ẩn icon nếu slot bị khóa
            }
        }

        RefreshHintLabel(isLocked);
    }

    // Ô số 1 mở từ Lv 1 nên không có ổ khóa: người chơi mới (chưa có skill nào, quest
    // "Equip Your First Skill" còn NotStarted) chỉ thấy một ô vuông trống cạnh hai ô
    // khóa → không biết mình đã có skill chưa hay phải làm gì. Ghi thẳng trạng thái
    // lên ô: "Lv 5" nếu khóa theo level, "Empty" nếu mở nhưng chưa trang bị.
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
                // RefreshLockState() ở trên chạy TRƯỚC khi có sprite nên vẫn coi ô này là
                // "Empty"; gọi lại để tắt nhãn ngay sau khi icon vào ô.
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

        // Skill không còn cooldown → khôi phục icon sáng và xóa overlay
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
            // Đặt màu lớp phủ đếm ngược thành màu đen mờ (75% alpha) để làm mờ icon phía sau khi hồi chiêu
            cooldownOverlay.color = new Color(0f, 0f, 0f, 0.75f);
        }

        if (cooldownText != null)
        {
            cooldownText.text = "";
            cooldownText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void Update()
    {
        if (_isCooldown)
        {
            _cooldownTimer -= Time.deltaTime;

            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = Mathf.Clamp01(_cooldownTimer / _cooldownDuration);
                // Giữ màu xám mờ đậm đè lên icon kỹ năng
                cooldownOverlay.color = new Color(0f, 0f, 0f, 0.75f);
            }

            // Hiển thị số giây hồi chiêu bằng màu VÀNG KIM NỔI BẬT với kích thước lớn
            if (cooldownText != null)
            {
                int remainingInt = Mathf.CeilToInt(_cooldownTimer);
                cooldownText.text = $"<size=150%><color=#FFE042><b>{remainingInt}</b></color></size>";
            }

            // Làm mờ icon kỹ năng phía dưới trong lúc chờ hồi chiêu
            if (equippedIcon != null)
            {
                equippedIcon.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            }

            if (_cooldownTimer <= 0)
            {
                _isCooldown = false;
                if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
                if (cooldownText != null) cooldownText.text = "";

                // Trả lại độ sáng 100% cho icon kỹ năng khi hồi chiêu xong
                if (equippedIcon != null)
                {
                    equippedIcon.color = Color.white;
                }
            }
        }
    }

    private void HandleSkillCast(int castedSlotIndex, float cooldownTime)
    {
        if (this.slotIndex == castedSlotIndex)
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

        // A ScrollRect can retain a child/scroll object as pointerDrag after the
        // gesture changes into a skill drag. Use the explicit payload as fallback.
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
        int currentLevel = GameStateService.Instance != null ? GameStateService.Instance.PlayerLevel : playerLevel;
        if (currentLevel < requiredLevel)
        {
            Debug.LogWarning($"Slot {slotIndex} is locked!");
            return;
        }

        // Cho phép click vào HUD để tung chiêu
        if (equippedIcon != null && equippedIcon.sprite != null)
        {
            // In multiplayer PlayerEntity.Instance can briefly point at a proxy when
            // another avatar spawns. HUD input must always target this client's avatar.
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
