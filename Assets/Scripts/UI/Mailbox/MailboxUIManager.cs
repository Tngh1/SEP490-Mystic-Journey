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
        [Header("Left Panel - List")]
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject mailItemPrefab;
        [SerializeField] private TMP_Text emptyListText;

        [Header("Right Panel - Header & Body")]
        [SerializeField] private GameObject rightPanelDetails;
        [SerializeField] private TMP_Text subjectText;
        [SerializeField] private TMP_Text senderText;
        [SerializeField] private TMP_Text dateInfoText;
        [SerializeField] private TMP_Text bodyText;

        [Header("Right Panel - Rewards")]
        [SerializeField] private GameObject rewardsContainer;
        [SerializeField] private GameObject goldSlot;
        [SerializeField] private TMP_Text goldAmountText;
        [SerializeField] private GameObject gemSlot;
        [SerializeField] private TMP_Text gemAmountText;
        [SerializeField] private GameObject itemSlot;
        [SerializeField] private TMP_Text itemQuantityText;

        [Header("Right Panel - Buttons")]
        [SerializeField] private Button claimButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private GameObject claimedStamp;
        [SerializeField] private Button closePanelButton;

        private MailSummaryResponse _currentSelectedMailSummary;
        private MailItemUI _currentSelectedMailUI;

        private void Start()
        {
            if (closePanelButton != null) closePanelButton.onClick.AddListener(() => gameObject.SetActive(false));
            if (claimButton != null) claimButton.onClick.AddListener(OnClaimClicked);
            if (deleteButton != null) deleteButton.onClick.AddListener(OnDeleteClicked);
        }

        private void OnEnable()
        {
            if (rightPanelDetails != null) rightPanelDetails.SetActive(false);
            LoadMailsFromBackend();
        }

        private void LoadMailsFromBackend()
        {
            foreach (Transform child in contentContainer) Destroy(child.gameObject);

            MailApi.Instance.GetMyMails(
                onSuccess: response => PopulateMailList(response.Items),
                onError: error => Debug.LogError($"[MailboxUI] Lỗi tải thư: {error.Message}")
            );
        }

        private void PopulateMailList(MailSummaryResponse[] mails)
        {
            if (mails == null || mails.Length == 0)
            {
                if (emptyListText != null) emptyListText.gameObject.SetActive(true);
                return;
            }

            if (emptyListText != null) emptyListText.gameObject.SetActive(false);

            foreach (var mail in mails)
            {
                var obj = Instantiate(mailItemPrefab, contentContainer);
                var mailUI = obj.GetComponent<MailItemUI>();
                mailUI.Setup(mail, OnMailClicked);
            }
        }

        private void OnMailClicked(MailItemUI clickedUI)
        {
            _currentSelectedMailUI = clickedUI;
            _currentSelectedMailSummary = clickedUI.GetMailData();

            // Gọi API lấy chi tiết mail
            MailApi.Instance.GetById(
                _currentSelectedMailSummary.MailId,
                onSuccess: mailDetail => DisplayMailDetail(mailDetail, clickedUI),
                onError: error => Debug.LogError($"[MailboxUI] Lỗi lấy chi tiết mail: {error.Message}")
            );
        }

        private void DisplayMailDetail(MailDetailResponse mailData, MailItemUI clickedUI)
        {
            if (rightPanelDetails != null) rightPanelDetails.SetActive(true);

            subjectText.text = mailData.Title;
            senderText.text = $"Từ: {mailData.Type}";
            bodyText.text = mailData.Content;

            string sentStr = DateTime.TryParse(mailData.SentAt, out DateTime sentDt) ? sentDt.ToString("dd/MM/yyyy") : (mailData.SentAt ?? "");
            dateInfoText.text = $"Ngày gửi: {sentStr}";

            bool hasGold = mailData.AttachedGold > 0;
            bool hasGems = mailData.AttachedGems > 0;
            bool hasItem = mailData.AttachedItem != null && mailData.AttachedItem.ItemId > 0;
            bool hasAnyReward = hasGold || hasGems || hasItem;

            if (rewardsContainer != null)
            {
                rewardsContainer.SetActive(hasAnyReward);
                if (goldSlot != null) goldSlot.SetActive(hasGold);
                if (hasGold && goldAmountText != null) goldAmountText.text = $"x{mailData.AttachedGold}";

                if (gemSlot != null) gemSlot.SetActive(hasGems);
                if (hasGems && gemAmountText != null) gemAmountText.text = $"x{mailData.AttachedGems}";

                if (itemSlot != null) itemSlot.SetActive(hasItem);
                if (hasItem && itemQuantityText != null) itemQuantityText.text = $"x{mailData.AttachedItem.Quantity}";
            }

            if (claimButton != null) claimButton.gameObject.SetActive(hasAnyReward && !mailData.IsClaimed);
            if (claimedStamp != null) claimedStamp.SetActive(hasAnyReward && mailData.IsClaimed);

            // Đánh dấu đã đọc nếu chưa đọc
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
                    claimButton.gameObject.SetActive(false);
                    claimButton.interactable = true;
                    if (claimedStamp != null) claimedStamp.SetActive(true);
                    if (_currentSelectedMailUI != null) _currentSelectedMailUI.UpdateIcon();
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

            MailApi.Instance.Delete(
                _currentSelectedMailSummary.MailId,
                onSuccess: res =>
                {
                    rightPanelDetails.SetActive(false);
                    LoadMailsFromBackend();
                },
                onError: err => Debug.LogError("Xóa thư thất bại")
            );
        }
    }
}
