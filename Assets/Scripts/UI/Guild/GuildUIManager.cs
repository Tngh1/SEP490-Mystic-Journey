using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using System.Collections.Generic;
using System.Linq;
using MysticJourney.UI;

namespace MysticJourney.UI.Guild
{
    // Executes core business logic for mono behaviour.
    public class GuildUIManager : MonoBehaviour
    {
        // Executes core business logic for instance.
        public static GuildUIManager Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject mainGuildPanel;
        [SerializeField] private GameObject tabsPanel;
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
        [SerializeField] private Image imgPreviewLeaderAvatar;
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
        [SerializeField] private GameObject memberHeaders;
        [SerializeField] private GameObject rankHeaders;
        [SerializeField] private Image btnRankTabImage;


        public GuildDetailResponseDto currentGuild;
        private bool isShowingApplications = false;

        // Initializes singleton instance on component creation.
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject); // Prevent multiple instances
        }

        // Monitors panel visibility and synchronizes tab states per frame.
        private void Update()
        {
            if (currentGuild != null && guildInfoPanel != null && guildInfoPanel.activeInHierarchy)
            {
                bool isViewingDetail = (guildDetailPanel != null && guildDetailPanel.activeInHierarchy);
                if (tabsPanel != null && tabsPanel.activeSelf == isViewingDetail)
                {
                    tabsPanel.SetActive(!isViewingDetail); // Toggle side tabs when viewing guild detail popup
                }
            }
        }

        // Binds button listeners for guild info tabs, management controls, and donations.
        private void Start()
        {
            if (mainGuildPanel != null) mainGuildPanel.SetActive(false); // Hide main modal on start
            if (guildListPanel != null) guildListPanel.SetActive(false);
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (createGuildPanel != null) createGuildPanel.SetActive(false);

            if (inputCreateName != null) inputCreateName.characterLimit = 15; // Set 15 char max length

            if (toggleRequireApproval != null)
            {
                toggleRequireApproval.onValueChanged.AddListener(isOn => {
                    if (inputRequiredLevel != null)
                    {
                        inputRequiredLevel.readOnly = !isOn || !IsCurrentPlayerGuildLeader(); // Enable min level input if approval is required
                    }
                });
                toggleRequireApproval.isOn = false;
            }

            if (btnSaveSettings != null)
            {
                btnSaveSettings.onClick.AddListener(OnSaveSettingsClicked); // Wire save settings handler
            }

            if (btnLeave != null)
            {
                btnLeave.onClick.AddListener(RequestLeaveGuild); // Wire leave guild request
            }

            if (btnApprove != null)
            {
                btnApprove.onClick.AddListener(ToggleApplicationsList); // Wire applicant list toggle
            }

            if (btnLevelUp != null)
            {
                btnLevelUp.onClick.AddListener(LevelUp); // Wire guild level up action
            }

            if (tabsPanel != null)
            {
                Transform btnRightInfo = tabsPanel.transform.Find("InfoButton");
                if (btnRightInfo != null) {
                    btnRightInfo.GetComponent<Button>()?.onClick.RemoveAllListeners();
                    btnRightInfo.GetComponent<Button>()?.onClick.AddListener(SwitchToInfoTab); // Route to info tab
                }

                Transform btnRightRank = tabsPanel.transform.Find("RankButton");
                if (btnRightRank != null) {
                    btnRightRank.GetComponent<Button>()?.onClick.RemoveAllListeners();
                    btnRightRank.GetComponent<Button>()?.onClick.AddListener(SwitchToRankTab); // Route to rank tab
                }
            }
            else
            {
                if (btnInfoTabImage != null) btnInfoTabImage.GetComponent<Button>()?.onClick.AddListener(SwitchToInfoTab);
                if (btnManageTabImage != null) btnManageTabImage.GetComponent<Button>()?.onClick.AddListener(SwitchToManageTab);
                if (btnRankTabImage != null) btnRankTabImage.GetComponent<Button>()?.onClick.AddListener(SwitchToRankTab);
            }

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
                        btnLeftManage.GetComponent<Button>()?.onClick.AddListener(SwitchToManageTab); // Route to manage tab
                    }
                }

                var allButtons = guildInfoPanel.GetComponentsInChildren<Button>(true);
                foreach (var b in allButtons)
                {
                    if (b.name == "DonateButton")
                    {
                        b.onClick.RemoveAllListeners();
                        b.onClick.AddListener(OpenDonatePopup); // Wire guild donation popup trigger
                        break;
                    }
                }
            }
        }


        public UIGuildDonatePanel donatePanel;

        // Opens the Guild Donation dialog (Gold, Gems, Medals contribution).
        private void OpenDonatePopup()
        {
            if (currentGuild == null) return;

            if (donatePanel == null) donatePanel = FindFirstObjectByType<UIGuildDonatePanel>(FindObjectsInactive.Include);

            if (donatePanel != null)
            {
                donatePanel.Open(currentGuild.guildId, () =>
                {
                    GuildApi.GetGuildDetail(currentGuild.guildId,
                        onSuccess: (detail) => OpenMyGuildDashboard(detail),
                        onError: (err) => Debug.LogError("Error refreshing guild after donate: " + err.Message));
                });
            }
        }

        // Executes core business logic for on save settings clicked.
        // Logic details: validates required non-empty string arguments.
        private void OnSaveSettingsClicked()
        {
            if (currentGuild == null) return;

            int joinPolicy = toggleRequireApproval != null && toggleRequireApproval.isOn ? 1 : 0;
            int requiredLevel = 1;

            if (inputRequiredLevel != null && !string.IsNullOrEmpty(inputRequiredLevel.text))
            {
                int.TryParse(inputRequiredLevel.text, out requiredLevel);
                if (requiredLevel < 1) requiredLevel = 1;
            }

            GuildApi.UpdateSettings(currentGuild.guildId, requiredLevel, joinPolicy,
                response => {
                    UIPopup.Instance.ShowAlert("Notice", "Guild settings saved!");
                    currentGuild.joinPolicy = joinPolicy;
                    currentGuild.requiredLevel = requiredLevel;
                },
                error => {
                    UIPopup.Instance.ShowAlert("Error", "Error saving settings: " + error.Message);
                });
        }

        // Executes core business logic for open guild system.
        public void OpenGuildSystem()
        {
            this.gameObject.SetActive(true);
            if (mainGuildPanel != null) mainGuildPanel.SetActive(true);

            if (guildListPanel != null) guildListPanel.SetActive(false);
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (createGuildPanel != null) createGuildPanel.SetActive(false);

            GuildApi.GetMyGuild(
                onSuccess: (detail) => {
                    if (detail != null && detail.guildId > 0)
                    {
                        OpenMyGuildDashboard(detail);
                    }
                    else
                    {
                        OpenGuildList();
                    }
                },
                onError: (err) => {
                    Debug.LogWarning("Không thể lấy thông tin Guild hiện tại, mở danh sách. Lỗi: " + err.Message);
                    OpenGuildList();
                }
            );
        }

        // Executes core business logic for close guild system.
        public void CloseGuildSystem()
        {
            if (mainGuildPanel != null) mainGuildPanel.SetActive(false);
            this.gameObject.SetActive(false);
        }

        // Executes core business logic for search guild.
        public void SearchGuild()
        {
            string keyword = inputSearchGuild != null ? inputSearchGuild.text : "";
            LoadGuildList(keyword);
        }

        // Executes core business logic for open guild list.
        public void OpenGuildList()
        {
            Debug.Log("[GuildUIManager] OpenGuildList() is called! StackTrace: " + UnityEngine.StackTraceUtility.ExtractStackTrace());
            if (guildListPanel != null) guildListPanel.SetActive(false);
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (createGuildPanel != null) createGuildPanel.SetActive(false);
            if (guildInfoPanel != null) guildInfoPanel.SetActive(false);
            if (memberListPanel != null) memberListPanel.SetActive(false);
            if (tabsPanel != null) tabsPanel.SetActive(false);

            if (guildListPanel != null)
            {
                Transform createBtn = guildListPanel.transform.Find("CreateButton");
                if (createBtn != null) createBtn.gameObject.SetActive(true);
            }

            if (guildListPanel != null) guildListPanel.SetActive(true);

            if (inputSearchGuild != null) inputSearchGuild.text = "";
            LoadGuildList("");
        }

        // Executes core business logic for open create guild panel.
        // Logic details: validates required non-empty string arguments.
        public void OpenCreateGuildPanel()
        {
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (guildInfoPanel != null) guildInfoPanel.SetActive(false);
            if (tabsPanel != null) tabsPanel.SetActive(false);

            if (createGuildPanel != null) createGuildPanel.SetActive(true);
        }

        // Executes core business logic for submit create guild.
        // Logic details: validates required non-empty string arguments.
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
                joinPolicy = 0
            };

            GuildApi.CreateGuild(request,
                onSuccess: (guildResp) =>
                {
                    if (UIPopup.Instance != null)
                    {
                        UIPopup.Instance.ShowAlert("Success", $"Created guild '{guildResp.name}' successfully!");
                    }
                    else
                    {
                        Debug.Log($"[GuildUIManager] Created guild '{guildResp.name}' successfully!");
                    }

                    inputCreateName.text = "";
                    if (inputCreateNotice != null) inputCreateNotice.text = "";

                    OpenGuildSystem();
                },
                onError: (err) =>
                {
                    if (UIPopup.Instance != null)
                    {
                        UIPopup.Instance.ShowAlert("Failed", "Error creating guild:\n" + err.Message);
                    }
                    else
                    {
                        Debug.LogError("[GuildUIManager] Error creating guild: " + err.Message);
                    }
                });
        }

        // Executes core business logic for request leave guild.
        public void RequestLeaveGuild()
        {
            Debug.Log($"[GuildUIManager] RequestLeaveGuild called. currentGuild: {(currentGuild != null ? currentGuild.name : "null")}");
            if (currentGuild == null) return;

            int myProfileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, -1);
            Debug.Log($"[GuildUIManager] myProfileId: {myProfileId}, leaderId: {currentGuild.leaderId}");

            if (currentGuild.leaderId == myProfileId)
            {
                Debug.Log($"[GuildUIManager] User is leader. Members count: {(currentGuild.members != null ? currentGuild.members.Count : 0)}");
                if (currentGuild.members != null && currentGuild.members.Count > 1)
                {
                    var nextLeader = currentGuild.members
                        .Where(m => m.playerProfileId != myProfileId)
                        .OrderBy(m => m.role == "Officer" ? 0 : 1)
                        .ThenByDescending(m => m.playerLevel)
                        .FirstOrDefault();

                    if (nextLeader != null)
                    {
                        if (UIPopup.Instance != null)
                        {
                            UIPopup.Instance.ShowConfirm(
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
                                            UIPopup.Instance.ShowAlert("Error", "Failed to transfer leadership: " + err.Message);
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
                    if (UIPopup.Instance != null)
                    {
                        UIPopup.Instance.ShowConfirm(
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
                if (UIPopup.Instance != null)
                {
                    UIPopup.Instance.ShowConfirm(
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

        // Executes core business logic for load guild list.
        private void LoadGuildList(string keyword)
        {
            GuildApi.GetGuildList(keyword, null, null,
                onSuccess: (list) => {
                    Debug.Log($"Loaded {list.Count} guilds!");

                    if (guildListContainer != null)
                    {
                        guildListContainer.gameObject.SetActive(true);
                        foreach (Transform child in guildListContainer)
                            Destroy(child.gameObject);

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

        // Executes core business logic for open guild detail.
        // Logic details: validates required non-empty string arguments.
        public void OpenGuildDetail(int guildId)
        {
            if (createGuildPanel != null) createGuildPanel.SetActive(false);

            GuildApi.GetGuildDetail(guildId,
                onSuccess: (detail) => {
                    guildDetailPanel.SetActive(true);

                    if (txtPreviewName != null) txtPreviewName.text = detail.name;
                    if (txtPreviewMember != null) txtPreviewMember.text = $"Members: {detail.memberCount}/{detail.maxMembers}";
                    if (txtPreviewLeader != null) txtPreviewLeader.text = $"Leader: {detail.leaderName}";
                    if (txtPreviewNotice != null) txtPreviewNotice.text = detail.notice;

                    if (imgPreviewLeaderAvatar != null)
                    {
                        imgPreviewLeaderAvatar.enabled = true;
                        string avatarUrl = string.IsNullOrWhiteSpace(detail.leaderAvatarUrl) ? "avatar_1" : detail.leaderAvatarUrl;
                        Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
                        if (avatarSprite != null)
                        {
                            imgPreviewLeaderAvatar.sprite = avatarSprite;
                        }
                    }

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

        // Executes core business logic for open my guild dashboard.
        public void OpenMyGuildDashboard(GuildDetailResponseDto detail)
        {
            currentGuild = detail;

            if (guildListPanel != null) guildListPanel.SetActive(false);
            if (guildDetailPanel != null) guildDetailPanel.SetActive(false);
            if (createGuildPanel != null) createGuildPanel.SetActive(false);

            if (guildInfoPanel != null) guildInfoPanel.SetActive(true);
            if (tabsPanel != null) tabsPanel.SetActive(true);
            SwitchToInfoTab();

            if (txtGuildName != null) txtGuildName.text = detail.name;
            if (txtGuildLevel != null) txtGuildLevel.text = $"Lv. {detail.level}";
            if (txtGuildNotice != null) txtGuildNotice.text = detail.notice;
            if (txtMemberCount != null) txtMemberCount.text = $"Member: {detail.memberCount}/{detail.maxMembers}";
            if (txtGuildTotalMedals != null) txtGuildTotalMedals.text = $"Medals: {detail.totalMedals}";

            if (txtGuildExp != null) txtGuildExp.text = $"EXP: {detail.guildExp}/{detail.expToNextLevel}";
            if (txtMedalsToLevelUp != null) txtMedalsToLevelUp.text = $"Medals: {detail.medalsToNextLevel}";

            int myProfileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, -1);
            var myMember = detail.members?.FirstOrDefault(m => m.playerProfileId == myProfileId);
            if (guildInfoPanel != null && myMember != null)
            {
                bool hasDonatedToday = false;
                if (!string.IsNullOrEmpty(myMember.lastDonateAt))
                {
                    if (System.DateTime.TryParse(myMember.lastDonateAt, out System.DateTime lastDonate))
                    {
                        if (lastDonate.ToUniversalTime().Date == System.DateTime.UtcNow.Date)
                        {
                            hasDonatedToday = true;
                        }
                    }
                }

                var allButtons = guildInfoPanel.GetComponentsInChildren<Button>(true);
                foreach (var b in allButtons)
                {
                    if (b.name == "DonateButton")
                    {
                        b.interactable = !hasDonatedToday;
                        var cg = b.GetComponent<CanvasGroup>();
                        if (cg != null) cg.alpha = hasDonatedToday ? 0.5f : 1.0f;
                        break;
                    }
                }
            }

            LoadMemberList();

            Debug.Log($"My Guild loaded: {detail.name} with {detail.members.Count} members.");
        }

        // Executes core business logic for switch to info tab.
        public void SwitchToInfoTab()
        {
            if (infoTabContainer != null) infoTabContainer.SetActive(true);
            if (manageTabContainer != null) manageTabContainer.SetActive(false);
            if (memberListPanel != null) memberListPanel.SetActive(true);
            if (applicationListPanel != null && applicationListPanel != memberListPanel) applicationListPanel.SetActive(false);
            if (memberHeaders != null) memberHeaders.SetActive(true);
            if (rankHeaders != null) rankHeaders.SetActive(false);

            if (guildListPanel != null) guildListPanel.SetActive(false);

            HighlightLeftTab("InfoButton");
            HighlightRightTab("InfoButton");

            if (guildInfoPanel != null)
            {
                Transform leftTabs = guildInfoPanel.transform.Find("Tabs");
                if (leftTabs != null) leftTabs.gameObject.SetActive(true);
            }

            if (currentGuild != null)
            {
                LoadMemberList();
            }
        }

        // Executes core business logic for switch to manage tab.
        public void SwitchToManageTab()
        {
            if (infoTabContainer != null) infoTabContainer.SetActive(false);
            if (manageTabContainer != null) manageTabContainer.SetActive(true);
            if (memberListPanel != null) memberListPanel.SetActive(true);
            if (applicationListPanel != null && applicationListPanel != memberListPanel) applicationListPanel.SetActive(false);
            if (memberHeaders != null) memberHeaders.SetActive(true);
            if (rankHeaders != null) rankHeaders.SetActive(false);

            if (guildListPanel != null) guildListPanel.SetActive(false);

            HighlightLeftTab("ManageButton");
            HighlightRightTab("InfoButton");

            if (guildInfoPanel != null)
            {
                Transform leftTabs = guildInfoPanel.transform.Find("Tabs");
                if (leftTabs != null) leftTabs.gameObject.SetActive(true);
            }

            isShowingApplications = false;
            LoadMemberList();

            UpdateManageButtonsVisibility();

            if (btnApprove != null)
            {
                var txt = btnApprove.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) txt.text = "Approve";
            }
        }

        // Executes core business logic for update manage buttons visibility.
        private void UpdateManageButtonsVisibility()
        {
            int myProfileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, -1);
            bool isLeader = currentGuild != null && currentGuild.members != null &&
                currentGuild.members.Any(m => m.playerProfileId == myProfileId && m.role == "Leader");
            bool isOfficer = currentGuild != null && currentGuild.members != null &&
                currentGuild.members.Any(m => m.playerProfileId == myProfileId && m.role == "Officer");
            bool isLeaderOrOfficer = isLeader || isOfficer;

            if (btnApprove != null) btnApprove.gameObject.SetActive(isLeaderOrOfficer);

            if (btnLevelUp != null) btnLevelUp.gameObject.SetActive(isLeader);
            if (btnSaveSettings != null) btnSaveSettings.gameObject.SetActive(isLeader);
            if (btnToggleKickMode != null) btnToggleKickMode.gameObject.SetActive(isLeader);

            if (currentGuild != null)
            {
                if (toggleRequireApproval != null)
                {
                    toggleRequireApproval.gameObject.SetActive(true);
                    toggleRequireApproval.interactable = isLeader;
                    toggleRequireApproval.SetIsOnWithoutNotify(currentGuild.joinPolicy == 1);
                }

                if (inputRequiredLevel != null)
                {
                    inputRequiredLevel.gameObject.SetActive(true);
                    inputRequiredLevel.text = currentGuild.requiredLevel.ToString();
                    inputRequiredLevel.interactable = true;
                    inputRequiredLevel.readOnly = !isLeader || currentGuild.joinPolicy != 1;
                }
            }
        }

        // Executes core business logic for is current player guild leader.
        // Returns a boolean indicating operation success.
        private bool IsCurrentPlayerGuildLeader()
        {
            if (currentGuild == null || currentGuild.members == null)
            {
                return false;
            }

            int myProfileId = PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerProfileIdKey, -1);
            return currentGuild.members.Any(member =>
                member.playerProfileId == myProfileId &&
                string.Equals(member.role, "Leader", System.StringComparison.OrdinalIgnoreCase));
        }

        // Executes core business logic for switch to rank tab.
        public void SwitchToRankTab()
        {
            if (infoTabContainer != null) infoTabContainer.SetActive(false);
            if (manageTabContainer != null) manageTabContainer.SetActive(false);
            if (memberListPanel != null) memberListPanel.SetActive(false);
            if (applicationListPanel != null) applicationListPanel.SetActive(false);

            if (memberHeaders != null) memberHeaders.SetActive(false);
            if (rankHeaders != null) rankHeaders.SetActive(true);

            if (guildListPanel != null) guildListPanel.SetActive(true);

            HighlightRightTab("RankButton");

            if (guildInfoPanel != null)
            {
                Transform leftTabs = guildInfoPanel.transform.Find("Tabs");
                if (leftTabs != null) leftTabs.gameObject.SetActive(false);
            }

            if (createGuildPanel != null) createGuildPanel.SetActive(false);

            if (guildListPanel != null)
            {
                Transform createBtn = guildListPanel.transform.Find("CreateButton");
                if (createBtn != null) createBtn.gameObject.SetActive(false);
            }

            LoadGuildRankings();
        }

        // Executes core business logic for load guild rankings.
        private void LoadGuildRankings()
        {
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



        // Executes core business logic for toggle applications list.
        public void ToggleApplicationsList()
        {
            if (currentGuild == null) return;

            isShowingApplications = !isShowingApplications;

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

        // Executes core business logic for load member list.
        private void LoadMemberList()
        {
            if (currentGuild == null || currentGuild.members == null) return;
            if (memberListContainer == null || memberEntryPrefab == null) return;

            memberListContainer.gameObject.SetActive(true);
            foreach (Transform child in memberListContainer)
                Destroy(child.gameObject);

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

                    entry.Setup(
                        member,
                        canKick,
                        HandleKickMember,
                        isKickModeActive,
                        member.playerProfileId == myProfileId);
                }
            }

            Debug.Log($"Loaded {sortedMembers.Count} guild members (including self)");
        }

        // Executes core business logic for load application list.
        private void LoadApplicationList()
        {
            if (currentGuild == null) return;
            if (applicationListContainer == null || applicationEntryPrefab == null) return;

            GuildApi.GetApplications(currentGuild.guildId,
                onSuccess: (applications) =>
                {
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

        // Executes core business logic for on application approved.
        private void OnApplicationApproved(int applicationId)
        {
            if (currentGuild == null) return;

            GuildApi.ApproveApplication(currentGuild.guildId, applicationId,
                onSuccess: (result) =>
                {
                    Debug.Log("Application approved!");
                    RefreshCurrentGuild();
                },
                onError: (err) =>
                {
                    Debug.LogError("Error approving application: " + err.Message);
                    if (UIPopup.Instance != null)
                        UIPopup.Instance.ShowAlert("Error", "Failed to approve: " + err.Message);
                });
        }

        // Executes core business logic for on application rejected.
        private void OnApplicationRejected(int applicationId)
        {
            if (currentGuild == null) return;

            GuildApi.RejectApplication(currentGuild.guildId, applicationId,
                onSuccess: (result) =>
                {
                    Debug.Log("Application rejected!");
                    RefreshCurrentGuild();
                },
                onError: (err) =>
                {
                    Debug.LogError("Error rejecting application: " + err.Message);
                });
        }

        // Executes core business logic for refresh current guild.
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

        // Executes core business logic for highlight left tab.
        private void HighlightLeftTab(string activeTabName)
        {
            if (guildInfoPanel == null) return;
            Transform leftTabs = guildInfoPanel.transform.Find("Tabs");
            if (leftTabs == null) return;

            Color activeBgColor = Color.white;
            Color inactiveBgColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            Color activeTxtColor = new Color(0.35f, 0.2f, 0.05f, 1f);
            Color inactiveTxtColor = new Color(0.4f, 0.4f, 0.4f, 1f);

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

        // Executes core business logic for highlight right tab.
        private void HighlightRightTab(string activeTabName)
        {
            if (tabsPanel == null) return;

            Color activeBgColor = Color.white;
            Color inactiveBgColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            Color activeTxtColor = new Color(0.35f, 0.2f, 0.05f, 1f);
            Color inactiveTxtColor = new Color(0.4f, 0.4f, 0.4f, 1f);

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

        // Executes core business logic for apply to guild.
        public void ApplyToGuild(int guildId)
        {
            GuildApi.ApplyToGuild(guildId,
                onSuccess: (result) => {
                    if (result.success)
                    {
                        UIPopup.Instance.ShowAlert("Notice", result.message);
                        OpenGuildSystem();
                    }
                    else if (!result.canJoin && result.cooldownRemainingSeconds > 0)
                    {
                        int hours = result.cooldownRemainingSeconds / 3600;
                        int minutes = (result.cooldownRemainingSeconds % 3600) / 60;
                        UIPopup.Instance.ShowAlert("Cannot join Guild", $"You must wait {hours}h {minutes}m.");
                    }
                    else
                    {
                        UIPopup.Instance.ShowAlert("Failed", result.message);
                    }
                },
                onError: (err) => {
                    UIPopup.Instance.ShowAlert("API Error", err.Message);
                });
        }



        // Executes core business logic for level up.
        public void LevelUp()
        {
            if (currentGuild == null) return;

            GuildApi.LevelUp(currentGuild.guildId,
                onSuccess: (result) => {
                    Debug.Log("Guild Leveled Up Successfully!");
                    OpenGuildDetail(currentGuild.guildId);
                },
                onError: (err) => {
                    Debug.LogError("Level Up failed: " + err.Message);
                });
        }

        // Executes core business logic for execute leave guild.
        private void ExecuteLeaveGuild()
        {
            if (currentGuild == null) return;

            GuildApi.LeaveGuild(currentGuild.guildId,
                onSuccess: (result) => {
                    if (result.success)
                    {
                        if (UIPopup.Instance != null)
                            UIPopup.Instance.ShowAlert("Success", "Left guild successfully.");
                        else
                            Debug.Log("Left guild successfully.");

                        currentGuild = null;
                        OpenGuildList();
                    }
                    else
                    {
                        if (UIPopup.Instance != null)
                            UIPopup.Instance.ShowAlert("Warning", "Cannot leave: " + result.message);
                        else
                            Debug.LogWarning("Cannot leave: " + result.message);
                    }
                },
                onError: (err) => {
                    if (UIPopup.Instance != null)
                        UIPopup.Instance.ShowAlert("Failed", err.Message);
                    else
                        Debug.LogError("Leave Guild failed: " + err.Message);
                });
        }

        // Executes core business logic for execute dissolve guild.
        private void ExecuteDissolveGuild()
        {
            if (currentGuild == null) return;

            GuildApi.DissolveGuild(currentGuild.guildId,
                onSuccess: (result) => {
                    if (UIPopup.Instance != null)
                        UIPopup.Instance.ShowAlert("Success", "Guild dissolved successfully.");
                    else
                        Debug.Log("Guild dissolved successfully.");

                    currentGuild = null;
                    OpenGuildList();
                },
                onError: (err) => {
                    if (UIPopup.Instance != null)
                        UIPopup.Instance.ShowAlert("Failed", err.Message);
                    else
                        Debug.LogError("Dissolve Guild failed: " + err.Message);
                });
        }

        // Executes core business logic for open invite panel.
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

        // Executes core business logic for toggle kick mode.
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

        // Executes core business logic for handle kick member.
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
                    UIPopup.Instance.ShowAlert("Error", err.Message);
                });
        }
    }
}
