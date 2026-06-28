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

        private MailResponse _currentSelectedMail;
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
                onSuccess: response => PopulateMailList(response.Mails),
                onError: error => Debug.LogError($"[MailboxUI] Lỗi tải thư: {error.Message}")
            );
        }

        private void PopulateMailList(MailResponse[] mails)
        {
            if (mails == null || mails.Length == 0) return;
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
            _currentSelectedMail = clickedUI.GetMailData();
            var mailData = _currentSelectedMail;

            if (rightPanelDetails != null) rightPanelDetails.SetActive(true);

            subjectText.text = mailData.Title;
            senderText.text = $"Từ: {mailData.Type}";
            bodyText.text = mailData.Content;

            string sentStr = DateTime.TryParse(mailData.SentAt, out DateTime sentDt) ? sentDt.ToString("dd/MM/yyyy") : (mailData.SentAt ?? "");
            dateInfoText.text = $"Ngày gửi: {sentStr}";

            bool hasGold = mailData.AttachedGold > 0;
            bool hasGems = mailData.AttachedGems > 0;
            bool hasItem = mailData.AttachedItemId != null && mailData.AttachedItemId > 0;
            bool hasAnyReward = hasGold || hasGems || hasItem;

            if (rewardsContainer != null)
            {
                rewardsContainer.SetActive(hasAnyReward);
                if (goldSlot != null) goldSlot.SetActive(hasGold);
                if (hasGold && goldAmountText != null) goldAmountText.text = $"x{mailData.AttachedGold}";

                if (gemSlot != null) gemSlot.SetActive(hasGems);
                if (hasGems && gemAmountText != null) gemAmountText.text = $"x{mailData.AttachedGems}";

                if (itemSlot != null) itemSlot.SetActive(hasItem);
                if (hasItem && itemQuantityText != null) itemQuantityText.text = $"x{mailData.AttachedItemQuantity}";
            }

            if (claimButton != null) claimButton.gameObject.SetActive(hasAnyReward && !mailData.IsClaimed);
            if (claimedStamp != null) claimedStamp.SetActive(hasAnyReward && mailData.IsClaimed);

            if (!mailData.IsRead)
            {
                MailApi.Instance.MarkAsRead(mailData.MailId, res => clickedUI.MarkAsReadLocally(), err => { });
            }
        }

        private void OnClaimClicked()
        {
            if (_currentSelectedMail == null || claimButton == null) return;
            claimButton.interactable = false;

            MailApi.Instance.ClaimReward(
                mailId: _currentSelectedMail.MailId,
                onSuccess: response =>
                {
                    _currentSelectedMail.IsClaimed = true;
                    claimButton.gameObject.SetActive(false);
                    claimButton.interactable = true;
                    if (claimedStamp != null) claimedStamp.SetActive(true);
                    if (_currentSelectedMailUI != null) _currentSelectedMailUI.UpdateIcon();
                },
                onError: error => claimButton.interactable = true
            );
        }

        private void OnDeleteClicked()
        {
            if (_currentSelectedMail == null || deleteButton == null) return;
            int currentProfileId = PlayerPrefs.GetInt(ApiConfig.PlayerProfileIdKey, 0);

            MailApi.Instance.Delete(
                _currentSelectedMail.MailId, currentProfileId,
                onSuccess: res => { rightPanelDetails.SetActive(false); LoadMailsFromBackend(); },
                onError: err => Debug.LogError("Xóa thư thất bại")
            );
        }
    }
}