using System;
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

            if (previousButton != null) previousButton.onClick.AddListener(() => GoToPage(_currentPage - 1));
            if (nextButton != null) nextButton.onClick.AddListener(() => GoToPage(_currentPage + 1));
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

                if (paginationContainer != null) paginationContainer.SetActive(false);
                HideRightPanelContent();
                if (emptyRightText != null) emptyRightText.gameObject.SetActive(true);

                UpdatePaginationUI(1, 0);
                return;
            }

            if (emptyListText != null) emptyListText.gameObject.SetActive(false);
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

            UpdatePaginationUI(response.Page, response.TotalPages);
        }

        private void UpdatePaginationUI(int currentPage, int totalPages)
        {
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

            // ponytail: Rewards section intentionally left blank — to be built later.
            // Keep the whole reward area (gold/gem/items) and the claim button/stamp
            // hidden so the right panel shows only title/type/body for now. Re-enable by
            // restoring the reward-rendering block below (git history) once the reward UI
            // is finalized.
            if (rewardsContainer != null) rewardsContainer.SetActive(false);
            if (claimButton != null) claimButton.gameObject.SetActive(false);
            if (claimedStamp != null) claimedStamp.SetActive(false);

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