using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using MysticJourney.Core.Services;
using System.Linq;
using MysticJourney.UI;

namespace UI.Friend
{
    // Executes core business logic for mono behaviour.
    public class FriendUIManager : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button friendTabButton;
        [SerializeField] private Button addTabButton;

        [Header("Tab Sprites")]
        [SerializeField] private Sprite activeTabSprite;
        [SerializeField] private Sprite inactiveTabSprite;

        [Header("Panels")]
        [SerializeField] private GameObject friendPanel;
        [SerializeField] private GameObject addPanel;

        [Header("Friend List (Left Column)")]
        [SerializeField] private Transform friendListContainer;
        [SerializeField] private UIFriendEntry friendEntryPrefab;
        [SerializeField] private TMP_Text friendCountText;

        [Header("Request List (Left Column in Add Tab)")]
        [SerializeField] private Transform requestListContainer;
        [SerializeField] private UIFriendEntry requestEntryPrefab;
        [SerializeField] private TMP_Text requestCountText;

        [Header("Search Players (Right Column in Add Tab)")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private Button searchButton;
        [SerializeField] private Transform searchListContainer;
        [SerializeField] private UIFriendEntry searchEntryPrefab;

        [Header("Master-Detail Panel (Right Column in Friend Tab)")]
        [SerializeField] private GameObject detailPanelObj;
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailLevelText;
        [SerializeField] private TMP_Text detailClassText;
        [SerializeField] private TMP_Text detailStatusText;
        [SerializeField] private Image detailAvatarImage;

        [Header("Detail Action Buttons")]
        [SerializeField] private Button detailChatButton;
        [SerializeField] private Button detailUnfriendButton;
        [SerializeField] private Button detailBlockButton;
        [SerializeField] private Button detailProfileButton;

        [Header("Friend Chat")]
        [SerializeField] private UIFriendChatPanel friendChatPanel;

        [Header("UI Control")]
        [SerializeField] private Button closeButton;

        private List<FriendDto> currentFriends = new List<FriendDto>();
        private List<PendingFriendRequestDto> currentRequests = new List<PendingFriendRequestDto>();
        private List<FriendSearchDto> searchResults = new List<FriendSearchDto>();

        private int selectedProfileId;
        private string selectedFriendName;
        private bool started;

        // Binds tab switching buttons, search queries, and master-detail action buttons.
        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false)); // Close modal on click

            if (friendTabButton != null) friendTabButton.onClick.AddListener(ShowFriendTab); // Switch to Friend list
            if (addTabButton != null) addTabButton.onClick.AddListener(ShowAddTab); // Switch to Add Friend tab

            if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked); // Submit player search query

            SetupDetailButtons(); // Wire chat, unfriend, and block buttons

            started = true;
            ShowFriendTab(); // Default to Friends tab
        }

        // Refreshes friend entries and incoming request counts on panel open.
        private void OnEnable()
        {
            if (!started) return;

            RefreshData(); // Query friends and incoming invites
        }

        // Queries friends, pending requests, and active search results from REST API.
        public void RefreshData()
        {
            LoadFriends(); // Query friend list
            LoadRequests(); // Query pending friend requests

            if (addPanel != null && addPanel.activeSelf)
            {
                OnSearchClicked(); // Refresh search list if on Add tab
            }
        }

        // Configures action listeners for friend profile detail card.
        private void SetupDetailButtons()
        {
            if (detailChatButton == null && detailPanelObj != null)
            {
                var chatButtonTransform = detailPanelObj.transform.Find("ChatButton");
                if (chatButtonTransform != null) detailChatButton = chatButtonTransform.GetComponent<Button>();
            }

            if (friendChatPanel == null)
            {
                friendChatPanel = FindFirstObjectByType<UIFriendChatPanel>(FindObjectsInactive.Include); // Locate private chat panel
            }

            if (friendChatPanel == null)
            {
                friendChatPanel = CreateRuntimeFriendChatPanel(); // Create runtime chat fallback if missing
            }

            if (detailChatButton != null) detailChatButton.onClick.AddListener(OnDetailChatClicked); // Open private 1-on-1 chat
            if (detailUnfriendButton != null) detailUnfriendButton.onClick.AddListener(OnDetailUnfriendClicked); // Remove friend
            if (detailBlockButton != null) detailBlockButton.onClick.AddListener(OnDetailBlockClicked); // Block user
            if (detailProfileButton != null) detailProfileButton.onClick.AddListener(OnDetailProfileClicked); // Inspect full profile
        }

        // Executes core business logic for create runtime friend chat panel.
        private UIFriendChatPanel CreateRuntimeFriendChatPanel()
        {
            UIChatMessage fallbackMessagePrefab = null;
            var worldChatPanel = FindFirstObjectByType<ChatUIManager>(FindObjectsInactive.Include);
            if (worldChatPanel != null)
            {
                fallbackMessagePrefab = worldChatPanel.chatMessagePrefab;
            }

            return UIFriendChatPanel.CreateRuntime(transform, fallbackMessagePrefab);
        }

        // Update visibility for friend tab; it updates all panels active, updates active, updates count texts, and loads friends.
        private void ShowFriendTab()
        {
            SetAllPanelsActive(false);
            friendPanel?.SetActive(true);
            ClearSelectedFriendState(true);
            UpdateCountTexts();
            LoadFriends();

            HighlightTab(friendTabButton, addTabButton);
        }

        // Update visibility for add tab; it updates all panels active, updates active, updates count texts, and loads requests.
        private void ShowAddTab()
        {
            SetAllPanelsActive(false);
            addPanel?.SetActive(true);
            UpdateCountTexts();
            LoadRequests();

            OnSearchClicked();

            HighlightTab(addTabButton, friendTabButton);
        }

        // Executes core business logic for highlight tab.
        private void HighlightTab(Button activeTab, Button inactiveTab)
        {
            if (activeTab != null && activeTab.GetComponent<Image>() != null)
            {
                var img = activeTab.GetComponent<Image>();
                img.color = Color.white;
                if (activeTabSprite != null)
                    img.sprite = activeTabSprite;
            }

            if (inactiveTab != null && inactiveTab.GetComponent<Image>() != null)
            {
                var img = inactiveTab.GetComponent<Image>();
                img.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                if (inactiveTabSprite != null)
                    img.sprite = inactiveTabSprite;
            }
        }

        // Executes core business logic for set all panels active.
        private void SetAllPanelsActive(bool active)
        {
            friendPanel?.SetActive(active);
            addPanel?.SetActive(active);
        }

        // Executes core business logic for hide detail panel.
        private void HideDetailPanel()
        {
            if (detailPanelObj != null) detailPanelObj.SetActive(false);
        }

        // Executes core business logic for clear selected friend state.
        private void ClearSelectedFriendState(bool closeChat)
        {
            selectedProfileId = 0;
            selectedFriendName = null;
            HideDetailPanel();

            if (closeChat)
            {
                CloseFriendChatPanel();
            }
        }

        // Executes core business logic for close friend chat panel.
        private void CloseFriendChatPanel()
        {
            if (friendChatPanel == null)
            {
                friendChatPanel = FindFirstObjectByType<UIFriendChatPanel>(FindObjectsInactive.Include);
            }

            if (friendChatPanel != null)
            {
                friendChatPanel.Close();
            }
        }


        // Executes core business logic for select friend.
        public void SelectFriend(FriendDto friend)
        {
            selectedProfileId = friend.FriendProfileId;
            selectedFriendName = friend.FriendName;
            ShowDetailPanel(friend.FriendName, friend.FriendLevel, friend.Class,
                friend.IsOnline ? $"<color=green>Online</color> - {friend.CurrentMap}" : $"<color=gray>Offline ({friend.LastOnline})</color>", friend.FriendAvatarUrl);

            EnableDetailButtons(showChat: true, showUnfriend: true, showBlock: true, showProfile: true);
            OpenSelectedFriendChat();
        }

        // Executes core business logic for show detail panel.
        // Logic details: validates required non-empty string arguments.
        private void ShowDetailPanel(string name, int level, string charClass, string statusText, string avatarUrl = null)
        {
            if (detailPanelObj != null) detailPanelObj.SetActive(true);
            if (detailNameText != null) detailNameText.text = name;
            if (detailLevelText != null) detailLevelText.text = $"Lv.{level}";
            if (detailClassText != null) detailClassText.text = charClass;
            if (detailStatusText != null)
            {
                detailStatusText.gameObject.SetActive(!string.IsNullOrEmpty(statusText));
                detailStatusText.text = statusText;
            }
            if (detailAvatarImage != null)
            {
                if (string.IsNullOrEmpty(avatarUrl)) avatarUrl = "avatar_1";

                Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
                if (avatarSprite != null)
                {
                    detailAvatarImage.sprite = avatarSprite;
                }
            }
        }

        // Executes core business logic for enable detail buttons.
        private void EnableDetailButtons(bool showChat = false, bool showUnfriend = false, bool showBlock = false, bool showProfile = false)
        {
            if (detailChatButton != null)
            {
                detailChatButton.gameObject.SetActive(showChat);
                detailChatButton.interactable = true;
            }

            if (detailUnfriendButton != null)
            {
                detailUnfriendButton.gameObject.SetActive(showUnfriend);
                detailUnfriendButton.interactable = true;
            }
            if (detailBlockButton != null)
            {
                detailBlockButton.gameObject.SetActive(showBlock);
                detailBlockButton.interactable = true;
            }
            if (detailProfileButton != null)
            {
                detailProfileButton.gameObject.SetActive(showProfile);
                detailProfileButton.interactable = true;
            }
        }

        // Executes core business logic for on detail chat clicked.
        // Logic details: validates numeric boundary constraints.
        private void OnDetailChatClicked()
        {
            OpenSelectedFriendChat();
        }

        // Executes core business logic for open selected friend chat.
        // Logic details: validates numeric boundary constraints.
        private void OpenSelectedFriendChat()
        {
            if (selectedProfileId <= 0)
            {
                Debug.LogWarning("[FriendUIManager] Cannot open friend chat because selectedProfileId is 0.");
                return;
            }

            if (friendChatPanel == null)
            {
                friendChatPanel = FindFirstObjectByType<UIFriendChatPanel>(FindObjectsInactive.Include);
            }

            if (friendChatPanel == null)
            {
                friendChatPanel = CreateRuntimeFriendChatPanel();
            }

            if (friendChatPanel == null)
            {
                Debug.LogWarning("[FriendUIManager] Friend chat panel is not available.");
                return;
            }

            Debug.Log($"[FriendUIManager] OpenSelectedFriendChat -> profileId={selectedProfileId} name={selectedFriendName}");
            friendChatPanel.Open(selectedProfileId, selectedFriendName);
        }

        // Executes core business logic for on detail unfriend clicked.
        // Logic details: validates numeric boundary constraints.
        private void OnDetailUnfriendClicked()
        {
            if (selectedProfileId <= 0)
            {
                ClearSelectedFriendState(true);
                return;
            }
            if (UIPopup.Instance != null)
            {
                UIPopup.Instance.ShowConfirm(
                    "Unfriend",
                    $"Are you sure you want to remove '{selectedFriendName}' from your friend list?",
                    onConfirm: ExecuteUnfriend
                );
            }
            else
            {
                ExecuteUnfriend();
            }
        }

        // Executes core business logic for execute unfriend.
        private void ExecuteUnfriend()
        {
            FriendApi.RemoveFriend(selectedProfileId,
                onSuccess: (res) =>
                {
                    if (UIPopup.Instance != null)
                        UIPopup.Instance.ShowAlert("Success", "Friend removed successfully!");
                    else
                        Debug.Log("Unfriended successfully.");

                    RefreshData();
                },
                onError: (err) =>
                {
                    if (UIPopup.Instance != null)
                        UIPopup.Instance.ShowAlert("Failed", err.Message);
                    else
                        Debug.LogError("Unfriend failed: " + err.Message);
                }
            );
        }

        // Executes core business logic for on detail block clicked.
        // Logic details: validates numeric boundary constraints.
        private void OnDetailBlockClicked()
        {
            if (selectedProfileId <= 0)
            {
                ClearSelectedFriendState(true);
                return;
            }

            if (UIPopup.Instance != null)
            {
                UIPopup.Instance.ShowConfirm(
                    "Block Player",
                    $"Are you sure you want to block '{selectedFriendName}'? They won't be able to send you messages or friend requests.",
                    onConfirm: ExecuteBlock
                );
            }
            else
            {
                ExecuteBlock();
            }
        }

        // Executes core business logic for execute block.
        private void ExecuteBlock()
        {
            SetButtonLoading(detailBlockButton);
            FriendApi.BlockPlayer(selectedProfileId,
                onSuccess: (res) =>
                {
                    ResetButtonText(detailBlockButton, "");
                    ClearSelectedFriendState(true);

                    if (UIPopup.Instance != null)
                        UIPopup.Instance.ShowAlert("Success", "Player blocked successfully.");
                    else
                        Debug.Log("Blocked successfully.");

                    RefreshData();
                },
                onError: (err) =>
                {
                    ResetButtonText(detailBlockButton, "");
                    if (UIPopup.Instance != null)
                        UIPopup.Instance.ShowAlert("Failed", err.Message);
                    else
                        Debug.LogError("Block failed: " + err.Message);
                }
            );
        }

        // Executes core business logic for on detail profile clicked.
        private void OnDetailProfileClicked()
        {
            var panel = FindFirstObjectByType<PlayerProfileUIManager>(FindObjectsInactive.Include);
            if (panel != null)
            {
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);

                if (friendChatPanel != null) friendChatPanel.Close();

                panel.ShowProfile(selectedProfileId, "");
            }
            else
            {
                Debug.LogWarning("[FriendUIManager] PlayerProfileUIManager not found in scene!");
            }
        }

        // Executes core business logic for set button loading.
        private void SetButtonLoading(Button btn)
        {
            if (btn == null) return;
            btn.interactable = false;
            var txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "Loading...";
        }

        // Executes core business logic for reset button text.
        private void ResetButtonText(Button btn, string text)
        {
            if (btn == null) return;
            btn.interactable = true;
            var txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = text;
        }

        // Executes core business logic for load friends.
        private void LoadFriends()
        {
            FriendApi.GetFriendList(friends =>
            {
                Debug.Log($"[LoadFriends] API returned {friends.Count} friends. Filtering Accepted status...");
                foreach(var f in friends) {
                    Debug.Log($"[LoadFriends] Friend: {f.FriendName} | Status: {f.Status}");
                }

                currentFriends = friends.Where(f => f.Status == "Accepted" || f.Status == "accepted").ToList();
                Debug.Log($"[LoadFriends] After filter: {currentFriends.Count} accepted friends. Instantiating list...");
                UpdateFriendUI();

                if (addPanel != null && addPanel.activeSelf)
                {
                    UpdateSearchUI();
                }
            }, err => Debug.LogError($"Failed to load friends: {err.Message}"));
        }

        // Executes core business logic for load requests.
        private void LoadRequests()
        {
            FriendApi.GetFriendRequests(requests =>
            {
                currentRequests = requests.ToList();
                UpdateRequestUI();
            }, err => Debug.LogError($"Failed to load requests: {err.Message}"));
        }

        // Executes core business logic for update friend ui.
        private void UpdateFriendUI()
        {
            if (friendListContainer == null) return;
            foreach (Transform child in friendListContainer) Destroy(child.gameObject);

            var sortedFriends = currentFriends
                .OrderByDescending(f => f.IsOnline)
                .ThenByDescending(f => f.FriendLevel)
                .ThenBy(f => f.FriendName)
                .ToList();

            foreach (var friend in sortedFriends)
            {
                var entry = Instantiate(friendEntryPrefab, friendListContainer);
                entry.SetupAsFriend(friend, this);
            }

            UpdateCountTexts();

            CloseChatIfSelectedFriendIsGone();
        }

        // Executes core business logic for close chat if selected friend is gone.
        // Logic details: validates numeric boundary constraints.
        private void CloseChatIfSelectedFriendIsGone()
        {
            if (selectedProfileId <= 0)
            {
                if (currentFriends.Count == 0)
                {
                    CloseFriendChatPanel();
                }

                return;
            }

            bool selectedStillExists = currentFriends.Any(f => f.FriendProfileId == selectedProfileId);
            if (!selectedStillExists)
            {
                ClearSelectedFriendState(true);
            }
        }

        // Executes core business logic for update request ui.
        private void UpdateRequestUI()
        {
            if (requestListContainer == null) return;
            foreach (Transform child in requestListContainer) Destroy(child.gameObject);

            foreach (var req in currentRequests)
            {
                var entry = Instantiate(requestEntryPrefab, requestListContainer);
                entry.SetupAsRequest(req, this);
            }

            UpdateCountTexts();
        }

        // Executes core business logic for update count texts.
        private void UpdateCountTexts()
        {
            if (friendCountText != null && friendCountText == requestCountText)
            {
                friendCountText.text = addPanel != null && addPanel.activeSelf
                    ? $"{currentRequests.Count}/100"
                    : $"Friends: {currentFriends.Count}/100";
                return;
            }

            if (friendCountText != null)
                friendCountText.text = $"Friends: {currentFriends.Count}/100";

            if (requestCountText != null)
                requestCountText.text = $"{currentRequests.Count}/100";
        }

        // Executes core business logic for on search clicked.
        // Logic details: validates required non-empty string arguments.
        private void OnSearchClicked()
        {
            string query = searchInput != null && !string.IsNullOrEmpty(searchInput.text) ? searchInput.text.Trim() : "";

            FriendApi.SearchPlayers(query, results =>
            {
                searchResults = results;
                UpdateSearchUI();
            }, err => Debug.LogError($"Search failed: {err.Message}"));
        }

        // Executes core business logic for update search ui.
        private void UpdateSearchUI()
        {
            if (searchListContainer == null) return;
            foreach (Transform child in searchListContainer) Destroy(child.gameObject);
            searchListContainer.DetachChildren();

            if (searchResults != null)
            {
                foreach (var result in GetAddableSearchResults())
                {
                    var entry = Instantiate(searchEntryPrefab, searchListContainer);
                    entry.SetupAsSearch(result, this);
                }
            }
        }

        // Executes core business logic for get addable search results.
        // Logic details: validates numeric boundary constraints.
        private IEnumerable<FriendSearchDto> GetAddableSearchResults()
        {
            int currentPlayerId = GetCurrentPlayerProfileId();
            var friendProfileIds = new HashSet<int>(currentFriends.Select(friend => friend.FriendProfileId));

            return searchResults
                .Where(result => result != null)
                .Where(result => result.ProfileId > 0)
                .Where(result => result.ProfileId != currentPlayerId)
                .Where(result => !friendProfileIds.Contains(result.ProfileId))
                .Where(result => result.RelationshipStatus != FriendRelationshipStatus.Self)
                .Where(result => result.RelationshipStatus != FriendRelationshipStatus.Friend)
                .GroupBy(result => result.ProfileId)
                .Select(group => group.First());
        }

        // Executes core business logic for get current player profile id.
        // Logic details: validates numeric boundary constraints.
        private static int GetCurrentPlayerProfileId()
        {
            int profileId = GameStateService.Instance != null
                ? GameStateService.Instance.PlayerProfileId
                : 0;

            if (profileId <= 0)
            {
                profileId = WorldState.PlayerProfileId;
            }

            if (profileId <= 0)
            {
                profileId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);
            }

            return profileId;
        }

        // Return the cached access token when available; otherwise load it from PlayerPrefs and cache the value.
        public string GetToken() => "";
    }
}
