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
        // Single "current / total" label. Keeps the reference that used to be wired to
        // the middle page number so re-assignment in the Inspector isn't required.
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

        private void Start()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
                // Same hover-scale the HUD/party buttons use.
                if (closeButton.GetComponent<UIHoverScaleEffect>() == null)
                    closeButton.gameObject.AddComponent<UIHoverScaleEffect>();
            }
            if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
            if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
            if (previousButton != null) previousButton.onClick.AddListener(() => GoToPage(_currentPage - 1));
            if (nextButton != null) nextButton.onClick.AddListener(() => GoToPage(_currentPage + 1));
        }

        private void OnEnable()
        {
            if (rightPanel != null) rightPanel.SetActive(true);
            HideRightPanelContent();
            _currentPage = 1;
            LoadMailboxesFromBackend();
        }

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

        private void ShowRightPanelContent()
        {
            if (titleText != null) titleText.gameObject.SetActive(true);
            if (typeText != null) typeText.gameObject.SetActive(true);
            if (bodyContainer != null) bodyContainer.SetActive(true);
            if (deleteButton != null) deleteButton.gameObject.SetActive(true);
        }

        // Scrollbar của Rewards là SIBLING của rewardsContainer (không phải con), nên
        // rewardsContainer.SetActive(false) không ẩn được nó. Thêm nữa, ScrollRect khi bị
        // disable sẽ ngừng chạy layout pass, nên cơ chế AutoHideAndExpandViewport của Unity
        // cũng không kịp tự ẩn -> scrollbar treo lại ở trạng thái bật trong scene.
        // Vì vậy phải tắt scrollbar tường minh cùng lúc với container.
        private void SetRewardsVisible(bool visible)
        {
            if (rewardsContainer != null) rewardsContainer.SetActive(visible);

            var scrollbar = GetRewardsScrollbar();
            if (scrollbar != null) scrollbar.gameObject.SetActive(visible);
        }

        // Lấy scrollbar từ chính ScrollRect của rewardsContainer để không phải wire thêm
        // reference trong Inspector; hỗ trợ cả trục ngang lẫn dọc.
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

        private void LoadMailboxesFromBackend()
        {
            _isLoading = true;
            SetPaginationInteractable(false);

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

        private void SetPaginationInteractable(bool on)
        {
            // Khi bật lại, để UpdatePaginationUI quyết định enable theo trang hiện tại.
            if (!on)
            {
                if (previousButton != null) previousButton.interactable = false;
                if (nextButton != null) nextButton.interactable = false;
            }
        }

        private void PopulateMailboxList(MailboxListPagedResponse response)
        {
            if (response == null || response.Items == null || response.Items.Length == 0)
            {
                // Xóa thư cuối cùng của trang > 1 làm trang này rỗng -> lùi về trang trước
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

        private void UpdatePaginationUI(int currentPage, int totalPages)
        {
            _isLoading = false;
            _currentPage = currentPage;
            _totalPages = totalPages;

            // Show "current / total" (e.g. "2 / 5"). Clamp the displayed total to at
            // least 1 so an empty mailbox reads "1 / 1" rather than "1 / 0".
            if (pageInfoText != null)
                pageInfoText.text = $"{currentPage} / {Mathf.Max(1, totalPages)}";

            if (previousButton != null) previousButton.interactable = currentPage > 1;
            if (nextButton != null) nextButton.interactable = currentPage < totalPages;
        }

        private void GoToPage(int page)
        {
            if (_isLoading) return;
            if (page < 1 || page > _totalPages || page == _currentPage) return;
            _currentPage = page;

            if (rightPanel != null) rightPanel.SetActive(false);

            LoadMailboxesFromBackend();
        }

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

            // Display rewards section
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

        private void DisplayRewards(MailboxDetailResponse mailboxData)
        {
            bool hasGold = mailboxData.AttachedGold > 0;
            bool hasGems = mailboxData.AttachedGems > 0;
            bool hasItems = mailboxData.AttachedItems != null && mailboxData.AttachedItems.Length > 0;
            bool hasRewards = hasGold || hasGems || hasItems;

            SetRewardsVisible(hasRewards);

            // Build combined list: gold + gems + items
            var allRewards = new List<UIItemDisplayData>();

            // Gold
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

            // Gems
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

            // Items
            if (hasItems)
            {
                foreach (var item in mailboxData.AttachedItems)
                {
                    allRewards.Add(CreateItemDisplayData(item));
                }
            }

            // Setup all reward slots
            SetupRewardSlots(allRewards);

            // Claim button / claimed stamp
            if (mailboxData.IsClaimed)
            {
                if (claimButton != null) claimButton.gameObject.SetActive(false);
                if (claimedStamp != null) claimedStamp.SetActive(true);
            }
            else if (IsExpired(mailboxData.ExpiredAt))
            {
                // Hết hạn mà chưa nhận -> BE sẽ từ chối claim, nên ẩn cả nút lẫn stamp.
                if (claimButton != null) claimButton.gameObject.SetActive(false);
                if (claimedStamp != null) claimedStamp.SetActive(false);
            }
            else
            {
                if (claimButton != null) claimButton.gameObject.SetActive(hasRewards);
                if (claimedStamp != null) claimedStamp.SetActive(false);
            }
        }

        private static bool IsExpired(string expiredAt)
        {
            return !string.IsNullOrEmpty(expiredAt)
                && DateTime.TryParse(expiredAt, out DateTime expiry)
                && expiry <= DateTime.UtcNow;
        }

        private void SetupRewardSlots(List<UIItemDisplayData> rewards)
        {
            if (itemsContainer == null)
                return;

            // Clear existing items
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

                var slot = slotObj.GetComponent<UIBaseItemSlot>();
                if (slot != null)
                {
                    slot.SetupCore(displayData);
                }
            }
        }

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

            // Get icon from ItemIconDatabase
            displayData.icon = GetIconFromDatabase(item.ItemName, null);

            // Try remote cache if no local icon
            if (displayData.icon == null && !string.IsNullOrWhiteSpace(item.IconUrl))
            {
                var cached = RemoteSpriteCache.GetCached(item.IconUrl);
                if (cached != null)
                    displayData.icon = cached;
            }

            return displayData;
        }

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

                    // Cập nhật HUD ngay để Gold/Gem/Level phản ánh phần thưởng vừa nhận,
                    // thay vì chờ vòng lặp refresh 3s của PlayerHUDUIManager.
                    if (PlayerHUDUIManager.Instance != null)
                        PlayerHUDUIManager.Instance.RefreshHUD();

                    // Nếu bạn có popup hiển thị tổng kết quà vừa nhận, bạn có thể gọi API/Popup Manager ở đây
                },
                onError: error =>
                {
                    claimButton.interactable = true;
                    Debug.LogError($"[MailboxUI] Claim reward failed: {error.Message}");
                }
            );
        }

        private void OnDeleteClicked()
        {
            if (_currentSelectedMailboxSummary == null || deleteButton == null) return;

            // BR-147: thư còn quà chưa nhận thì không được xóa -> chỉ thông báo,
            // không cho xác nhận xóa nữa (server cũng chặn, trước đây bấm OK là
            // gọi API rồi thất bại im lặng).
            // Thư hết hạn thì quà không claim được nữa -> xóa thẳng, khỏi cảnh báo.
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
                    // Server là nơi chốt BR-147, nên nếu nó từ chối thì phải cho
                    // người chơi thấy lý do thay vì im lặng như trước.
                    Debug.LogError($"[MailboxUI] Xóa thư thất bại: {err.Message}");
                    ShowConfirmPopup(
                        string.IsNullOrEmpty(err.Message) ? "Failed to delete this mail." : err.Message,
                        allowDelete: false);
                }
            );
        }

        private Color GetMailboxTypeColor(string type)
        {
            if (string.IsNullOrEmpty(type)) return Color.white;

            // Theo FE design, chỉnh mã màu phù hợp với từng loại thư
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
