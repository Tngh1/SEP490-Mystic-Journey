using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Models.Response;

public class HUDSkillManager : MonoBehaviour
{
    [Header("Gắn 3 cái Image (Icon) của HUD ngoài màn hình vào đây")]
    public Image[] hudSkillIcons; // Mảng chứa 3 ô (Slot_1, Slot_2, Slot_3)

    [Header("Master Data")]
    public SkillData[] allSkillsInGame; // Kéo file SkillData vào đây y như SkillPanelManager

    private void OnEnable()
    {
        SkillSlot.OnSkillEquipped += UpdateHUDIcon;
    }

    private void Start()
    {
        // 1. Ẩn tất cả icon lúc mới vào game (tránh bị nền trắng)
        if (hudSkillIcons != null)
        {
            foreach (var icon in hudSkillIcons)
            {
                if (icon != null)
                {
                    icon.sprite = null;
                    icon.color = new Color(1, 1, 1, 0); // Ẩn đi
                }
            }
        }

        Debug.Log("[HUDSkillManager] Start fetching skills...");
        // 2. Tự động tải danh sách skill đang trang bị để hiển thị lên HUD
        MysticJourney.API.Endpoints.SkillApi.Instance.GetMySkills(
            onSuccess: (response) =>
            {
                Debug.Log($"[HUDSkillManager] Fetch success. Total skills: {(response.Skills != null ? response.Skills.Count : 0)}");
                if (response.Skills == null || allSkillsInGame == null) return;
                
                foreach (var ps in response.Skills)
                {
                    if (ps.EquippedSlot.HasValue && ps.EquippedSlot.Value >= 0 && ps.EquippedSlot.Value < hudSkillIcons.Length)
                    {
                        var visual = System.Array.Find(allSkillsInGame, d => d.skillId == ps.SkillId);
                        if (visual != null && visual.skillIcon != null)
                        {
                            var icon = hudSkillIcons[ps.EquippedSlot.Value];
                            if (icon != null)
                            {
                                Debug.Log($"[HUDSkillManager] Loaded equipped skill {visual.name} at slot {ps.EquippedSlot.Value}");
                                icon.gameObject.SetActive(true);
                                icon.enabled = true;
                                icon.sprite = visual.skillIcon;
                                icon.color = Color.white; // Hiện rõ ảnh lên
                            }
                        }
                    }
                }
            },
            onError: (error) => 
            {
                Debug.LogError($"[HUDSkillManager] Failed to fetch skills: {error.Message}");
            }
        );
    }

    private void OnDisable()
    {
        SkillSlot.OnSkillEquipped -= UpdateHUDIcon;
    }

    private void UpdateHUDIcon(int slotIndex, SkillData vData, PlayerSkillResponse sData)
    {
        Debug.Log($"[HUDSkillManager] UpdateHUDIcon called with slotIndex: {slotIndex}");
        // Kiểm tra xem ô đó có hợp lệ trong mảng HUD không
        if (slotIndex >= 0 && slotIndex < hudSkillIcons.Length)
        {
            if (hudSkillIcons[slotIndex] != null && vData != null && vData.skillIcon != null)
            {
                Debug.Log($"[HUDSkillManager] Setting sprite for slot {slotIndex}: {vData.skillIcon.name}");
                hudSkillIcons[slotIndex].gameObject.SetActive(true);
                hudSkillIcons[slotIndex].enabled = true;
                hudSkillIcons[slotIndex].sprite = vData.skillIcon;
                hudSkillIcons[slotIndex].color = Color.white; // Hiện rõ ảnh lên
            }
            else
            {
                Debug.LogWarning($"[HUDSkillManager] Failed to set sprite. hudSkillIcon is null? {hudSkillIcons[slotIndex] == null}, vData null? {vData == null}, skillIcon null? {vData?.skillIcon == null}");
            }
        }
    }
}