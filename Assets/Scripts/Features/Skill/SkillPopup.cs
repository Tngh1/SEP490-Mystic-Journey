using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

public class SkillPopup : MonoBehaviour
{
    public Image popupIcon;
    public TextMeshProUGUI popupName;
    public TextMeshProUGUI popupDesc;
    public TextMeshProUGUI popupStats; // Thêm 1 dòng text hiển thị Sát thương & Cấp độ
    public Button upgradeButton;

    private PlayerSkillResponse currentServerData;

    public void ShowPopup(SkillData visual, PlayerSkillResponse server)
    {
        currentServerData = server;

        // Ảnh lấy từ Client
        popupIcon.sprite = visual.skillIcon;

        // Chỉ số lấy từ Server Database
        popupName.text = server.SkillName;
        popupDesc.text = server.SkillDescription;

        // Hiển thị Effective Damage đã được BLL tính toán
        popupStats.text = $"Cấp độ: {server.Level} \nSát thương: {server.EffectiveDamage} \nHồi chiêu: {server.CooldownSeconds}s";

        gameObject.SetActive(true);
    }

    public void HidePopup()
    {
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

                // (Tùy chọn) Gọi lại RefreshSkillList() ở SkillPanelManager để load lại toàn bộ bảng
                FindAnyObjectByType<SkillPanelManager>().RefreshSkillList();
            },
            onError: (error) =>
            {
                Debug.LogError("Lỗi nâng cấp: " + error.Message);
                upgradeButton.interactable = true;
            }
        );
    }
}