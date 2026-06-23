using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.Core.Services;
using MysticJourney.API.Models.Response; // Thêm dòng này

public class SkillSlot : MonoBehaviour, IDropHandler
{
    // 👇 THÊM DÒNG NÀY: Khai báo Đài phát thanh (Sự kiện toàn cục)
    public static event System.Action<int, SkillData, PlayerSkillResponse> OnSkillEquipped;

    public int requiredLevel;
    public int playerLevel = 1;
    public int slotIndex;
    public Image equippedIcon;
    public GameObject lockImage;

    void Start()
    {
        if (playerLevel < requiredLevel) lockImage.SetActive(true);
        else lockImage.SetActive(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerDrag == null) return;
        if (playerLevel < requiredLevel) return;

        SkillItem droppedSkill = eventData.pointerDrag.GetComponent<SkillItem>();
        if (droppedSkill != null && droppedSkill.serverData != null)
        {
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
    (response) => // Tham số 4: Thành công
    {
        if (equippedIcon != null && droppedSkill.visualData != null)
        {
            equippedIcon.sprite = droppedSkill.visualData.skillIcon;
            equippedIcon.color = Color.white;
        }
        SkillSlot.BroadcastSkillEquipped(slotIndex, droppedSkill.visualData, response);
        Debug.Log($"Equipped successfully!");
    },
    (error) => // Tham số 5: Thất bại
    {
        Debug.LogError("Server rejected equip: " + error.Message);
    }
        );
        }
    }
    // Hàm này cho phép các file bên ngoài nhờ SkillSlot phát loa sự kiện
    public static void BroadcastSkillEquipped(int slotIndex, SkillData visualData, PlayerSkillResponse serverData)
    {
        OnSkillEquipped?.Invoke(slotIndex, visualData, serverData);
    }
}