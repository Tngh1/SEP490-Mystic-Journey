using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.API.Core;

namespace MysticJourney.Screen.Mail
{
    public class MailboxUIManager : MonoBehaviour
    {
        [Header("Left Panel - General")]
        [SerializeField] private GameObject totalMailContainer;
        [SerializeField] private TMP_Text totalMailText;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject mailItemPrefab;
        [SerializeField] private TMP_Text emptyListText;

        [Header("Left Panel - Pagination")]
        [SerializeField] private GameObject paginationContainer;
        [SerializeField] private Button firstButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private TMP_Text pageNumber1;
        [SerializeField] private TMP_Text pageNumber2;
        [SerializeField] private TMP_Text pageNumber3;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button lastButton;

        [Header("Right Panel - General")]
        [SerializeField] private GameObject rightPanel;
        [SerializeField] private TMP_Text emptyRightText;

        [Header("Right Panel - Header & Body")]
        [SerializeField] private GameObject bodyContainer;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text typeText;
        [SerializeField] private TMP_Text bodyText;

        [Header("Right Panel - Footer (Rewards)")]
        [SerializeField] private GameObject rewardsContainer;
        [SerializeField] private GameObject goldSlot;
        [SerializeField] private TMP_Text goldAmountText;
        [SerializeField] private GameObject gemSlot;
        [SerializeField] private TMP_Text gemAmountText;

        [Header("Right Panel - Reward Items")]
        [SerializeField] private Transform itemsContainer;
        [SerializeField] private GameObject itemSlotPrefab;

        [Header("Right Panel - Buttons")]
        [SerializeField] private Button claimButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private GameObject claimedStamp;

        [Header("Delete Confirm Popup")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TextMeshProUGUI popupMainText;
        [SerializeField] private Button popupOkButton;
        [SerializeField] private Button popupCancelButton;

        [Header("Main Setup")]
        [SerializeField] private Button closeButton;

        private MailSummaryResponse _currentSelectedMailSummary;
        private MailItemUI _currentSelectedMailUI;

        private int _currentPage = 1;
        private int _totalPages = 1;
        private readonly int _itemsPerPage = 5;

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
            if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
            if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (popupOkButton != null) popupOkButton.onClick.AddListener(OnPopupOkClicked);
            if (popupCancelButton != null) popupCancelButton.onClick.AddListener(OnPopupCancelClicked);

            if (firstButton != null) firstButton.onClick.AddListener(() => GoToPage(1));
            if (previousButton != null) previousButton.onClick.AddListener(() => GoToPage(_currentPage - 1));
            if (nextButton != null) nextButton.onClick.AddListener(() => GoToPage(_currentPage + 1));
            if (lastButton != null) lastButton.onClick.AddListener(() => GoToPage(_totalPages));
        }

        private void OnEnable()
        {
            if (rightPanel != null) rightPanel.SetActive(true);
            HideRightPanelContent();
            _currentPage = 1;
            LoadMailsFromBackend();
        }

        private void HideRightPanelContent()
        {
            if (titleText != null) titleText.gameObject.SetActive(false);
            if (typeText != null) typeText.gameObject.SetActive(false);
            if (bodyContainer != null) bodyContainer.SetActive(false);
            if (rewardsContainer != null) rewardsContainer.SetActive(false);
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

        private void LoadMailsFromBackend()
        {
            if (totalMailContainer != null) totalMailContainer.SetActive(false);
            if (paginationContainer != null) paginationContainer.SetActive(false);
            if (emptyListText != null) emptyListText.gameObject.SetActive(false);
            if (contentContainer != null)
            {
                foreach (Transform child in contentContainer)
                {
                    child.gameObject.SetActive(false);
                }
            }

            MailApi.Instance.GetMyMails(
                _currentPage,
                _itemsPerPage,
                response => PopulateMailList(response),
                onError: error =>
                {
                    Debug.LogError($"[MailboxUI] Lỗi tải thư: {error.Message}");
                }
            );
        }

        private void PopulateMailList(MailListPagedResponse response)
        {
            if (response == null || response.Items == null || response.Items.Length == 0)
            {
                if (emptyListText != null)
                {
                    emptyListText.gameObject.SetActive(true);
                    emptyListText.text = "No mail has arrived yet.";
                }

                if (contentContainer != null)
                {
                    foreach (Transform child in contentContainer)
                    {
                        child.gameObject.SetActive(false);
                    }
                }

                if (totalMailContainer != null) totalMailContainer.SetActive(false);
                if (paginationContainer != null) paginationContainer.SetActive(false);
                HideRightPanelContent();
                if (emptyRightText != null) emptyRightText.gameObject.SetActive(true);

                UpdatePaginationUI(0, 1, 0);
                return;
            }

            if (emptyListText != null) emptyListText.gameObject.SetActive(false);
            if (totalMailContainer != null) totalMailContainer.SetActive(true);
            if (totalMailText != null) totalMailText.text = $"Total: {response.TotalMails}";
            if (emptyRightText != null) emptyRightText.gameObject.SetActive(false);
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

                    var mailUI = itemGO.GetComponent<MailItemUI>();
                    if (mailUI != null)
                        mailUI.Setup(response.Items[i], OnMailClicked);
                }
                else
                {
                    contentContainer.GetChild(i).gameObject.SetActive(false);
                }
            }

            UpdatePaginationUI(response.TotalMails, response.Page, response.TotalPages);
        }

        private void UpdatePaginationUI(int totalMails, int currentPage, int totalPages)
        {
            _currentPage = currentPage;
            _totalPages = totalPages;

            if (pageNumber1 != null)
                pageNumber1.text = (currentPage > 1) ? (currentPage - 1).ToString() : "";

            if (pageNumber2 != null)
                pageNumber2.text = (totalPages > 0) ? currentPage.ToString() : "1";

            if (pageNumber3 != null)
                pageNumber3.text = (currentPage < totalPages) ? (currentPage + 1).ToString() : "";

            if (firstButton != null) firstButton.interactable = currentPage > 1;
            if (previousButton != null) previousButton.interactable = currentPage > 1;
            if (nextButton != null) nextButton.interactable = currentPage < totalPages;
            if (lastButton != null) lastButton.interactable = currentPage < totalPages;
        }

        private void GoToPage(int page)
        {
            if (page < 1 || page > _totalPages || page == _currentPage) return;
            _currentPage = page;

            if (rightPanel != null) rightPanel.SetActive(false);

            LoadMailsFromBackend();
        }

        private void OnMailClicked(MailItemUI clickedUI)
        {
            _currentSelectedMailUI = clickedUI;
            _currentSelectedMailSummary = clickedUI.GetMailData();

            MailApi.Instance.GetById(
                _currentSelectedMailSummary.MailId,
                onSuccess: mailDetail => DisplayMailDetail(mailDetail, clickedUI),
                onError: error => Debug.LogError($"[MailboxUI] Lỗi lấy chi tiết mail: {error.Message}")
            );
        }

        private void DisplayMailDetail(MailDetailResponse mailData, MailItemUI clickedUI)
        {
            if (rightPanel != null) rightPanel.SetActive(true);

            if (emptyRightText != null) emptyRightText.gameObject.SetActive(false);
            ShowRightPanelContent();

            if (titleText != null) titleText.text = mailData.Title;
            if (typeText != null) typeText.text = mailData.Type;
            if (bodyText != null) bodyText.text = mailData.Content;

            bool hasGold = mailData.AttachedGold > 0;
            bool hasGems = mailData.AttachedGems > 0;
            bool hasItems = mailData.AttachedItems != null && mailData.AttachedItems.Length > 0;
            bool hasAnyReward = hasGold || hasGems || hasItems;

            Debug.Log($"[MailboxUI] Gold: {mailData.AttachedGold}, Gems: {mailData.AttachedGems}, Items count: {(mailData.AttachedItems != null ? mailData.AttachedItems.Length : 0)}");

            if (rewardsContainer != null)
            {
                rewardsContainer.SetActive(hasAnyReward);

                // Gold
                if (goldSlot != null) goldSlot.SetActive(hasGold);
                if (hasGold && goldAmountText != null) goldAmountText.text = $"x{(int)mailData.AttachedGold}";

                // Gems
                if (gemSlot != null) gemSlot.SetActive(hasGems);
                if (hasGems && gemAmountText != null) gemAmountText.text = $"x{(int)mailData.AttachedGems}";

                // Items động
                if (itemsContainer != null && itemSlotPrefab != null)
                {
                    // Dọn dẹp item cũ
                    foreach (Transform child in itemsContainer)
                    {
                        Destroy(child.gameObject);
                    }

                    if (hasItems)
                    {
                        foreach (var item in mailData.AttachedItems)
                        {
                            GameObject itemObj = Instantiate(itemSlotPrefab, itemsContainer);

                            // SỬA LỖI 1: Reset lại scale để tránh UI bị biến dạng hoặc tàng hình (scale = 0)
                            itemObj.transform.localScale = Vector3.one;
                            itemObj.SetActive(true);

                            // Thay vì dùng GetComponentInChildren hoặc transform.Find...
                            // Hãy lấy TRỰC TIẾP component Image nằm ngay trên object ItemSlot
                            var iconImage = itemObj.GetComponent<UnityEngine.UI.Image>();
                            var quantityText = itemObj.GetComponentInChildren<TMP_Text>();

                            if (iconImage != null && ItemIconDatabase.Instance != null)
                            {
                                // Lấy icon từ database
                                Sprite icon = ItemIconDatabase.Instance.GetIcon(item.ItemName, null);

                                if (icon != null)
                                {
                                    iconImage.sprite = icon; // Gán hình vào
                                }
                                else
                                {
                                    // Nếu vẫn nhảy vào đây, nghĩa là key trong Database VẪN CHƯA KHỚP
                                    Debug.LogWarning($"[MailboxUI] Vẫn không tìm thấy icon cho: '{item.ItemName}'");
                                }
                            }

                            if (quantityText != null)
                            {
                                quantityText.text = $"x{item.Quantity}";
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("[MailboxUI] Thư này không có AttachedItems.");
                    }
                }
            }

            if (claimButton != null) claimButton.gameObject.SetActive(hasAnyReward && !mailData.IsClaimed);
            if (claimedStamp != null) claimedStamp.SetActive(hasAnyReward && mailData.IsClaimed);

            if (!mailData.IsRead)
            {
                MailApi.Instance.MarkAsRead(
                    mailData.MailId,
                    res => clickedUI.MarkAsReadLocally(),
                    err => { }
                );
            }
        }

        private void OnClaimClicked()
        {
            if (_currentSelectedMailSummary == null || claimButton == null) return;
            claimButton.interactable = false;

            MailApi.Instance.ClaimReward(
                mailId: _currentSelectedMailSummary.MailId,
                onSuccess: response =>
                {
                    if (rewardsContainer != null) rewardsContainer.SetActive(false);
                    if (claimButton != null) claimButton.gameObject.SetActive(false);
                    claimButton.interactable = true;
                    if (claimedStamp != null) claimedStamp.SetActive(true);
                    _currentSelectedMailUI?.MarkAsClaimedLocally();

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
            if (_currentSelectedMailSummary == null || deleteButton == null) return;

            // Nếu thư còn quà chưa nhận -> bắt buộc mở popup xác nhận
            if (_currentSelectedMailSummary.HasClaimableReward && !_currentSelectedMailSummary.IsClaimed)
            {
                ShowConfirmPopup("This mail still has unclaimed rewards. Are you sure you want to delete it?");
            }
            else
            {
                PerformDeleteMail();
            }
        }

        // --- POPUP ---
        private void ShowConfirmPopup(string message)
        {
            if (confirmPanel == null)
            {
                Debug.LogWarning($"[MailboxUI] Không tìm thấy Confirm Panel để hiển thị: {message}");
                return;
            }

            if (popupMainText != null) popupMainText.text = message;
            confirmPanel.SetActive(true);
        }

        private void OnPopupOkClicked()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            PerformDeleteMail();
        }

        private void OnPopupCancelClicked()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
        }

        private void PerformDeleteMail()
        {
            if (_currentSelectedMailSummary == null) return;

            int mailId = _currentSelectedMailSummary.MailId;

            MailApi.Instance.Delete(
                mailId,
                onSuccess: res =>
                {
                    if (_currentSelectedMailSummary != null && _currentSelectedMailSummary.MailId == mailId)
                    {
                        _currentSelectedMailSummary = null;
                        _currentSelectedMailUI = null;
                    }
                    HideRightPanelContent();
                    if (emptyRightText != null) emptyRightText.gameObject.SetActive(true);
                    LoadMailsFromBackend();
                },
                onError: err => Debug.LogError("[MailboxUI] Xóa thư thất bại")
            );
        }
    }
}