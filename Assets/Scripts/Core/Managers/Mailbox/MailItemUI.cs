using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using MysticJourney.API.Models.Response;

namespace MysticJourney.Screen.Mail
{
    public class MailItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image mailIcon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text dateText;
        [SerializeField] private Button clickButton;

        [Header("Sprites")]
        [SerializeField] private Sprite rewardIcon;
        [SerializeField] private Sprite normalIcon;
        [SerializeField] private Sprite readIcon;

        private MailResponse _mailData;
        private Action<MailItemUI> _onClickCallback;

        public void Setup(MailResponse mail, Action<MailItemUI> onClick)
        {
            _mailData = mail;
            _onClickCallback = onClick;

            if (titleText != null) titleText.text = mail.Title;

            if (dateText != null)
            {
                if (DateTime.TryParse(mail.SentAt, out DateTime parsedDate))
                    dateText.text = parsedDate.ToString("dd/MM/yyyy");
                else
                    dateText.text = mail.SentAt ?? "";
            }

            UpdateIcon();

            if (clickButton != null)
            {
                clickButton.onClick.RemoveAllListeners();
                clickButton.onClick.AddListener(() => _onClickCallback?.Invoke(this));
            }
        }

        public void UpdateIcon()
        {
            if (mailIcon == null) return;
            bool hasReward = _mailData.AttachedGold > 0 || _mailData.AttachedGems > 0 || (_mailData.AttachedItemId != null && _mailData.AttachedItemId > 0);

            if (_mailData.IsRead)
            {
                mailIcon.sprite = (hasReward && !_mailData.IsClaimed) ? rewardIcon : readIcon;
            }
            else
            {
                mailIcon.sprite = hasReward ? rewardIcon : normalIcon;
            }
        }

        public void MarkAsReadLocally()
        {
            _mailData.IsRead = true;
            UpdateIcon();
        }

        public MailResponse GetMailData() => _mailData;
    }
}