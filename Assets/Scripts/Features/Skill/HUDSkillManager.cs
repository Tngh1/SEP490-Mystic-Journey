using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Models.Response;

public class HUDSkillManager : MonoBehaviour
{
    [Header("Gắn 3 cái Image (Icon) của HUD ngoài màn hình vào đây")]
    public Image[] hudSkillIcons; // Mảng chứa 3 ô (Slot_1, Slot_2, Slot_3)

    private void OnEnable()
    {
        SkillSlot.OnSkillEquipped += UpdateHUDIcon;
    }

    private void OnDisable()
    {
        SkillSlot.OnSkillEquipped -= UpdateHUDIcon;
    }

    private void UpdateHUDIcon(int slotIndex, SkillData vData, PlayerSkillResponse sData)
    {
        // Kiểm tra xem ô đó có hợp lệ trong mảng HUD không
        if (slotIndex >= 0 && slotIndex < hudSkillIcons.Length)
        {
            if (hudSkillIcons[slotIndex] != null && vData != null && vData.skillIcon != null)
            {
                hudSkillIcons[slotIndex].sprite = vData.skillIcon;
                hudSkillIcons[slotIndex].color = Color.white; // Hiện rõ ảnh lên
            }
        }
    }
}