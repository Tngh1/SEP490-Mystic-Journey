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
        public static GuildUIManager Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject mainGuildPanel; // Panel bự nhất chứa tất cả
        [SerializeField] private GameObject tabsPanel; // Panel chứa các Tab bên phải (Info, Rank, Chat)
        [SerializeField] private GameObject guildListPanel;
        [SerializeField] private GameObject guildDetailPanel;
        [SerializeField] private GameObject createGuildPanel;
        [SerializeField] private GameObject guildInfoPanel;
        [SerializeField] private GameObject memberListPanel;
        [SerializeField] private UIGuildInvitePanel invitePanel;

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
        [SerializeField] private Button btnApprove;
        [SerializeField] private Toggle toggleRequireApproval;
        [SerializeField] private TMP_InputField inputRequiredLevel;
        [SerializeField] private Button btnSaveSettings;
        [SerializeField] private Button btnToggleKickMode;

        [Header("Member List UI")]
        [SerializeField] private Transform memberListContainer;
        [SerializeField] private GameObject memberEntryPrefab;

        [Header("Application List UI")]
        [SerializeField] private Transform applicationListContainer;
        [SerializeField] private GameObject applicationEntryPrefab;
        [SerializeField] private TextMeshProUGUI txtApplicationCount;
        [SerializeField] private GameObject applicationListPanel;

        [Header("Guild List UI")]
        [SerializeField] private TMP_InputField inputSearchGuild;
        [SerializeField] private Transform guildListContainer;
        [SerializeField] private GameObject guildEntryPrefab;

        [Header("Rank Settings")]
        [SerializeField] private GameObject memberHeaders; // Chứa text Members, Medals, Feats, Status
        [SerializeField] private GameObject rankHeaders; // Chứa text hạng, tên guild, level, điểm
        [SerializeField] private Image btnRankTabImage; // Nút bấm Tab Rank bên phải


        // Lưu thông tin Guild hiện tại
        public GuildDetailResponseDto currentGuild; // Lu thng tin Guild ca ti hoc Guild dang xem chi tit
        private bool isShowingApplications = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            // Auto hide/show tabsPanel if we are inside GuildInfo but viewing GuildDetail preview (from Rank list)
            if (currentGuild != null && guildInfoPanel != null && guildInfoPanel.activeInHierarchy)
            {
                bool isViewingDetail = (guildDetailPanel != null && guildDetailPanel.activeInHierarchy);
                if (tabsPanel != null && tabsPanel.activeSelf == isViewingDetail)
                {
                    tabsPanel.SetActive(!isViewingDetail);
                }
            }
        }

        private void Start()
        {
            // Tạm thời để ẩn hết
            if (mainGuildPanel != null) mainGuildPanel.SetActive(false);
            if (guildListPanel != null) guildListPanel.SetActive(false);
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (createGuildPanel != null) createGuildPanel.SetActive(false);
            
            // Ràng buộc số lượng ký tự nhập vào khi tạo Guild
            if (inputCreateName != null) inputCreateName.characterLimit = 15;

            if (toggleRequireApproval != null)
            {
                toggleRequireApproval.onValueChanged.AddListener(isOn => {
                    if (inputRequiredLevel != null) inputRequiredLevel.interactable = isOn;
                });
                toggleRequireApproval.isOn = false;
            }

            if (btnSaveSettings != null)
            {
                btnSaveSettings.onClick.AddListener(OnSaveSettingsClicked);
            }

            if (btnLeave != null)
            {
                btnLeave.onClick.AddListener(RequestLeaveGuild);
            }

            if (btnApprove != null)
            {
                btnApprove.onClick.AddListener(ToggleApplicationsList);
            }

            if (btnLevelUp != null)
            {
                btnLevelUp.onClick.AddListener(LevelUp);
            }

            // Bind Right Tabs
            if (tabsPanel != null)
            {
                Transform btnRightInfo = tabsPanel.transform.Find("InfoButton");
                if (btnRightInfo != null) {
                    btnRightInfo.GetComponent<Button>()?.onClick.RemoveAllListeners();
                    btnRightInfo.GetComponent<Button>()?.onClick.AddListener(SwitchToInfoTab);
                }

                Transform btnRightRank = tabsPanel.transform.Find("RankButton");
                if (btnRightRank != null) {
                    btnRightRank.GetComponent<Button>()?.onClick.RemoveAllListeners();
                    btnRightRank.GetComponent<Button>()?.onClick.AddListener(SwitchToRankTab);
                }
            }
            else
            {
                // Fallback if tabsPanel is somehow null
                if (btnInfoTabImage != null) btnInfoTabImage.GetComponent<Button>()?.onClick.AddListener(SwitchToInfoTab);
                if (btnManageTabImage != null) btnManageTabImage.GetComponent<Button>()?.onClick.AddListener(SwitchToManageTab);
                if (btnRankTabImage != null) btnRankTabImage.GetComponent<Button>()?.onClick.AddListener(SwitchToRankTab);
            }

            // Bind Left Tabs
            if (guildInfoPanel != null)
            {
                Transform leftTabs = guildInfoPanel.transform.Find("Tabs");
                if (leftTabs != null)
                {
                    Transform btnLeftInfo = leftTabs.Find("InfoButton");
                    if (btnLeftInfo != null) {
                        btnLeftInfo.GetComponent<Button>()?.onClick.RemoveAllListeners();
                        btnLeftInfo.GetComponent<Button>()?.onClick.AddListener(SwitchToInfoTab);
                    }

                    Transform btnLeftManage = leftTabs.Find("ManageButton");
                    if (btnLeftManage != null) {
                        btnLeftManage.GetComponent<Button>()?.onClick.RemoveAllListeners();
                        btnLeftManage.GetComponent<Button>()?.onClick.AddListener(SwitchToManageTab);
                    }
                }
            }
        }

        private void OnSaveSettingsClicked()
        {
            if (currentGuild == null) return;
            
            int joinPolicy = toggleRequireApproval != null && toggleRequireApproval.isOn ? 1 : 0;
            int requiredLevel = 1;
            
            if (inputRequiredLevel != null && !string.IsNullOrEmpty(inputRequiredLevel.text))
            {
                int.TryParse(inputRequiredLevel.text, out requiredLevel);
                if (requiredLevel < 1) requiredLevel = 1; // Khong cho nhap am
            }

            GuildApi.UpdateSettings(currentGuild.guildId, requiredLevel, joinPolicy, 
                response => {
                    UIPopupManager.Instance.ShowAlert("Notice", "Guild settings saved!");
                    // Update local copy
                    currentGuild.joinPolicy = joinPolicy;
                    currentGuild.requiredLevel = requiredLevel;
                },
                error => {
                    UIPopupManager.Instance.ShowAlert("Error", "Error saving settings: " + error.Message);
                });
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
            if (guildListPanel != null) guildListPanel.SetActive(false);
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (createGuildPanel != null) createGuildPanel.SetActive(false);
            if (guildInfoPanel != null) guildInfoPanel.SetActive(false);
            if (memberListPanel != null) memberListPanel.SetActive(false);
            if (tabsPanel != null) tabsPanel.SetActive(false); // Ẩn các tab bên phải khi chưa có guild

            // Nếu GuildList đang được dùng làm Rank thì nút Create bị ẩn đi, giờ cần hiện lại
            if (guildListPanel != null)
            {
                Transform createBtn = guildListPanel.transform.Find("CreateButton");
                if (createBtn != null) createBtn.gameObject.SetActive(true);
            }

            if (guildListPanel != null) guildListPanel.SetActive(true);

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
                joinPolicy = 0 // Open by default
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
            Debug.Log($"[GuildUIManager] RequestLeaveGuild called. currentGuild: {(currentGuild != null ? currentGuild.name : "null")}");
            if (currentGuild == null) return;
            
            int myProfileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, -1);
            Debug.Log($"[GuildUIManager] myProfileId: {myProfileId}, leaderId: {currentGuild.leaderId}");
            
            if (currentGuild.leaderId == myProfileId)
            {
                Debug.Log($"[GuildUIManager] User is leader. Members count: {(currentGuild.members != null ? currentGuild.members.Count : 0)}");
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
                Debug.Log("[GuildUIManager] User is NOT leader. Showing leave confirm popup.");
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
                        guildListContainer.gameObject.SetActive(true);
                        foreach (Transform child in guildListContainer)
                            Destroy(child.gameObject);
                        
                        // Tạo danh sách mới
                        for (int i = 0; i < list.Count; i++)
                        {
                            var guild = list[i];
                            GameObject obj = Instantiate(guildEntryPrefab, guildListContainer);
                            obj.SetActive(true);
                            obj.transform.localScale = Vector3.one;
                            UIGuildEntry entry = obj.GetComponent<UIGuildEntry>();
                            entry.Setup(guild, 
                                entryClicked: (id) => OpenGuildDetail(id), 
                                applyClicked: (id) => ApplyToGuild(id),
                                rank: i + 1);
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

            // Load danh sách thành viên (bao gồm cả bản thân)
            LoadMemberList();

            Debug.Log($"My Guild loaded: {detail.name} with {detail.members.Count} members.");
        }

        public void SwitchToInfoTab()
        {
            if (infoTabContainer != null) infoTabContainer.SetActive(true);
            if (manageTabContainer != null) manageTabContainer.SetActive(false);
            if (memberListPanel != null) memberListPanel.SetActive(true);
            if (applicationListPanel != null && applicationListPanel != memberListPanel) applicationListPanel.SetActive(false);
            if (memberHeaders != null) memberHeaders.SetActive(true);
            if (rankHeaders != null) rankHeaders.SetActive(false);
            
            // Ẩn panel của Rank tab (vì nó dùng chung guildListPanel)
            if (guildListPanel != null) guildListPanel.SetActive(false);

            HighlightLeftTab("InfoButton");
            HighlightRightTab("InfoButton");

            // Show Left Tabs
            if (guildInfoPanel != null)
            {
                Transform leftTabs = guildInfoPanel.transform.Find("Tabs");
                if (leftTabs != null) leftTabs.gameObject.SetActive(true);
            }

            // Hiển thị danh sách thành viên khi vào Info Tab
            if (currentGuild != null)
            {
                LoadMemberList();
            }
        }

        public void SwitchToManageTab()
        {
            if (infoTabContainer != null) infoTabContainer.SetActive(false);
            if (manageTabContainer != null) manageTabContainer.SetActive(true);
            if (memberListPanel != null) memberListPanel.SetActive(true);
            if (applicationListPanel != null && applicationListPanel != memberListPanel) applicationListPanel.SetActive(false);
            if (memberHeaders != null) memberHeaders.SetActive(true);
            if (rankHeaders != null) rankHeaders.SetActive(false);
            
            // Ẩn panel của Rank tab
            if (guildListPanel != null) guildListPanel.SetActive(false);

            HighlightLeftTab("ManageButton");
            HighlightRightTab("InfoButton");

            // Show Left Tabs
            if (guildInfoPanel != null)
            {
                Transform leftTabs = guildInfoPanel.transform.Find("Tabs");
                if (leftTabs != null) leftTabs.gameObject.SetActive(true);
            }

            // Chuyển về hiển thị danh sách Member mặc định khi sang Manage Tab
            isShowingApplications = false;
            LoadMemberList();

            // Bật nút Approve chỉ khi là Leader hoặc Officer
            UpdateManageButtonsVisibility();

            if (btnApprove != null)
            {
                var txt = btnApprove.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = "Approve";
            }
        }

        private void UpdateManageButtonsVisibility()
        {
            int myProfileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, -1);
            bool isLeader = currentGuild != null && currentGuild.members != null &&
                currentGuild.members.Any(m => m.playerProfileId == myProfileId && m.role == "Leader");
            bool isOfficer = currentGuild != null && currentGuild.members != null &&
                currentGuild.members.Any(m => m.playerProfileId == myProfileId && m.role == "Officer");
            bool isLeaderOrOfficer = isLeader || isOfficer;

            if (btnApprove != null) btnApprove.gameObject.SetActive(isLeaderOrOfficer);
            
            // Leader-only buttons
            if (btnLevelUp != null) btnLevelUp.gameObject.SetActive(isLeader);
            if (btnSaveSettings != null) btnSaveSettings.gameObject.SetActive(isLeader);
            if (btnToggleKickMode != null) btnToggleKickMode.gameObject.SetActive(isLeader);
            
            // Setup settings UI interactability based on Leader role
            if (currentGuild != null)
            {
                if (toggleRequireApproval != null)
                {
                    toggleRequireApproval.interactable = isLeader;
                    toggleRequireApproval.SetIsOnWithoutNotify(currentGuild.joinPolicy == 1);
                }
                
                if (inputRequiredLevel != null)
                {
                    inputRequiredLevel.text = currentGuild.requiredLevel.ToString();
                    inputRequiredLevel.interactable = isLeader && (currentGuild.joinPolicy == 1);
                }
            }
        }

        public void SwitchToRankTab()
        {
            // Tắt Info/Manage/Member
            if (infoTabContainer != null) infoTabContainer.SetActive(false);
            if (manageTabContainer != null) manageTabContainer.SetActive(false);
            if (memberListPanel != null) memberListPanel.SetActive(false);
            if (applicationListPanel != null) applicationListPanel.SetActive(false);
            
            // Ẩn Header Member, Bật Header Rank
            if (memberHeaders != null) memberHeaders.SetActive(false);
            if (rankHeaders != null) rankHeaders.SetActive(true);

            // Bật Panel Rank
            if (guildListPanel != null) guildListPanel.SetActive(true);

            // Tắt màu Info/Manage, Bật màu Rank
            HighlightRightTab("RankButton");

            // Hide Left Tabs
            if (guildInfoPanel != null)
            {
                Transform leftTabs = guildInfoPanel.transform.Find("Tabs");
                if (leftTabs != null) leftTabs.gameObject.SetActive(false);
            }

            if (createGuildPanel != null) createGuildPanel.SetActive(false);

            // Hide Create Button if we are viewing Rank while not in a guild
            if (guildListPanel != null)
            {
                Transform createBtn = guildListPanel.transform.Find("CreateButton");
                if (createBtn != null) createBtn.gameObject.SetActive(false);
            }

            LoadGuildRankings();
        }

        private void LoadGuildRankings()
        {
            // Xóa list cũ
            if (guildListContainer != null)
            {
                foreach (Transform child in guildListContainer)
                {
                    Destroy(child.gameObject);
                }
            }

            GuildApi.GetGuildRankings(
                onSuccess: (rankings) => {
                    if (guildListContainer == null || guildEntryPrefab == null) return;
                    foreach (var rank in rankings)
                    {
                        GameObject obj = Instantiate(guildEntryPrefab, guildListContainer);
                        obj.SetActive(true);
                        obj.transform.localScale = Vector3.one;
                        var entry = obj.GetComponent<UIGuildEntry>();
                        if (entry != null)
                        {
                            entry.SetupRank(rank, 
                                entryClicked: (id) => OpenGuildDetail(id), 
                                applyClicked: (id) => ApplyToGuild(id));
                        }
                    }
                },
                onError: (err) => {
                    Debug.LogError("Lỗi khi load bảng xếp hạng: " + err.Message);
                });
        }



        public void ToggleApplicationsList()
        {
            if (currentGuild == null) return;

            isShowingApplications = !isShowingApplications;

            // Nếu 2 panel khác nhau thì bật tắt, nếu giống nhau thì luôn bật
            if (applicationListPanel != null && applicationListPanel != memberListPanel)
            {
                applicationListPanel.SetActive(isShowingApplications);
                if (memberListPanel != null) 
                    memberListPanel.SetActive(!isShowingApplications);
            }
            else if (memberListPanel != null)
            {
                memberListPanel.SetActive(true);
            }

            if (btnApprove != null)
            {
                var txt = btnApprove.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = isShowingApplications ? "Member" : "Approve";
            }

            if (isShowingApplications)
            {
                LoadApplicationList();
            }
            else
            {
                LoadMemberList();
            }
        }

        private void LoadMemberList()
        {
            if (currentGuild == null || currentGuild.members == null) return;
            if (memberListContainer == null || memberEntryPrefab == null) return;

            // Xóa danh sách cũ
            memberListContainer.gameObject.SetActive(true);
            foreach (Transform child in memberListContainer)
                Destroy(child.gameObject);

            // Sắp xếp: Leader > Officer > Member, sau đó theo level
            var sortedMembers = currentGuild.members
                .OrderByDescending(m => m.role == "Leader" ? 2 : m.role == "Officer" ? 1 : 0)
                .ThenByDescending(m => m.playerLevel)
                .ToList();

            int myProfileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, -1);
            var myMember = currentGuild.members.FirstOrDefault(m => m.playerProfileId == myProfileId);
            string myRole = myMember != null ? myMember.role : "Member";

            foreach (var member in sortedMembers)
            {
                GameObject obj = Instantiate(memberEntryPrefab, memberListContainer);
                obj.SetActive(true);
                obj.transform.localScale = Vector3.one;
                UIGuildMemberEntry entry = obj.GetComponent<UIGuildMemberEntry>();
                if (entry != null)
                {
                    bool canKick = false;
                    if (member.playerProfileId != myProfileId && myRole != "Member")
                    {
                        if (myRole == "Leader") canKick = true;
                        else if (myRole == "Officer" && member.role == "Member") canKick = true;
                    }
                    
                    entry.Setup(member, canKick, HandleKickMember, isKickModeActive);
                }
            }

            Debug.Log($"Loaded {sortedMembers.Count} guild members (including self)");
        }

        private void LoadApplicationList()
        {
            if (currentGuild == null) return;
            if (applicationListContainer == null || applicationEntryPrefab == null) return;

            GuildApi.GetApplications(currentGuild.guildId,
                onSuccess: (applications) =>
                {
                    // Xóa danh sách cũ
                    applicationListContainer.gameObject.SetActive(true);
                    foreach (Transform child in applicationListContainer)
                        Destroy(child.gameObject);

                    if (txtApplicationCount != null)
                        txtApplicationCount.text = $"Applications ({applications.Count})";

                    foreach (var app in applications)
                    {
                        GameObject obj = Instantiate(applicationEntryPrefab, applicationListContainer);
                        obj.SetActive(true);
                        obj.transform.localScale = Vector3.one;
                        UIGuildApplicationEntry entry = obj.GetComponent<UIGuildApplicationEntry>();
                        if (entry != null)
                        {
                            entry.Setup(app, currentGuild.guildId,
                                onApprove: () => OnApplicationApproved(app.guildApplicationId),
                                onReject: () => OnApplicationRejected(app.guildApplicationId));
                        }
                    }

                    Debug.Log($"Loaded {applications.Count} applications");
                },
                onError: (err) =>
                {
                    Debug.LogError("Error loading applications: " + err.Message);
                });
        }

        private void OnApplicationApproved(int applicationId)
        {
            if (currentGuild == null) return;

            GuildApi.ApproveApplication(currentGuild.guildId, applicationId,
                onSuccess: (result) =>
                {
                    Debug.Log("Application approved!");
                    // Refresh lại danh sách
                    RefreshCurrentGuild();
                },
                onError: (err) =>
                {
                    Debug.LogError("Error approving application: " + err.Message);
                    if (UIPopupManager.Instance != null)
                        UIPopupManager.Instance.ShowAlert("Error", "Failed to approve: " + err.Message);
                });
        }

        private void OnApplicationRejected(int applicationId)
        {
            if (currentGuild == null) return;

            GuildApi.RejectApplication(currentGuild.guildId, applicationId,
                onSuccess: (result) =>
                {
                    Debug.Log("Application rejected!");
                    // Refresh lại danh sách
                    RefreshCurrentGuild();
                },
                onError: (err) =>
                {
                    Debug.LogError("Error rejecting application: " + err.Message);
                });
        }

        private void RefreshCurrentGuild()
        {
            if (currentGuild == null) return;

            GuildApi.GetGuildDetail(currentGuild.guildId,
                onSuccess: (detail) =>
                {
                    currentGuild = detail;
                    if (isShowingApplications)
                    {
                        LoadApplicationList();
                    }
                    else
                    {
                        LoadMemberList();
                    }
                },
                onError: (err) =>
                {
                    Debug.LogError("Error refreshing guild: " + err.Message);
                });
        }

        private void HighlightLeftTab(string activeTabName)
        {
            if (guildInfoPanel == null) return;
            Transform leftTabs = guildInfoPanel.transform.Find("Tabs");
            if (leftTabs == null) return;

            Color activeBgColor = Color.white;
            Color inactiveBgColor = new Color(0.5f, 0.5f, 0.5f, 1f); 
            Color activeTxtColor = new Color(0.35f, 0.2f, 0.05f, 1f); // Nâu đậm
            Color inactiveTxtColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Xám

            string[] tabNames = { "InfoButton", "ManageButton" };
            foreach (var tabName in tabNames)
            {
                Transform tab = leftTabs.Find(tabName);
                if (tab == null) continue;
                
                bool isActive = (tabName == activeTabName);
                var img = tab.GetComponent<Image>();
                if (img != null) img.color = isActive ? activeBgColor : inactiveBgColor;

                var txt = tab.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.color = isActive ? activeTxtColor : inactiveTxtColor;
            }
        }

        private void HighlightRightTab(string activeTabName)
        {
            if (tabsPanel == null) return;

            Color activeBgColor = Color.white;
            Color inactiveBgColor = new Color(0.5f, 0.5f, 0.5f, 1f); 
            Color activeTxtColor = new Color(0.35f, 0.2f, 0.05f, 1f); // Nâu đậm
            Color inactiveTxtColor = new Color(0.4f, 0.4f, 0.4f, 1f); // Xám

            string[] tabNames = { "InfoButton", "RankButton", "QuestButton" };
            foreach (var tabName in tabNames)
            {
                Transform tab = tabsPanel.transform.Find(tabName);
                if (tab == null) continue;

                bool isActive = (tabName == activeTabName);
                var img = tab.GetComponent<Image>();
                if (img != null) img.color = isActive ? activeBgColor : inactiveBgColor;

                var txt = tab.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.color = isActive ? activeTxtColor : inactiveTxtColor;
            }
        }

        public void ApplyToGuild(int guildId)
        {
            GuildApi.ApplyToGuild(guildId,
                onSuccess: (result) => {
                    if (result.success)
                    {
                        UIPopupManager.Instance.ShowAlert("Notice", result.message);
                        OpenGuildSystem(); // Refresh toàn bộ hệ thống
                    }
                    else if (!result.canJoin && result.cooldownRemainingSeconds > 0)
                    {
                        // Hiện Popup thông báo cooldown 24h
                        int hours = result.cooldownRemainingSeconds / 3600;
                        int minutes = (result.cooldownRemainingSeconds % 3600) / 60;
                        UIPopupManager.Instance.ShowAlert("Cannot join Guild", $"You must wait {hours}h {minutes}m.");
                    }
                    else
                    {
                        UIPopupManager.Instance.ShowAlert("Failed", result.message);
                    }
                },
                onError: (err) => {
                    UIPopupManager.Instance.ShowAlert("API Error", err.Message);
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

        public void OpenInvitePanel()
        {
            if (invitePanel != null)
            {
                invitePanel.OpenPanel();
            }
            else
            {
                Debug.LogWarning("Invite Panel is not assigned in GuildUIManager");
            }
        }

        private bool isKickModeActive = false;

        public void ToggleKickMode()
        {
            isKickModeActive = !isKickModeActive;
            if (memberListContainer != null)
            {
                foreach (Transform child in memberListContainer)
                {
                    var entry = child.GetComponent<UIGuildMemberEntry>();
                    if (entry != null)
                    {
                        entry.SetKickMode(isKickModeActive);
                    }
                }
            }
        }

        private void HandleKickMember(int memberId)
        {
            if (currentGuild == null) return;

            GuildApi.KickMember(currentGuild.guildId, memberId,
                onSuccess: (res) =>
                {
                    RefreshCurrentGuild();
                },
                onError: (err) =>
                {
                    UIPopupManager.Instance.ShowAlert("Error", err.Message);
                });
        }
    }
}
