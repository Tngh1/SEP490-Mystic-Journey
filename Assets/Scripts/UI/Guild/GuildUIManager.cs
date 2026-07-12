using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.UI; // For UIPopupManager

namespace MysticJourney.UI.Guild
{
    public class GuildUIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainGuildPanel; // Panel bự nhất chứa tất cả
        [SerializeField] private GameObject tabsPanel; // Panel chứa các Tab bên phải (Info, Rank, Chat)
        [SerializeField] private GameObject guildListPanel;
        [SerializeField] private GameObject guildDetailPanel;
        [SerializeField] private GameObject createGuildPanel;
        [SerializeField] private GameObject guildInfoPanel;
        [SerializeField] private GameObject memberListPanel;

        [Header("Preview Detail UI (Outsider)")]
        [SerializeField] private TextMeshProUGUI txtPreviewName;
        [SerializeField] private TextMeshProUGUI txtPreviewMember;
        [SerializeField] private TextMeshProUGUI txtPreviewLeader;
        [SerializeField] private TextMeshProUGUI txtPreviewNotice;
        [SerializeField] private Button btnPreviewApply;

        [Header("Guild Info Tabs (Containers)")]
        [SerializeField] private GameObject infoTabContainer;
        [SerializeField] private GameObject manageTabContainer;
        [SerializeField] private Image btnInfoTabImage;
        [SerializeField] private Image btnManageTabImage;

        [Header("Info Tab UI (Insider)")]
        [SerializeField] private TextMeshProUGUI txtGuildName;
        [SerializeField] private TextMeshProUGUI txtMemberCount;
        [SerializeField] private TextMeshProUGUI txtGuildLevel;
        [SerializeField] private TextMeshProUGUI txtGuildTotalMedals;
        [SerializeField] private TextMeshProUGUI txtGuildNotice;

        [Header("Create Guild UI")]
        [SerializeField] private TMP_InputField inputCreateName;
        [SerializeField] private TMP_InputField inputCreateNotice;

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
            if (mainGuildPanel != null) mainGuildPanel.SetActive(false);
            if (guildListPanel != null) guildListPanel.SetActive(false);
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (createGuildPanel != null) createGuildPanel.SetActive(false);
            
            // Ràng buộc số lượng ký tự nhập vào khi tạo Guild
            if (inputCreateName != null) inputCreateName.characterLimit = 15;
        }

        /// <summary>
        /// Gọi hàm này khi người chơi bấm nút "Guild" ở màn hình chính
        /// </summary>
        public void OpenGuildSystem()
        {
            this.gameObject.SetActive(true); // Đảm bảo script được chạy
            if (mainGuildPanel != null) mainGuildPanel.SetActive(true); // Bật Panel tổng lên!
            
            // Ẩn tất cả trước khi có kết quả
            if (guildListPanel != null) guildListPanel.SetActive(false);
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (createGuildPanel != null) createGuildPanel.SetActive(false);

            GuildApi.GetMyGuild(
                onSuccess: (detail) => {
                    if (detail != null && detail.guildId > 0)
                    {
                        // Đã có Guild -> Mở tab Info của Guild mình
                        OpenMyGuildDashboard(detail);
                    }
                    else
                    {
                        // Chưa có Guild -> Mở danh sách
                        OpenGuildList();
                    }
                },
                onError: (err) => {
                    Debug.LogWarning("Không thể lấy thông tin Guild hiện tại, mở danh sách. Lỗi: " + err.Message);
                    OpenGuildList();
                }
            );
        }

        public void CloseGuildSystem()
        {
            if (mainGuildPanel != null) mainGuildPanel.SetActive(false);
            this.gameObject.SetActive(false); // Ẩn luôn cục quản lý để tối ưu performance
        }

        public void SearchGuild()
        {
            string keyword = inputSearchGuild != null ? inputSearchGuild.text : "";
            LoadGuildList(keyword);
        }

        public void OpenGuildList()
        {
            Debug.Log("[GuildUIManager] OpenGuildList() is called! StackTrace: " + UnityEngine.StackTraceUtility.ExtractStackTrace());
            guildListPanel.SetActive(false);
            guildDetailPanel.SetActive(false);
            createGuildPanel.SetActive(false);
            if (guildInfoPanel != null) guildInfoPanel.SetActive(false);
            if (memberListPanel != null) memberListPanel.SetActive(false);
            if (tabsPanel != null) tabsPanel.SetActive(false); // Ẩn các tab bên phải

            guildListPanel.SetActive(true);

            if (inputSearchGuild != null) inputSearchGuild.text = "";
            LoadGuildList("");
        }

        public void OpenCreateGuildPanel()
        {
            // Không ẩn guildListPanel vì nó nằm bên phải, create nằm bên trái
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (guildInfoPanel != null) guildInfoPanel.SetActive(false);
            if (tabsPanel != null) tabsPanel.SetActive(false);
            
            if (createGuildPanel != null) createGuildPanel.SetActive(true);
        }

        public void SubmitCreateGuild()
        {
            if (inputCreateName == null || string.IsNullOrWhiteSpace(inputCreateName.text))
            {
                Debug.LogWarning("[GuildUIManager] Guild name cannot be empty!");
                return;
            }

            var request = new CreateGuildRequestDto
            {
                name = inputCreateName.text,
                notice = inputCreateNotice != null ? inputCreateNotice.text : "",
                requiredLevel = 1,
                joinPolicy = 1 // Open
            };

            GuildApi.CreateGuild(request,
                onSuccess: (guildResp) =>
                {
                    // Tắt loading hoặc hiện thông báo
                    if (UIPopupManager.Instance != null)
                    {
                        UIPopupManager.Instance.ShowAlert("Success", $"Created guild '{guildResp.name}' successfully!");
                    }
                    else
                    {
                        Debug.Log($"[GuildUIManager] Created guild '{guildResp.name}' successfully!");
                    }
                    
                    // Clear the form
                    inputCreateName.text = "";
                    if (inputCreateNotice != null) inputCreateNotice.text = "";
                    
                    // Automatically open the Guild System again (which will fetch My Guild and open Info Panel)
                    OpenGuildSystem();
                },
                onError: (err) =>
                {
                    if (UIPopupManager.Instance != null)
                    {
                        UIPopupManager.Instance.ShowAlert("Failed", "Error creating guild:\n" + err.Message);
                    }
                    else
                    {
                        Debug.LogError("[GuildUIManager] Error creating guild: " + err.Message);
                    }
                });
        }

        public void RequestLeaveGuild()
        {
            if (currentGuild == null) return;
            
            int myProfileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, -1);
            
            if (currentGuild.leaderId == myProfileId)
            {
                // Kiểm tra xem bang còn ai khác không
                if (currentGuild.members != null && currentGuild.members.Count > 1)
                {
                    // Tìm 1 người khác (ưu tiên Officer, hoặc level cao nhất, hoặc random ai đó khác leader)
                    var nextLeader = currentGuild.members
                        .Where(m => m.playerProfileId != myProfileId)
                        .OrderBy(m => m.role == "Officer" ? 0 : 1) // ưu tiên Officer
                        .ThenByDescending(m => m.playerLevel)
                        .FirstOrDefault();

                    if (nextLeader != null)
                    {
                        if (UIPopupManager.Instance != null)
                        {
                            UIPopupManager.Instance.ShowConfirm(
                                "Transfer & Leave",
                                $"Do you want to transfer leadership to {nextLeader.playerDisplayName} and leave the guild?",
                                onConfirm: () =>
                                {
                                    GuildApi.TransferLeader(currentGuild.guildId, nextLeader.playerProfileId,
                                        onSuccess: (res) =>
                                        {
                                            ExecuteLeaveGuild();
                                        },
                                        onError: (err) =>
                                        {
                                            UIPopupManager.Instance.ShowAlert("Error", "Failed to transfer leadership: " + err.Message);
                                        });
                                }
                            );
                        }
                        else
                        {
                            Debug.LogWarning("You must transfer leadership before leaving.");
                        }
                    }
                }
                else
                {
                    // Chỉ có 1 mình -> Giải tán bang
                    if (UIPopupManager.Instance != null)
                    {
                        UIPopupManager.Instance.ShowConfirm(
                            "Dissolve Guild", 
                            $"You are the only member of '{currentGuild.name}'. Leaving will permanently dissolve the guild. Are you sure you want to dissolve it?", 
                            onConfirm: ExecuteDissolveGuild
                        );
                    }
                    else
                    {
                        ExecuteDissolveGuild();
                    }
                }
            }
            else
            {
                // Thành viên bình thường -> Rời bang
                if (UIPopupManager.Instance != null)
                {
                    UIPopupManager.Instance.ShowConfirm(
                        "Leave Guild", 
                        $"Are you sure you want to leave the guild '{currentGuild.name}'?", 
                        onConfirm: ExecuteLeaveGuild
                    );
                }
                else
                {
                    ExecuteLeaveGuild();
                }
            }
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
                            entry.Setup(guild, 
                                entryClicked: (id) => OpenGuildDetail(id), 
                                applyClicked: (id) => ApplyToGuild(id));
                        }
                    }
                },
                onError: (err) => {
                    Debug.LogError("Error loading guild list: " + err.Message);
                });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Dành cho: NGƯỜI CHƯA CÓ GUILD (Bấm vào xem Preview)
        // ─────────────────────────────────────────────────────────────────────────
        public void OpenGuildDetail(int guildId)
        {
            if (createGuildPanel != null) createGuildPanel.SetActive(false); // Ẩn Create panel (bên trái)

            GuildApi.GetGuildDetail(guildId, 
                onSuccess: (detail) => {
                    guildDetailPanel.SetActive(true);
                    
                    // Gắn thông tin cho bảng Preview (Dành cho người chưa có Guild)
                    if (txtPreviewName != null) txtPreviewName.text = detail.name;
                    if (txtPreviewMember != null) txtPreviewMember.text = $"Members: {detail.memberCount}/{detail.maxMembers}";
                    if (txtPreviewLeader != null) txtPreviewLeader.text = $"Leader: {detail.leaderName}";
                    if (txtPreviewNotice != null) txtPreviewNotice.text = detail.notice;

                    if (btnPreviewApply != null)
                    {
                        btnPreviewApply.onClick.RemoveAllListeners();
                        btnPreviewApply.onClick.AddListener(() => ApplyToGuild(detail.guildId));
                    }
                    
                    Debug.Log($"Preview Guild: {detail.name}");
                },
                onError: (err) => {
                    Debug.LogError("Error loading guild detail: " + err.Message);
                });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Dành cho: NGƯỜI ĐÃ CÓ GUILD (Mở Dashboard quản lý)
        // ─────────────────────────────────────────────────────────────────────────
        public void OpenMyGuildDashboard(GuildDetailResponseDto detail)
        {
            currentGuild = detail;

            // Ẩn các màn hình khác
            if (guildListPanel != null) guildListPanel.SetActive(false);
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (createGuildPanel != null) createGuildPanel.SetActive(false);
            
            // Bật màn hình Dashboard (Info Panel) và Tab mặc định là Info
            if (guildInfoPanel != null) guildInfoPanel.SetActive(true);
            if (tabsPanel != null) tabsPanel.SetActive(true); // Bật các tab bên phải lên
            SwitchToInfoTab();

            // Map UI cho Tab Info
            if (txtGuildName != null) txtGuildName.text = detail.name;
            if (txtGuildLevel != null) txtGuildLevel.text = $"Lv. {detail.level}";
            if (txtGuildNotice != null) txtGuildNotice.text = detail.notice;
            if (txtMemberCount != null) txtMemberCount.text = $"Member: {detail.memberCount}/{detail.maxMembers}";
            if (txtGuildTotalMedals != null) txtGuildTotalMedals.text = $"Medals: {detail.totalMedals}";

            // Map UI cho Tab Manage
            if (txtGuildExp != null) txtGuildExp.text = $"EXP: {detail.guildExp}/{detail.expToNextLevel}";
            if (txtMedalsToLevelUp != null) txtMedalsToLevelUp.text = $"Medals: {detail.medalsToNextLevel}";

            Debug.Log($"My Guild loaded: {detail.name} with {detail.members.Count} members.");
        }

        public void SwitchToInfoTab()
        {
            if (infoTabContainer != null) infoTabContainer.SetActive(true);
            if (manageTabContainer != null) manageTabContainer.SetActive(false);
            HighlightTab(btnInfoTabImage, btnManageTabImage);
        }

        public void SwitchToManageTab()
        {
            if (infoTabContainer != null) infoTabContainer.SetActive(false);
            if (manageTabContainer != null) manageTabContainer.SetActive(true);
            HighlightTab(btnManageTabImage, btnInfoTabImage);
        }

        private void HighlightTab(Image activeTab, Image inactiveTab)
        {
            if (activeTab != null) activeTab.color = Color.white;
            if (inactiveTab != null) inactiveTab.color = new Color(0.6f, 0.6f, 0.6f, 1f); // Màu xám tối
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

        private void ExecuteLeaveGuild()
        {
            if (currentGuild == null) return;

            GuildApi.LeaveGuild(currentGuild.guildId,
                onSuccess: (result) => {
                    if (result.success)
                    {
                        if (UIPopupManager.Instance != null)
                            UIPopupManager.Instance.ShowAlert("Success", "Left guild successfully.");
                        else
                            Debug.Log("Left guild successfully.");
                            
                        currentGuild = null;
                        OpenGuildList(); // Quay về màn hình tìm guild
                    }
                    else
                    {
                        if (UIPopupManager.Instance != null)
                            UIPopupManager.Instance.ShowAlert("Warning", "Cannot leave: " + result.message);
                        else
                            Debug.LogWarning("Cannot leave: " + result.message);
                    }
                },
                onError: (err) => {
                    if (UIPopupManager.Instance != null)
                        UIPopupManager.Instance.ShowAlert("Failed", err.Message);
                    else
                        Debug.LogError("Leave Guild failed: " + err.Message);
                });
        }

        private void ExecuteDissolveGuild()
        {
            if (currentGuild == null) return;

            GuildApi.DissolveGuild(currentGuild.guildId,
                onSuccess: (result) => {
                    if (UIPopupManager.Instance != null)
                        UIPopupManager.Instance.ShowAlert("Success", "Guild dissolved successfully.");
                    else
                        Debug.Log("Guild dissolved successfully.");
                        
                    currentGuild = null;
                    OpenGuildList(); // Quay về màn hình tìm guild
                },
                onError: (err) => {
                    if (UIPopupManager.Instance != null)
                        UIPopupManager.Instance.ShowAlert("Failed", err.Message);
                    else
                        Debug.LogError("Dissolve Guild failed: " + err.Message);
                });
        }
    }
}
