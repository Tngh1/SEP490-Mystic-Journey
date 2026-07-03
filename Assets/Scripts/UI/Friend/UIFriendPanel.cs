using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using API.Endpoints;
using API.Models;
using System.Linq;

namespace UI.Friend
{
    public class UIFriendPanel : MonoBehaviour
    {
        [Header("Tabs")]
        [SerializeField] private Button friendTabButton;
        [SerializeField] private Button requestTabButton;
        [SerializeField] private Button searchTabButton;
        [SerializeField] private Button blockedTabButton;

        [SerializeField] private GameObject friendPanel;
        [SerializeField] private GameObject requestPanel;
        [SerializeField] private GameObject searchPanel;
        [SerializeField] private GameObject blockedPanel;

        [Header("Friend List")]
        [SerializeField] private Transform friendListContainer;
        [SerializeField] private UIFriendEntry friendEntryPrefab;
        [SerializeField] private TMP_Text friendCountText;

        [Header("Request List")]
        [SerializeField] private Transform requestListContainer;
        [SerializeField] private UIFriendEntry requestEntryPrefab;
        [SerializeField] private TMP_Text requestCountText;

        [Header("Search Players")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private Button searchButton;
        [SerializeField] private Transform searchListContainer;
        [SerializeField] private UIFriendEntry searchEntryPrefab;

        [Header("Blocked List")]
        [SerializeField] private Transform blockedListContainer;
        [SerializeField] private UIFriendEntry blockedEntryPrefab;

        [Header("UI Control")]
        [SerializeField] private Button closeButton;

        private List<FriendDto> currentFriends = new List<FriendDto>();
        private List<PendingFriendRequestDto> currentRequests = new List<PendingFriendRequestDto>();
        private List<FriendSearchDto> searchResults = new List<FriendSearchDto>();
        private List<FriendProfileDto> currentBlocks = new List<FriendProfileDto>();
        private string token;

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            if (friendTabButton != null) friendTabButton.onClick.AddListener(ShowFriendTab);
            if (requestTabButton != null) requestTabButton.onClick.AddListener(ShowRequestTab);
            if (searchTabButton != null) searchTabButton.onClick.AddListener(ShowSearchTab);
            if (blockedTabButton != null) blockedTabButton.onClick.AddListener(ShowBlockedTab);

            if (searchButton != null) searchButton.onClick.AddListener(OnSearchClicked);
            
            // Assume AuthSystem provides token or we get it from elsewhere.
            token = PlayerPrefs.GetString("AuthToken", ""); 
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
            LoadBlocks();
            if (searchPanel != null && searchPanel.activeSelf)
            {
                OnSearchClicked();
            }
        }

        private void ShowFriendTab()
        {
            SetAllPanelsActive(false);
            friendPanel?.SetActive(true);
            LoadFriends();
        }

        private void ShowRequestTab()
        {
            SetAllPanelsActive(false);
            requestPanel?.SetActive(true);
            LoadRequests();
        }

        private void ShowSearchTab()
        {
            SetAllPanelsActive(false);
            searchPanel?.SetActive(true);
        }

        private void ShowBlockedTab()
        {
            SetAllPanelsActive(false);
            blockedPanel?.SetActive(true);
            LoadBlocks();
        }

        private void SetAllPanelsActive(bool active)
        {
            friendPanel?.SetActive(active);
            requestPanel?.SetActive(active);
            searchPanel?.SetActive(active);
            blockedPanel?.SetActive(active);
        }

        private void LoadFriends()
        {
            FriendApi.GetFriendList(token, friends =>
            {
                currentFriends = friends.Where(f => f.Status == "Accepted").ToList();
                UpdateFriendUI();
            }, err => Debug.LogError($"Failed to load friends: {err}"));
        }

        private void LoadRequests()
        {
            FriendApi.GetFriendRequests(token, requests =>
            {
                currentRequests = requests.ToList();
                UpdateRequestUI();
            }, err => Debug.LogError($"Failed to load requests: {err}"));
        }

        private void LoadBlocks()
        {
            FriendApi.GetFriendBlocks(token, blocks =>
            {
                currentBlocks = blocks.ToList();
                UpdateBlockedUI();
            }, err => Debug.LogError($"Failed to load blocks: {err}"));
        }

        private void UpdateFriendUI()
        {
            if (friendListContainer == null) return;
            foreach (Transform child in friendListContainer) Destroy(child.gameObject);

            // Sorting logic: Online first, then Level desc, then Alphabetical
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
                friendCountText.text = $"Friends: {currentFriends.Count}/100";
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
                requestCountText.text = $"Requests: {currentRequests.Count}";
        }

        private void UpdateBlockedUI()
        {
            if (blockedListContainer == null) return;
            foreach (Transform child in blockedListContainer) Destroy(child.gameObject);

            foreach (var block in currentBlocks)
            {
                var entry = Instantiate(blockedEntryPrefab, blockedListContainer);
                entry.SetupAsBlock(block, this);
            }
        }

        private void OnSearchClicked()
        {
            if (string.IsNullOrWhiteSpace(searchInput.text)) return;
            
            FriendApi.SearchPlayers(token, searchInput.text, results =>
            {
                searchResults = results;
                UpdateSearchUI();
            }, err => Debug.LogError($"Search failed: {err}"));
        }

        private void UpdateSearchUI()
        {
            if (searchListContainer == null) return;
            foreach (Transform child in searchListContainer) Destroy(child.gameObject);

            foreach (var result in searchResults)
            {
                var entry = Instantiate(searchEntryPrefab, searchListContainer);
                entry.SetupAsSearch(result, this);
            }
        }

        public string GetToken() => token;
    }
}
