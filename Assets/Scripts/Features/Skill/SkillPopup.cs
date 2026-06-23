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
        }
        // NẾU KỸ NĂNG CHƯA MỞ KHÓA (server == null)
        else
        {
            popupName.text = "Kỹ năng bị khóa";
            popupDesc.text = "Bạn chưa sở hữu kỹ năng này. Hãy làm nhiệm vụ hoặc đạt cấp độ yêu cầu để mở khóa.";
            popupStats.text = "Cấp độ: 0 \nSát thương: 0 \nHồi chiêu: 0s";

            // Ẩn nút nâng cấp đi để người chơi không bấm được
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);
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
            },
            onError: (error) =>
            {
                Debug.LogError("Lỗi nâng cấp: " + error.Message);
                upgradeButton.interactable = true;
            }
        );
    }
}