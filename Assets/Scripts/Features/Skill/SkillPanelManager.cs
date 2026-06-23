using System.Collections.Generic;
using UnityEngine;
using MysticJourney.Core.Services;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

public class SkillPanelManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject skillItemPrefab;
    public Transform contentArea;

    [Header("Master Data")]
    // Kéo toàn bộ file SkillData từ thư mục ScriptableObjects vào mảng này
    public SkillData[] allSkillsInGame;

    [Header("Slots")]
    public SkillSlot[] skillSlots; // assign 3 slots in inspector

    private void OnEnable()
    {
        // Tự động gọi API mỗi khi Panel này được SetActive(true)
        RefreshSkillList();
    }

    public void RefreshSkillList()
    {
        // Dọn dẹp UI cũ trước khi tải mới
        foreach (Transform child in contentArea)
        {
            Destroy(child.gameObject);
        }

        // Gọi API từ class SkillApi của bạn
        SkillApi.Instance.GetMySkills(
            onSuccess: (response) =>
            {
                PopulateUI(response.Skills);
            },
            onError: (error) =>
            {
                Debug.LogError($"[UI] Lỗi tải kỹ năng: {error.Message}");
            }
        );
    }

    private void PopulateUI(List<PlayerSkillResponse> playerSkills)
    {
        // 1. Tạo một danh sách ảo để ghép nối dữ liệu tĩnh (Visual) và dữ liệu thật (Server)
        var sortedSkillList = new List<(SkillData visual, PlayerSkillResponse server)>();

        foreach (var data in allSkillsInGame)
        {
            PlayerSkillResponse owned = null;
            if (playerSkills != null)
            {
                owned = playerSkills.Find(ps => ps.SkillId == data.skillId);
            }
            sortedSkillList.Add((data, owned));
        }

        // 2. THỰC HIỆN SẮP XẾP DANH SÁCH
        sortedSkillList.Sort((a, b) =>
        {
            bool aUnlocked = a.server != null;
            bool bUnlocked = b.server != null;

            // Nếu A mở khóa, B khóa -> A xếp trước
            if (aUnlocked && !bUnlocked) return -1;

            // Nếu A khóa, B mở khóa -> B xếp trước
            if (!aUnlocked && bUnlocked) return 1;

            // Nếu cùng mở hoặc cùng khóa, sắp xếp theo thứ tự ID để danh sách không bị lộn xộn
            return a.visual.skillId.CompareTo(b.visual.skillId);
        });

        // Qua met voi Phat
        // 3. Tiến hành vẽ giao diện dựa trên danh sách đã sắp xếp xong
        foreach (var item in sortedSkillList)
        {
            GameObject newSkillObj = Instantiate(skillItemPrefab, contentArea);
            SkillItem itemScript = newSkillObj.GetComponent<SkillItem>();
            itemScript.Setup(item.visual, item.server);
        }

        // =========================================================
        // (Phần code xử lý skillSlots của bạn ở bên dưới GIỮ NGUYÊN)
        // Reset slots visuals
        if (skillSlots != null)
        {
            foreach (var s in skillSlots)
            {
                if (s != null && s.equippedIcon != null)
                {
                    s.equippedIcon.sprite = null;
                    s.equippedIcon.color = new Color(1, 1, 1, 0); // hide
                }
            }

            // Update slot lock visuals according to current player level
            int playerLevel = GameStateService.Instance?.PlayerLevel ?? 1;
            foreach (var s in skillSlots)
            {
                if (s == null) continue;
                if (s.lockImage != null)
                    s.lockImage.SetActive(playerLevel < s.requiredLevel);
            }

            // Fill slots according to playerSkills equipped info
            if (playerSkills != null)
            {
                foreach (var ps in playerSkills)
                {
                    if (ps.EquippedSlot.HasValue && ps.EquippedSlot.Value >= 0 && ps.EquippedSlot.Value < skillSlots.Length)
                    {
                        var slot = skillSlots[ps.EquippedSlot.Value];
                        // find visual icon from master data
                        var visual = System.Array.Find(allSkillsInGame, d => d.skillId == ps.SkillId);
                        if (visual != null && slot != null && slot.equippedIcon != null)
                        {
                            slot.equippedIcon.sprite = visual.skillIcon;
                            slot.equippedIcon.color = Color.white;
                        }
                    }
                }
            }
        }
    }
}