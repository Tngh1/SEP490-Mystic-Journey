using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using System.Collections.Generic;

namespace MysticJourney.UI.Guild
{
    public class GuildUIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject guildListPanel;
        [SerializeField] private GameObject guildDetailPanel;
        [SerializeField] private GameObject createGuildPanel;
        [SerializeField] private GameObject guildInfoPanel;
        [SerializeField] private GameObject memberListPanel;

        [Header("Info Tab UI")]
        [SerializeField] private TextMeshProUGUI txtGuildName;
        [SerializeField] private TextMeshProUGUI txtMemberCount;
        [SerializeField] private TextMeshProUGUI txtGuildLevel;
        [SerializeField] private TextMeshProUGUI txtGuildTotalMedals;
        [SerializeField] private TextMeshProUGUI txtGuildNotice;

        [Header("Manage Tab UI")]
        [SerializeField] private TextMeshProUGUI txtGuildExp;
        [SerializeField] private TextMeshProUGUI txtMedalsToLevelUp;
        [SerializeField] private Button btnLevelUp;
        [SerializeField] private Button btnLeave;

        [Header("Guild List UI")]
        [SerializeField] private TMP_InputField inputSearchGuild;
        [SerializeField] private Transform guildListContainer;
        [SerializeField] private GameObject guildEntryPrefab;

        // Lưu thông tin Guild hiện tại
        private GuildDetailResponseDto currentGuild;

        private void Start()
        {
            // Tạm thời để ẩn hết
            guildListPanel.SetActive(false);
            guildDetailPanel.SetActive(false);
            createGuildPanel.SetActive(false);
        }

        /// <summary>
        /// Gọi hàm này khi người chơi bấm nút "Guild" ở màn hình chính
        /// </summary>
        public void OpenGuildSystem()
        {
            OpenGuildList();
        }

        public void SearchGuild()
        {
            string keyword = inputSearchGuild != null ? inputSearchGuild.text : "";
            LoadGuildList(keyword);
        }

        public void OpenGuildList()
        {
            guildListPanel.SetActive(true);
            guildDetailPanel.SetActive(false);
            createGuildPanel.SetActive(false);

            if (inputSearchGuild != null) inputSearchGuild.text = "";
            LoadGuildList("");
        }

        private void LoadGuildList(string keyword)
        {
            // Fetch list
            GuildApi.GetGuildList(keyword, null, null,
                onSuccess: (list) => {
                    Debug.Log($"Loaded {list.Count} guilds!");
                    
                    // Xóa danh sách cũ
                    if (guildListContainer != null)
                    {
                        foreach (Transform child in guildListContainer)
                            Destroy(child.gameObject);
                        
                        // Tạo danh sách mới
                        foreach (var guild in list)
                        {
                            GameObject obj = Instantiate(guildEntryPrefab, guildListContainer);
                            UIGuildEntry entry = obj.GetComponent<UIGuildEntry>();
                            // Truyền dữ liệu và callback hàm Apply vào Prefab
                            entry.Setup(guild, (id) => ApplyToGuild(id));
                        }
                    }
                },
                onError: (err) => {
                    Debug.LogError("Error loading guild list: " + err.Message);
                });
        }

        public void OpenGuildDetail(int guildId)
        {
            guildListPanel.SetActive(false);
            createGuildPanel.SetActive(false);

            GuildApi.GetGuildDetail(guildId, 
                onSuccess: (detail) => {
                    currentGuild = detail;
                    guildDetailPanel.SetActive(true);

                    // Map UI
                    // Tab Info
                    if (txtGuildName != null) txtGuildName.text = detail.name;
                    if (txtGuildLevel != null) txtGuildLevel.text = $"Lv. {detail.level}";
                    if (txtGuildNotice != null) txtGuildNotice.text = detail.notice;
                    if (txtMemberCount != null) txtMemberCount.text = $"Member: {detail.memberCount}/{detail.maxMembers}";
                    if (txtGuildTotalMedals != null) txtGuildTotalMedals.text = $"Medals: {detail.totalMedals}";

                    // Tab Manage
                    if (txtGuildExp != null) txtGuildExp.text = $"EXP: {detail.guildExp}/{detail.expToNextLevel}";
                    if (txtMedalsToLevelUp != null) txtMedalsToLevelUp.text = $"Medals: {detail.medalsToNextLevel}";

                    Debug.Log($"Guild loaded: {detail.name} with {detail.members.Count} members.");
                },
                onError: (err) => {
                    Debug.LogError("Error loading guild detail: " + err.Message);
                });
        }

        public void ApplyToGuild(int guildId)
        {
            GuildApi.ApplyToGuild(guildId,
                onSuccess: (result) => {
                    if (result.success)
                    {
                        Debug.Log("Applied / Joined successfully!");
                        OpenGuildDetail(guildId); // Refresh
                    }
                    else if (!result.canJoin && result.cooldownRemainingSeconds > 0)
                    {
                        // Hiện Popup thông báo cooldown 24h
                        int hours = result.cooldownRemainingSeconds / 3600;
                        int minutes = (result.cooldownRemainingSeconds % 3600) / 60;
                        Debug.LogWarning($"Cooldown! Chờ {hours}h {minutes}m nữa.");
                        // TODO: UIManager.ShowPopup(result.message);
                    }
                    else
                    {
                        Debug.LogWarning("Failed: " + result.message);
                    }
                },
                onError: (err) => {
                    Debug.LogError("API Error: " + err.Message);
                });
        }

        public void Donate()
        {
            if (currentGuild == null) return;

            GuildApi.Donate(currentGuild.guildId, 1, 
                onSuccess: (result) => {
                    Debug.Log($"Donate success! Gained {result.guildExpGained} EXP. New Level: {result.newGuildLevel}");
                    // Refresh data
                    OpenGuildDetail(currentGuild.guildId);
                },
                onError: (err) => {
                    Debug.LogError("Donate failed: " + err.Message);
                });
        }

        public void LevelUp()
        {
            if (currentGuild == null) return;

            GuildApi.LevelUp(currentGuild.guildId,
                onSuccess: (result) => {
                    Debug.Log("Guild Leveled Up Successfully!");
                    // Refresh data
                    OpenGuildDetail(currentGuild.guildId);
                },
                onError: (err) => {
                    Debug.LogError("Level Up failed: " + err.Message);
                });
        }

        public void LeaveGuild()
        {
            if (currentGuild == null) return;

            GuildApi.LeaveGuild(currentGuild.guildId,
                onSuccess: (result) => {
                    if (result.success)
                    {
                        Debug.Log("Left guild successfully.");
                        currentGuild = null;
                        OpenGuildList(); // Quay về màn hình tìm guild
                    }
                    else
                    {
                        Debug.LogWarning("Cannot leave: " + result.message);
                    }
                },
                onError: (err) => {
                    Debug.LogError("Leave Guild failed: " + err.Message);
                });
        }
    }
}
