using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

public class SkillPopup : MonoBehaviour
{
    [Header("UI References")]
    public Image popupIcon;
    public TextMeshProUGUI popupName;
    public TextMeshProUGUI popupDesc;
    public TextMeshProUGUI popupStats;
    public Button upgradeButton;
    public Button dismantleButton;
    public TMP_Dropdown targetDropdown;

    // maps dropdown index -> PlayerSkillId (0 = None)
    private List<int> _dropdownPlayerSkillIds = new List<int>();

    // 👇 MỚI THÊM: Biến chứa Danh sách kỹ năng để Tắt/Bật
    public GameObject skillListArea;

    private PlayerSkillResponse currentServerData;

    public void ShowPopup(SkillData visual, PlayerSkillResponse server)
    {
        currentServerData = server;

        // Ảnh lấy từ Client (Lúc nào cũng có nên không sợ lỗi)
        if (visual != null && visual.skillIcon != null)
        {
            popupIcon.sprite = visual.skillIcon;
        }

        // KIỂM TRA AN TOÀN: Nếu kỹ năng ĐÃ MỞ KHÓA
        if (server != null)
        {
            popupName.text = server.SkillName;
            popupDesc.text = server.SkillDescription;
            popupStats.text = $"Cấp độ: {server.Level} \nSát thương: {server.EffectiveDamage} \nHồi chiêu: {server.CooldownSeconds}s";

            // Bật nút nâng cấp
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(true);

            // 👇 ĐÃ SỬA: Xử lý nút Phân rã (Khóa nếu kỹ năng đang được trang bị)
            if (dismantleButton != null)
            {
                dismantleButton.gameObject.SetActive(true);
                // Nếu đang trang bị (IsEquipped == true) -> interactable = false (Khóa nút mờ đi)
                // Nếu chưa trang bị (IsEquipped == false) -> interactable = true (Bấm được bình thường)
                dismantleButton.interactable = !server.IsEquipped;
            }

            // Prepare target dropdown (load player's owned skills)
            if (targetDropdown != null)
            {
                // reset
                targetDropdown.ClearOptions();
                _dropdownPlayerSkillIds.Clear();

                // Add default 'None' option
                var options = new List<string> { "None" };
                _dropdownPlayerSkillIds.Add(0);

                // Load player's skills from server and populate options (exclude current skill)
                SkillApi.Instance.GetMySkills(
                    response =>
                    {
                        var mySkills = response.Skills ?? new List<PlayerSkillResponse>();
                        foreach (var s in mySkills)
                        {
                            if (s == null) continue;
                            if (s.PlayerSkillId == server.PlayerSkillId) continue; // skip source
                            // Only include skills usable by player's class (client-side check)
                            options.Add($"{s.SkillName} (Lv.{s.Level})");
                            _dropdownPlayerSkillIds.Add(s.PlayerSkillId);
                        }
                        targetDropdown.AddOptions(options);
                        targetDropdown.value = 0;
                    },
                    error =>
                    {
                        Debug.LogWarning("Could not load player skills for dismantle dropdown: " + error.Message);
                        targetDropdown.AddOptions(new List<string> { "None" });
                        targetDropdown.value = 0;
                    }
                );
            }
        }
        // NẾU KỸ NĂNG CHƯA MỞ KHÓA (server == null)
        else
        {
            popupName.text = "Kỹ năng bị khóa";
            popupDesc.text = "Bạn chưa sở hữu kỹ năng này. Hãy làm nhiệm vụ hoặc đạt cấp độ yêu cầu để mở khóa.";
            popupStats.text = "Cấp độ: 0 \nSát thương: 0 \nHồi chiêu: 0s";

            // Ẩn nút nâng cấp đi để người chơi không bấm được
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);
            if (dismantleButton != null) dismantleButton.gameObject.SetActive(false);
            if (targetDropdown != null)
            {
                targetDropdown.ClearOptions();
            }
        }

        // 👇 ẨN DANH SÁCH KỸ NĂNG KHI MỞ POPUP
        if (skillListArea != null)
        {
            skillListArea.SetActive(false);
        }

        gameObject.SetActive(true);
    }

    public void HidePopup()
    {
        // 👇 HIỆN LẠI DANH SÁCH KỸ NĂNG KHI ĐÓNG POPUP
        if (skillListArea != null)
        {
            skillListArea.SetActive(true);
        }

        gameObject.SetActive(false);
    }

    // Gắn hàm này vào OnClick của nút Upgrade trên UI
    public void OnClickUpgrade()
    {
        if (currentServerData == null) return;

        // Tắt nút để tránh user bấm spam (Double click)
        upgradeButton.interactable = false;

        SkillApi.Instance.UpgradePlayerSkill(
            currentServerData.PlayerSkillId,
            onSuccess: (updatedSkill) =>
            {
                Debug.Log("Nâng cấp thành công lên Level " + updatedSkill.Level);

                // Cập nhật lại Text trên UI ngay lập tức
                currentServerData = updatedSkill;
                popupStats.text = $"Cấp độ: {updatedSkill.Level} \nSát thương: {updatedSkill.EffectiveDamage} \nHồi chiêu: {updatedSkill.CooldownSeconds}s";

                upgradeButton.interactable = true;

                // TÌM CẢ OBJECT BỊ ẨN VÀ GỌI REFRESH AN TOÀN
                var panelManager = FindFirstObjectByType<SkillPanelManager>(FindObjectsInactive.Include);
                if (panelManager != null)
                {
                    panelManager.RefreshSkillList();
                }

                var invManager = FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
                if (invManager != null)
                {
                    invManager.LoadInventory(true);
                }
            },
            onError: (error) =>
            {
                Debug.LogError("Lỗi nâng cấp: " + error.Message);
                popupDesc.text = $"<color=red>Lỗi: {error.Message}</color>";
                upgradeButton.interactable = true;
            }
        );
    }

    public void OnClickDismantle()
    {
        if (currentServerData == null) return;

        dismantleButton.interactable = false;

        int? targetId = null;
        if (targetDropdown != null && _dropdownPlayerSkillIds.Count > 0)
        {
            int idx = targetDropdown.value;
            if (idx >= 0 && idx < _dropdownPlayerSkillIds.Count)
            {
                int mapped = _dropdownPlayerSkillIds[idx];
                if (mapped != 0) targetId = mapped;
            }
        }

        SkillApi.Instance.DismantlePlayerSkill(
            currentServerData.PlayerSkillId,
            targetId,
            onSuccess: (updated) =>
            {
                Debug.Log("Dismantle success");
                dismantleButton.interactable = true;

                // Refresh skill list
                var panelManager = FindFirstObjectByType<SkillPanelManager>(FindObjectsInactive.Include);
                if (panelManager != null)
                {
                    panelManager.RefreshSkillList();
                }
                HidePopup();
            },
            onError: (error) =>
            {
                Debug.LogError("Dismantle failed: " + error.Message);
                dismantleButton.interactable = true;
            }
        );
    }
}