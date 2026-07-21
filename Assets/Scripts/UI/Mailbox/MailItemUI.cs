using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models.Response;

namespace MysticJourney.Screen.Mail
{
    public class MailItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text expireText;
        [SerializeField] private GameObject rewardAvailableObj; // Khớp với GameObject RewardAvailable
        [SerializeField] private Toggle itemToggle;

        private MailSummaryResponse _mailData;
        private Action<MailItemUI> _onClickAction;

        public void Setup(MailSummaryResponse mailData, Action<MailItemUI> onClick)
        {
            _mailData = mailData;
            _onClickAction = onClick;

            if (titleText != null) titleText.text = mailData.Title;

            if (expireText != null)
            {
                // Ưu tiên dùng RemainingDays do BE tính sẵn
                if (mailData.RemainingDays.HasValue)
                {
                    int days = mailData.RemainingDays.Value;
                    if (days <= 0)
                        expireText.text = "Expired";
                    else if (days == 1)
                        expireText.text = "1 day left";
                    else
                        expireText.text = $"{days} days left";
                }
                else if (!string.IsNullOrEmpty(mailData.ExpiredAt) && DateTime.TryParse(mailData.ExpiredAt, out DateTime expiredDate))
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

        public MailSummaryResponse GetMailData() => _mailData;

        public void UpdateUIState()
        {
            // Hiển thị icon RewardAvailable nếu có quà và chưa nhận
            if (rewardAvailableObj != null)
                rewardAvailableObj.SetActive(_mailData.HasClaimableReward && !_mailData.IsClaimed);
        }

        public void MarkAsReadLocally()
        {
            _mailData.IsRead = true;
            UpdateUIState();
        }

        public void MarkAsClaimedLocally()
        {
            _mailData.IsClaimed = true;
            _mailData.HasClaimableReward = false;
            UpdateUIState();
        }
    }
}