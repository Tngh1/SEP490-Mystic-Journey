using System.Collections.Generic;
using UnityEngine;
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
        if (playerSkills == null || playerSkills.Count == 0) return;

        foreach (var serverSkill in playerSkills)
        {
            // Tìm ảnh có ID tương ứng
            SkillData matchedVisual = null;
            foreach (var data in allSkillsInGame)
            {
                if (data.skillId == serverSkill.SkillId)
                {
                    matchedVisual = data;
                    break;
                }
            }

            if (matchedVisual != null)
            {
                // Instantiate và gán dữ liệu
                GameObject newSkillObj = Instantiate(skillItemPrefab, contentArea);
                SkillItem itemScript = newSkillObj.GetComponent<SkillItem>();

                itemScript.Setup(matchedVisual, serverSkill);
            }
        }
    }
}