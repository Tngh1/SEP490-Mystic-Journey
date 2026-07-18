using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using System.Linq;
using MysticJourney.UI; // For UIPopupManager

namespace UI.Friend
{
    public class UIFriendPanel : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button friendTabButton;
        [SerializeField] private Button addTabButton;

        [Header("Panels")]
        [SerializeField] private GameObject friendPanel; // Contains friendListContainer and detailPanelObj
        [SerializeField] private GameObject addPanel; // Contains requestListContainer, searchInput, searchListContainer

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

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            if (friendTabButton != null) friendTabButton.onClick.AddListener(ShowFriendTab);
            if (addTabButton != null) addTabButton.onClick.AddListener(ShowAddTab);

            if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked);
            
            SetupDetailButtons();

            ShowFriendTab();
        }

        private void OnEnable()
        {
            RefreshData();
        }

        public void RefreshData()
        {
            LoadFriends();
            LoadRequests();
            
            if (addPanel != null && addPanel.activeSelf)
            {
                OnSearchClicked();
            }
        }

        private void SetupDetailButtons()
        {
            if (detailChatButton == null && detailPanelObj != null)
            {
                var chatButtonTransform = detailPanelObj.transform.Find("ChatButton");
                if (chatButtonTransform != null) detailChatButton = chatButtonTransform.GetComponent<Button>();
            }

            if (friendChatPanel == null)
            {
                friendChatPanel = FindFirstObjectByType<UIFriendChatPanel>(FindObjectsInactive.Include);
            }

            if (friendChatPanel == null)
            {
                friendChatPanel = CreateRuntimeFriendChatPanel();
            }

            if (detailChatButton != null) detailChatButton.onClick.AddListener(OnDetailChatClicked);
            if (detailUnfriendButton != null) detailUnfriendButton.onClick.AddListener(OnDetailUnfriendClicked);
            if (detailBlockButton != null) detailBlockButton.onClick.AddListener(OnDetailBlockClicked);
            if (detailProfileButton != null) detailProfileButton.onClick.AddListener(OnDetailProfileClicked);
        }

        private UIFriendChatPanel CreateRuntimeFriendChatPanel()
        {
            UIChatMessage fallbackMessagePrefab = null;
            var worldChatPanel = FindFirstObjectByType<UIChatPanel>(FindObjectsInactive.Include);
            if (worldChatPanel != null)
            {
                fallbackMessagePrefab = worldChatPanel.chatMessagePrefab;
            }

            return UIFriendChatPanel.CreateRuntime(transform, fallbackMessagePrefab);
        }

        private void ShowFriendTab()
        {
            SetAllPanelsActive(false);
            friendPanel?.SetActive(true);
            HideDetailPanel(); // Hide until a friend is clicked
            LoadFriends();
            HighlightTab(friendTabButton, addTabButton);
        }

        private void ShowAddTab()
        {
            SetAllPanelsActive(false);
            addPanel?.SetActive(true);
            LoadRequests(); // Left column of Add tab
            
            // Automatically trigger search to get random players when tab opens
            OnSearchClicked(); // Right column of Add tab
            
            HighlightTab(addTabButton, friendTabButton);
        }

        private void HighlightTab(Button activeTab, Button inactiveTab)
        {
            if (activeTab != null && activeTab.GetComponent<Image>() != null)
                activeTab.GetComponent<Image>().color = Color.white;
                
            if (inactiveTab != null && inactiveTab.GetComponent<Image>() != null)
                inactiveTab.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 1f); // Màu xám tối
        }

        private void SetAllPanelsActive(bool active)
        {
            friendPanel?.SetActive(active);
            addPanel?.SetActive(active);
        }

        private void HideDetailPanel()
        {
            if (detailPanelObj != null) detailPanelObj.SetActive(false);
        }

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

        // -----------------------------
        // Master-Detail Selection Logic
        // -----------------------------
        
        // Overload for FriendList
        public void SelectFriend(FriendDto friend)
        {
            selectedProfileId = friend.FriendProfileId;
            selectedFriendName = friend.FriendName;
            ShowDetailPanel(friend.FriendName, friend.FriendLevel, friend.Class, 
                friend.IsOnline ? $"<color=green>Online</color> - {friend.CurrentMap}" : $"<color=gray>Offline ({friend.LastOnline})</color>", friend.FriendAvatarUrl);
            
            EnableDetailButtons(showChat: true, showUnfriend: true, showBlock: true, showProfile: true);
            OpenSelectedFriendChat();
        }

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
                if (string.IsNullOrEmpty(avatarUrl)) avatarUrl = "avatar_1"; // Default avatar
                
                Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
                if (avatarSprite != null)
                {
                    detailAvatarImage.sprite = avatarSprite;
                }
            }
        }

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

        // -----------------------------
        // Button Actions
        // -----------------------------
        private void OnDetailChatClicked()
        {
            OpenSelectedFriendChat();
        }

        private void OpenSelectedFriendChat()
        {
            if (selectedProfileId <= 0)
            {
                Debug.LogWarning("[UIFriendPanel] Cannot open friend chat because selectedProfileId is 0.");
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
                Debug.LogWarning("[UIFriendPanel] Friend chat panel is not available.");
                return;
            }

            Debug.Log($"[UIFriendPanel] OpenSelectedFriendChat -> profileId={selectedProfileId} name={selectedFriendName}");
            friendChatPanel.Open(selectedProfileId, selectedFriendName);
        }

        private void OnDetailUnfriendClicked()
        {
            if (selectedProfileId <= 0)
            {
                ClearSelectedFriendState(true);
                return;
            }
            if (UIPopupManager.Instance != null)
            {
                UIPopupManager.Instance.ShowConfirm(
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

        private void ExecuteUnfriend()
        {
            FriendApi.RemoveFriend(selectedProfileId, 
                onSuccess: (res) => 
                {
                    if (UIPopupManager.Instance != null)
                        UIPopupManager.Instance.ShowAlert("Success", "Friend removed successfully!");
                    else
                        Debug.Log("Unfriended successfully.");
                        
                    RefreshData();
                },
                onError: (err) => 
                {
                    if (UIPopupManager.Instance != null)
                        UIPopupManager.Instance.ShowAlert("Failed", err.Message);
                    else
                        Debug.LogError("Unfriend failed: " + err.Message);
                }
            );
        }

        private void OnDetailBlockClicked()
        {
            if (selectedProfileId <= 0)
            {
                ClearSelectedFriendState(true);
                return;
            }

            if (UIPopupManager.Instance != null)
            {
                UIPopupManager.Instance.ShowConfirm(
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

        private void ExecuteBlock()
        {
            SetButtonLoading(detailBlockButton);
            FriendApi.BlockPlayer(selectedProfileId, 
                onSuccess: (res) => 
                {
                    ResetButtonText(detailBlockButton, "");
                    ClearSelectedFriendState(true);
                    
                    if (UIPopupManager.Instance != null)
                        UIPopupManager.Instance.ShowAlert("Success", "Player blocked successfully.");
                    else
                        Debug.Log("Blocked successfully.");
                        
                    RefreshData();
                },
                onError: (err) => 
                {
                    ResetButtonText(detailBlockButton, "");
                    if (UIPopupManager.Instance != null)
                        UIPopupManager.Instance.ShowAlert("Failed", err.Message);
                    else
                        Debug.LogError("Block failed: " + err.Message);
                }
            );
        }

        private void OnDetailProfileClicked()
        {
            var panel = FindFirstObjectByType<UIFriendProfilePanel>(FindObjectsInactive.Include);
            if (panel != null)
            {
                // Deselect any focused UI (e.g. chat InputField) to prevent it stealing focus
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
                
                // Close friend chat panel explicitly to stop FocusInput loops
                if (friendChatPanel != null) friendChatPanel.Close();
                
                panel.ShowProfile(selectedProfileId, "");
            }
            else
            {
                Debug.LogWarning("[UIFriendPanel] UIFriendProfilePanel not found in scene!");
            }
        }

        private void SetButtonLoading(Button btn)
        {
            if (btn == null) return;
            btn.interactable = false;
            var txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = "Loading...";
        }

        private void ResetButtonText(Button btn, string text)
        {
            if (btn == null) return;
            btn.interactable = true;
            var txt = btn.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = text;
        }

        // -----------------------------
        // Data Loading
        // -----------------------------
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
            }, err => Debug.LogError($"Failed to load friends: {err.Message}"));
        }

        private void LoadRequests()
        {
            FriendApi.GetFriendRequests(requests =>
            {
                currentRequests = requests.ToList();
                UpdateRequestUI();
            }, err => Debug.LogError($"Failed to load requests: {err.Message}"));
        }

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

            if (friendCountText != null)
                friendCountText.text = $"Friends: {currentFriends.Count}/100"; // Can be adjusted with limits later

            CloseChatIfSelectedFriendIsGone();
        }

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

        private void UpdateRequestUI()
        {
            if (requestListContainer == null) return;
            foreach (Transform child in requestListContainer) Destroy(child.gameObject);

            foreach (var req in currentRequests)
            {
                var entry = Instantiate(requestEntryPrefab, requestListContainer);
                entry.SetupAsRequest(req, this);
            }

            if (requestCountText != null)
                requestCountText.text = $"{currentRequests.Count}/100"; // As seen in image 2
        }

        private void OnSearchClicked()
        {
            string query = searchInput != null && !string.IsNullOrEmpty(searchInput.text) ? searchInput.text.Trim() : "";
            
            FriendApi.SearchPlayers(query, results =>
            {
                searchResults = results;
                UpdateSearchUI();
            }, err => Debug.LogError($"Search failed: {err.Message}"));
        }

        private void UpdateSearchUI()
        {
            if (searchListContainer == null) return;
            foreach (Transform child in searchListContainer) Destroy(child.gameObject);
            searchListContainer.DetachChildren();

            if (searchResults != null)
            {
                foreach (var result in searchResults)
                {
                    var entry = Instantiate(searchEntryPrefab, searchListContainer);
                    entry.SetupAsSearch(result, this);
                }
            }
        }

        public string GetToken() => ""; // Kept for legacy signature
    }
}
