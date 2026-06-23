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
        // Dọn dẹp UI cũ được dời vào trong PopulateUI để tránh lỗi bất đồng bộ
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
        if (skillItemPrefab == null || contentArea == null) return;

        // 1. DỌN DẸP UI TRIỆT ĐỂ (Chống bug nhân đôi hình)
        foreach (Transform child in contentArea)
        {
            Destroy(child.gameObject);
            child.SetParent(null); // Tách Object ra khỏi danh sách ngay lập tức
        }

        // 2. LẤY CLASS HIỆN TẠI CỦA NGƯỜI CHƠI
        string playerClass = GameStateService.Instance?.PlayerClass ?? "";

        var sortedSkillList = new List<(SkillData visual, PlayerSkillResponse server)>();

        foreach (var data in allSkillsInGame)
        {
            // 3. TÍNH NĂNG LỌC: Bỏ qua các kỹ năng không thuộc Class của mình (hoặc không phải All)
            bool isAllClass = string.IsNullOrWhiteSpace(data.classRequirement) || data.classRequirement.Equals("All", System.StringComparison.OrdinalIgnoreCase);
            bool isMyClass = !string.IsNullOrWhiteSpace(playerClass) && data.classRequirement.Equals(playerClass, System.StringComparison.OrdinalIgnoreCase);

            if (!isAllClass && !isMyClass)
                continue; // ⬅️ Nếu không hợp hệ, lập tức bỏ qua, không hiển thị lên UI

            PlayerSkillResponse owned = null;
            if (playerSkills != null)
            {
                owned = playerSkills.Find(ps => ps.SkillId == data.skillId);
            }
            sortedSkillList.Add((data, owned));
        }

        // 4. THỰC HIỆN SẮP XẾP DANH SÁCH (Đã lọc)
        sortedSkillList.Sort((a, b) =>
        {
            bool aUnlocked = a.server != null;
            bool bUnlocked = b.server != null;

            if (aUnlocked && !bUnlocked) return -1;
            if (!aUnlocked && bUnlocked) return 1;
            return a.visual.skillId.CompareTo(b.visual.skillId);
        });

        // 5. Tiến hành vẽ giao diện 
        foreach (var item in sortedSkillList)
        {
            GameObject newSkillObj = Instantiate(skillItemPrefab, contentArea);
            SkillItem itemScript = newSkillObj.GetComponent<SkillItem>();
            itemScript.Setup(item.visual, item.server);
        }

        // =========================================================
        // (Phần code xử lý skillSlots của bạn ở bên dưới GIỮ NGUYÊN)
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

            int pLevel = GameStateService.Instance?.PlayerLevel ?? 1;
            foreach (var s in skillSlots)
            {
                if (s == null) continue;
                if (s.lockImage != null)
                    s.lockImage.SetActive(pLevel < s.requiredLevel);
            }

            if (playerSkills != null)
            {
                foreach (var ps in playerSkills)
                {
                    if (ps.EquippedSlot.HasValue && ps.EquippedSlot.Value >= 0 && ps.EquippedSlot.Value < skillSlots.Length)
                    {
                        var slot = skillSlots[ps.EquippedSlot.Value];
                        var visual = System.Array.Find(allSkillsInGame, d => d.skillId == ps.SkillId);
                        if (visual != null && slot != null && slot.equippedIcon != null)
                        {
                            slot.equippedIcon.sprite = visual.skillIcon;
                            slot.equippedIcon.color = Color.white;

                            // 👇 THÊM DÒNG NÀY: Phát loa để nạp dữ liệu cho HUD & Nhân vật lúc mới vào game
                            SkillSlot.BroadcastSkillEquipped(ps.EquippedSlot.Value, visual, ps);
                        }
                    }
                }
            }
        }
    }
}