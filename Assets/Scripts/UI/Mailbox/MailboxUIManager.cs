using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.API.Core;

namespace MysticJourney.Screen.Mail
{
    // Executes core business logic for mono behaviour.
    public class MailboxUIManager : MonoBehaviour
    {
        public static event Action MailboxStateChanged;

        [Header("Left Panel - General")]
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject mailItemPrefab;
        [SerializeField] private TMP_Text emptyListText;

        [Header("Left Panel - Pagination")]
        [SerializeField] private GameObject paginationContainer;
        [SerializeField] private Button previousButton;
        [FormerlySerializedAs("pageNumber2")]
        [SerializeField] private TMP_Text pageInfoText;
        [SerializeField] private Button nextButton;

        [Header("Right Panel - General")]
        [SerializeField] private GameObject rightPanel;

        [Header("Right Panel - Header & Body")]
        [SerializeField] private GameObject bodyContainer;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text typeText;
        [SerializeField] private TMP_Text bodyText;

        [Header("Right Panel - Footer (Rewards)")]
        [SerializeField] private GameObject rewardsContainer;
        [SerializeField] private Transform itemsContainer;
        [SerializeField] private GameObject itemSlotPrefab;

        [Header("Right Panel - Buttons")]
        [SerializeField] private Button claimButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private GameObject claimedStamp;

        [Header("Main Setup")]
        [SerializeField] private Button closeButton;

        private MailboxSummaryResponse _currentSelectedMailboxSummary;
        private MailboxItemUI _currentSelectedMailboxUI;
        private Scrollbar _rewardsScrollbar;

        private int _currentPage = 1;
        private int _totalPages = 1;
        private readonly int _itemsPerPage = 5;
        private bool _isLoading;

        // Binds close buttons, claim/delete handlers, and pagination controls.
        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false)); // Close mailbox popup
                if (closeButton.GetComponent<UIHoverScaleEffect>() == null)
                    closeButton.gameObject.AddComponent<UIHoverScaleEffect>();
            }
            if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked); // Claim reward attachment
            if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked); // Delete read message
            if (previousButton != null) previousButton.onClick.AddListener(() => GoToPage(_currentPage - 1)); // Page back
            if (nextButton != null) nextButton.onClick.AddListener(() => GoToPage(_currentPage + 1)); // Page forward
        }

        // Resets active page to 1, clears preview pane, and loads messages from backend.
        private void OnEnable()
        {
            if (rightPanel != null) rightPanel.SetActive(true);
            HideRightPanelContent(); // Hide preview pane until message selected
            _currentPage = 1;
            LoadMailboxesFromBackend(); // Query inbox
        }

        // Executes core business logic for hide right panel content.
        private void HideRightPanelContent()
        {
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (typeText != null) typeText.gameObject.SetActive(false);
            if (bodyContainer != null) bodyContainer.SetActive(false);
            SetRewardsVisible(false);
            if (claimButton != null) claimButton.gameObject.SetActive(false);
            if (deleteButton != null) deleteButton.gameObject.SetActive(false);
            if (claimedStamp != null) claimedStamp.SetActive(false);
        }

        // Executes core business logic for show right panel content.
        private void ShowRightPanelContent()
        {
            if (titleText != null) titleText.gameObject.SetActive(true);
            if (typeText != null) typeText.gameObject.SetActive(true);
            if (bodyContainer != null) bodyContainer.SetActive(true);
            if (deleteButton != null) deleteButton.gameObject.SetActive(true);
        }

        // Executes core business logic for set rewards visible.
        private void SetRewardsVisible(bool visible)
        {
            if (rewardsContainer != null) rewardsContainer.SetActive(visible);

            var scrollbar = GetRewardsScrollbar();
            if (scrollbar != null) scrollbar.gameObject.SetActive(visible);
        }

        // Executes core business logic for get rewards scrollbar.
        private Scrollbar GetRewardsScrollbar()
        {
            if (_rewardsScrollbar != null) return _rewardsScrollbar;
            if (rewardsContainer == null) return null;

            var scrollRect = rewardsContainer.GetComponent<ScrollRect>();
            if (scrollRect == null) return null;

            _rewardsScrollbar = scrollRect.horizontalScrollbar != null
                ? scrollRect.horizontalScrollbar
                : scrollRect.verticalScrollbar;

            return _rewardsScrollbar;
        }

        // Fetches paginated mailbox messages for local player from backend API.
        private void LoadMailboxesFromBackend()
        {
            _isLoading = true;
            SetPaginationInteractable(false); // Disable pagination while loading

            if (paginationContainer != null) paginationContainer.SetActive(false);
            if (emptyListText != null) emptyListText.gameObject.SetActive(false);
            if (contentContainer != null)
            {
                foreach (Transform child in contentContainer)
                {
                    child.gameObject.SetActive(false);
                }
            }

            MailboxApi.Instance.GetMyMailboxes(
                _currentPage,
                _itemsPerPage,
                response => PopulateMailboxList(response),
                onError: error =>
                {
                    _isLoading = false;
                    SetPaginationInteractable(true);
                    Debug.LogError($"[MailboxUI] Lỗi tải thư: {error.Message}");
                }
            );
        }

        // Executes core business logic for set pagination interactable.
        private void SetPaginationInteractable(bool on)
        {
            if (!on)
            {
                if (previousButton != null) previousButton.interactable = false;
                if (nextButton != null) nextButton.interactable = false;
            }
        }

        // Executes core business logic for populate mailbox list.
        private void PopulateMailboxList(MailboxListPagedResponse response)
        {
            if (response == null || response.Items == null || response.Items.Length == 0)
            {
                if (_currentPage > 1)
                {
                    _currentPage--;
                    LoadMailboxesFromBackend();
                    return;
                }

                if (emptyListText != null)
                {
                    emptyListText.gameObject.SetActive(true);
                    emptyListText.text = "No mailbox has arrived yet.";
                }

                if (contentContainer != null)
                {
                    foreach (Transform child in contentContainer)
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                if (paginationContainer != null) paginationContainer.SetActive(false);
                HideRightPanelContent();

                UpdatePaginationUI(1, 0);
                return;
            }

            if (emptyListText != null) emptyListText.gameObject.SetActive(false);
            if (paginationContainer != null) paginationContainer.SetActive(true);

            _totalPages = response.TotalPages;
            int dataCount = response.Items.Length;
            int maxItemsToLoop = Mathf.Max(dataCount, contentContainer.childCount);

            for (int i = 0; i < maxItemsToLoop; i++)
            {
                if (i < dataCount)
                {
                    GameObject itemGO;

                    if (i < contentContainer.childCount)
                        itemGO = contentContainer.GetChild(i).gameObject;
                    else
                        itemGO = Instantiate(mailItemPrefab, contentContainer);

                    itemGO.SetActive(true);

                    var mailboxUI = itemGO.GetComponent<MailboxItemUI>();
                    if (mailboxUI != null)
                        mailboxUI.Setup(response.Items[i], OnMailboxClicked);
                }
                else
                {
                    contentContainer.GetChild(i).gameObject.SetActive(false);
                }
            }

            UpdatePaginationUI(response.Page, response.TotalPages);
        }

        // Executes core business logic for update pagination ui.
        private void UpdatePaginationUI(int currentPage, int totalPages)
        {
            _isLoading = false;
            _currentPage = currentPage;
            _totalPages = totalPages;

            if (pageInfoText != null)
                pageInfoText.text = $"{currentPage} / {Mathf.Max(1, totalPages)}";

            if (previousButton != null) previousButton.interactable = currentPage > 1;
            if (nextButton != null) nextButton.interactable = currentPage < totalPages;
        }

        // Executes core business logic for go to page.
        private void GoToPage(int page)
        {
            if (_isLoading) return;
            if (page < 1 || page > _totalPages || page == _currentPage) return;
            _currentPage = page;

            if (rightPanel != null) rightPanel.SetActive(false);

            LoadMailboxesFromBackend();
        }

        // Executes core business logic for on mailbox clicked.
        private void OnMailboxClicked(MailboxItemUI clickedUI)
        {
            _currentSelectedMailboxUI = clickedUI;
            _currentSelectedMailboxSummary = clickedUI.GetMailboxData();

            MailboxApi.Instance.GetById(
                _currentSelectedMailboxSummary.MailboxId,
                onSuccess: mailboxDetail => DisplayMailboxDetail(mailboxDetail, clickedUI),
                onError: error => Debug.LogError($"[MailboxUI] Lỗi lấy chi tiết thư: {error.Message}")
            );
        }

        // Executes core business logic for display mailbox detail.
        private void DisplayMailboxDetail(MailboxDetailResponse mailboxData, MailboxItemUI clickedUI)
        {
            if (rightPanel != null) rightPanel.SetActive(true);

            ShowRightPanelContent();

            if (titleText != null) titleText.text = mailboxData.Title;
            if (typeText != null)
            {
                typeText.text = mailboxData.Type;
                typeText.color = GetMailboxTypeColor(mailboxData.Type);
            }
            if (bodyText != null) bodyText.text = mailboxData.Content;

            DisplayRewards(mailboxData);

            if (!mailboxData.IsRead)
            {
                MailboxApi.Instance.MarkAsRead(
                    mailboxData.MailboxId,
                    res =>
                    {
                        clickedUI.MarkAsReadLocally();
                        MailboxStateChanged?.Invoke();
                    },
                    err => { }
                );
            }
        }

        // Executes core business logic for display rewards.
        private void DisplayRewards(MailboxDetailResponse mailboxData)
        {
            bool hasGold = mailboxData.AttachedGold > 0;
            bool hasGems = mailboxData.AttachedGems > 0;
            bool hasItems = mailboxData.AttachedItems != null && mailboxData.AttachedItems.Length > 0;
            bool hasRewards = hasGold || hasGems || hasItems;

            SetRewardsVisible(hasRewards);

            var allRewards = new List<UIItemDisplayData>();

            if (hasGold)
            {
                var goldDisplayData = new UIItemDisplayData
                {
                    itemId = -1,
                    itemName = "Gold",
                    icon = GetIconFromDatabase("Gold", "Currency"),
                    quantity = (int)mailboxData.AttachedGold,
                    rarity = "Common",
                    rawData = new MailboxRewardItemResponse { ItemId = -1, ItemName = "Gold", Quantity = (int)mailboxData.AttachedGold }
                };
                allRewards.Add(goldDisplayData);
            }

            if (hasGems)
            {
                var gemDisplayData = new UIItemDisplayData
                {
                    itemId = -2,
                    itemName = "Gem",
                    icon = GetIconFromDatabase("Gem", "Currency"),
                    quantity = (int)mailboxData.AttachedGems,
                    rarity = "Rare",
                    rawData = new MailboxRewardItemResponse { ItemId = -2, ItemName = "Gem", Quantity = (int)mailboxData.AttachedGems }
                };
                allRewards.Add(gemDisplayData);
            }

            if (hasItems)
            {
                foreach (var item in mailboxData.AttachedItems)
                {
                    allRewards.Add(CreateItemDisplayData(item));
                }
            }

            SetupRewardSlots(allRewards);

            if (mailboxData.IsClaimed)
            {
                if (claimButton != null) claimButton.gameObject.SetActive(false);
                if (claimedStamp != null) claimedStamp.SetActive(true);
            }
            else if (IsExpired(mailboxData.ExpiredAt))
            {
                if (claimButton != null) claimButton.gameObject.SetActive(false);
                if (claimedStamp != null) claimedStamp.SetActive(false);
            }
            else
            {
                if (claimButton != null) claimButton.gameObject.SetActive(hasRewards);
                if (claimedStamp != null) claimedStamp.SetActive(false);
            }
        }

        // Executes core business logic for is expired.
        // Logic details: validates required non-empty string arguments.
        // Returns a boolean indicating operation success.
        private static bool IsExpired(string expiredAt)
        {
            return !string.IsNullOrEmpty(expiredAt)
                && DateTime.TryParse(expiredAt, out DateTime expiry)
                && expiry <= DateTime.UtcNow;
        }

        // Executes core business logic for setup reward slots.
        private void SetupRewardSlots(List<UIItemDisplayData> rewards)
        {
            if (itemsContainer == null)
                return;

            foreach (Transform child in itemsContainer)
            {
                Destroy(child.gameObject);
            }

            if (rewards == null || rewards.Count == 0)
                return;

            foreach (var displayData in rewards)
            {
                GameObject slotObj;
                if (itemSlotPrefab != null)
                {
                    slotObj = Instantiate(itemSlotPrefab, itemsContainer);
                }
                else
                {
                    slotObj = new GameObject("RewardItem");
                    slotObj.transform.SetParent(itemsContainer);
                    slotObj.AddComponent<RectTransform>().sizeDelta = new Vector2(80, 80);
                }

                // Supported equipment slots: None, Weapon, Armor, Helmet, Gloves, Boots, Ring, Necklace, or Shield.
                var slot = slotObj.GetComponent<UIBaseItemSlot>();
                if (slot != null)
                {
                    slot.SetupCore(displayData);
                }
            }
        }

        // Executes core business logic for get icon from database.
        private Sprite GetIconFromDatabase(string itemName, string itemType)
        {
            if (ItemIconDatabase.Instance != null)
            {
                var icon = ItemIconDatabase.Instance.GetIcon(itemName, itemType);
                if (icon != null)
                    return icon;
            }
            return null;
        }

        // Executes core business logic for create item display data.
        private UIItemDisplayData CreateItemDisplayData(MailboxRewardItemResponse item)
        {
            var displayData = new UIItemDisplayData
            {
                itemId = item.ItemId,
                itemName = item.ItemName,
                quantity = item.Quantity,
                rarity = "Common",
                rawData = item
            };

            displayData.icon = GetIconFromDatabase(item.ItemName, null);

            if (displayData.icon == null && !string.IsNullOrWhiteSpace(item.IconUrl))
            {
                var cached = RemoteSpriteCache.GetCached(item.IconUrl);
                if (cached != null)
                    displayData.icon = cached;
            }

            return displayData;
        }

        // Executes core business logic for on claim clicked.
        private void OnClaimClicked()
        {
            if (_currentSelectedMailboxSummary == null || claimButton == null) return;
            claimButton.interactable = false;

            MailboxApi.Instance.ClaimReward(
                mailboxId: _currentSelectedMailboxSummary.MailboxId,
                onSuccess: response =>
                {
                    SetRewardsVisible(false);
                    if (claimButton != null) claimButton.gameObject.SetActive(false);
                    claimButton.interactable = true;
                    if (claimedStamp != null) claimedStamp.SetActive(true);
                    _currentSelectedMailboxUI?.MarkAsClaimedLocally();

                    if (PlayerHUDUIManager.Instance != null)
                        PlayerHUDUIManager.Instance.RefreshHUD();

                },
                onError: error =>
                {
                    claimButton.interactable = true;
                    Debug.LogError($"[MailboxUI] Claim reward failed: {error.Message}");
                }
            );
        }

        // Executes core business logic for on delete clicked.
        private void OnDeleteClicked()
        {
            if (_currentSelectedMailboxSummary == null || deleteButton == null) return;

            if (_currentSelectedMailboxSummary.HasClaimableReward && !_currentSelectedMailboxSummary.IsClaimed
                && !IsExpired(_currentSelectedMailboxSummary.ExpiredAt))
            {
                ShowConfirmPopup(
                    "This mailbox still has unclaimed rewards.\nPlease claim the rewards before deleting it.",
                    allowDelete: false);
            }
            else
            {
                PerformDeleteMailbox();
            }
        }

        // Executes core business logic for show confirm popup.
        private void ShowConfirmPopup(string message, bool allowDelete = true)
        {
            if (allowDelete)
            {
                UIPopupBox.Show(
                    transform,
                    "Delete Mail",
                    message,
                    PerformDeleteMailbox,
                    confirmText: "Delete",
                    cancelText: "Cancel");
                return;
            }

            UIPopupBox.Notify(transform, "Mailbox", message);
        }

        // Executes core business logic for perform delete mailbox.
        private void PerformDeleteMailbox()
        {
            if (_currentSelectedMailboxSummary == null) return;

            int mailboxId = _currentSelectedMailboxSummary.MailboxId;

            MailboxApi.Instance.Delete(
                mailboxId,
                onSuccess: res =>
                {
                    if (_currentSelectedMailboxSummary != null && _currentSelectedMailboxSummary.MailboxId == mailboxId)
                    {
                        _currentSelectedMailboxSummary = null;
                        _currentSelectedMailboxUI = null;
                    }
                    HideRightPanelContent();
                    MailboxStateChanged?.Invoke();
                    LoadMailboxesFromBackend();
                },
                onError: err =>
                {
                    Debug.LogError($"[MailboxUI] Xóa thư thất bại: {err.Message}");
                    ShowConfirmPopup(
                        string.IsNullOrEmpty(err.Message) ? "Failed to delete this mail." : err.Message,
                        allowDelete: false);
                }
            );
        }

        // Executes core business logic for get mailbox type color.
        // Logic details: validates required non-empty string arguments.
        private Color GetMailboxTypeColor(string type)
        {
            if (string.IsNullOrEmpty(type)) return Color.white;

            switch (type.ToLower())
            {
                case "gift":
                    if (ColorUtility.TryParseHtmlString("#A1D06C", out Color giftColor)) return giftColor;
                    return Color.green;
                case "system":
                    if (ColorUtility.TryParseHtmlString("#FF6B6B", out Color sysColor)) return sysColor;
                    return Color.red;
                case "notice":
                case "warning":
                    if (ColorUtility.TryParseHtmlString("#FFC453", out Color warnColor)) return warnColor;
                    return Color.yellow;
                default:
                    if (ColorUtility.TryParseHtmlString("#E6E6E6", out Color defaultColor)) return defaultColor;
                    return Color.white;
            }
        }
    }
}
