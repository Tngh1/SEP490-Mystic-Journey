using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models.Response;

namespace MysticJourney.Screen.Mail
{
    // Executes mono behaviour operation.
    // Validates input parameters against null or empty values.
    public class MailboxItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text expireText;
        [SerializeField] private GameObject rewardAvailableObj;
        [SerializeField] private Toggle itemToggle;

        private MailboxSummaryResponse _mailboxData;
        private Action<MailboxItemUI> _onClickAction;

        // Executes setup operation.
        // Validates input parameters against null or empty values.
        public void Setup(MailboxSummaryResponse mailboxData, Action<MailboxItemUI> onClick)
        {
            _mailboxData = mailboxData;
            _onClickAction = onClick;

            if (titleText != null) titleText.text = mailboxData.Title;

            if (expireText != null)
            {
                if (mailboxData.RemainingDays.HasValue)
                {
                    int days = mailboxData.RemainingDays.Value;
                    if (days <= 0)
                        expireText.text = "Expired";
                    else if (days == 1)
                        expireText.text = "1 day left";
                    else
                        expireText.text = $"{days} days left";
                }
                else if (!string.IsNullOrEmpty(mailboxData.ExpiredAt) && DateTime.TryParse(mailboxData.ExpiredAt, out DateTime expiredDate))
                {
                    int days = (int)Math.Ceiling((expiredDate - DateTime.UtcNow).TotalDays);
                    if (days <= 0)
                        expireText.text = "Expired";
                    else if (days == 1)
                        expireText.text = "1 day left";
                    else
                        expireText.text = $"{days} days left";
                }
                else
                {
                    expireText.text = "No expiry";
                }
            }

            UpdateUIState();

            if (itemToggle == null) itemToggle = GetComponent<Toggle>();

            if (itemToggle != null)
            {
                itemToggle.group = GetComponentInParent<ToggleGroup>();
                itemToggle.onValueChanged.RemoveAllListeners();
                itemToggle.SetIsOnWithoutNotify(false);
                itemToggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn) _onClickAction?.Invoke(this);
                });
            }
        }

        // Executes get mailbox data operation.
        public MailboxSummaryResponse GetMailboxData() => _mailboxData;

        // Executes update ui state operation.
        public void UpdateUIState()
        {
            if (rewardAvailableObj != null)
                rewardAvailableObj.SetActive(_mailboxData.HasClaimableReward && !_mailboxData.IsClaimed);
        }

        // Executes mark as read locally operation.
        public void MarkAsReadLocally()
        {
            _mailboxData.IsRead = true;
            UpdateUIState();
        }

        // Executes mark as claimed locally operation.
        public void MarkAsClaimedLocally()
        {
            _mailboxData.IsClaimed = true;
            _mailboxData.HasClaimableReward = false;
            UpdateUIState();
        }
    }
}
