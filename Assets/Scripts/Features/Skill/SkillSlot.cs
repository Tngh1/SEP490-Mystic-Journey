using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MysticJourney.API.Endpoints; // Khai báo thư viện API của bạn

public class SkillSlot : MonoBehaviour, IDropHandler
{
    public int requiredLevel;
    public int playerLevel = 1;
    public int slotIndex;
    public Image equippedIcon;
    public GameObject lockImage;

    void Start()
    {
        if (playerLevel < requiredLevel)
            lockImage.SetActive(true);
        else
            lockImage.SetActive(false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        // 1. KIỂM TRA AN TOÀN KÉO THẢ: 
        // Nếu chuột thả ra nhưng không có vật thể nào đang được kéo -> Bỏ qua ngay
        if (eventData == null || eventData.pointerDrag == null) return;

        // 2. Kiểm tra cấp độ người chơi
        if (playerLevel < requiredLevel)
        {
            Debug.Log("Slot locked: player level too low.");
            return;
        }

        // 3. Lấy thông tin kỹ năng đang được kéo
        SkillItem droppedSkill = eventData.pointerDrag.GetComponent<SkillItem>();

        // 4. KIỂM TRA AN TOÀN DỮ LIỆU: 
        // Đảm bảo kéo đúng ô SkillItem và Skill đó đã được người chơi sở hữu
        if (droppedSkill != null && droppedSkill.serverData != null)
        {
            int targetPlayerSkillId = droppedSkill.serverData.PlayerSkillId;

            // Gọi API lên Server để xin phép trang bị
            SkillApi.Instance.EquipPlayerSkill(
                targetPlayerSkillId,
                true, // true = muốn trang bị
                slotIndex,
                onSuccess: (response) =>
                {
                    // Cập nhật giao diện an toàn
                    if (equippedIcon != null && droppedSkill.visualData != null)
                    {
                        equippedIcon.sprite = droppedSkill.visualData.skillIcon;
                        equippedIcon.color = Color.white;
                    }
                    Debug.Log($"Equipped successfully: {response.SkillName} to slot {slotIndex}!");
                },
                onError: (error) =>
                {
                    Debug.LogError("Server rejected equip: " + error.Message);
                }
            );
        }
        else
        {
            // Bỏ qua im lặng hoặc log cảnh báo nếu người chơi cố tình kéo kỹ năng bị khóa
            Debug.LogWarning("Kỹ năng này bị khóa hoặc không hợp lệ, không thể trang bị!");
        }
    }
}