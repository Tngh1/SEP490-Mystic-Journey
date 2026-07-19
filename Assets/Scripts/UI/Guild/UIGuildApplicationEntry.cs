using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models;
using System;

namespace MysticJourney.UI.Guild
{
    public class UIGuildApplicationEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txtPlayerName;
        [SerializeField] private TextMeshProUGUI txtLevel;
        [SerializeField] private TextMeshProUGUI txtAppliedTime;
        [SerializeField] private Button btnApprove;
        [SerializeField] private Button btnReject;
        [SerializeField] private GameObject pendingBadge;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI txtMedals;
        [SerializeField] private TextMeshProUGUI txtFeats;

        private int applicationId;
        private int guildId;
        private Action onApproveCallback;
        private Action onRejectCallback;

        public void Setup(GuildApplicationDTO application, int guildId, Action onApprove, Action onReject)
        {
            this.applicationId = application.guildApplicationId;
            this.guildId = guildId;
            this.onApproveCallback = onApprove;
            this.onRejectCallback = onReject;

            if (txtPlayerName != null) txtPlayerName.text = application.playerName;
            if (txtLevel != null) txtLevel.text = $"Lv. {application.playerLevel}";

            if (txtMedals != null)
            {
                txtMedals.text = application.medals > 0 ? application.medals.ToString() : "-";
            }
            if (txtFeats != null)
            {
                txtFeats.text = application.feats > 0 ? application.feats.ToString() : "-";
            }

            if (avatarImage != null)
            {
                avatarImage.enabled = true;
                string avatarUrl = string.IsNullOrWhiteSpace(application.playerAvatarUrl) ? "avatar_1" : application.playerAvatarUrl;
                Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
                if (avatarSprite != null)
                {
                    avatarImage.sprite = avatarSprite;
                }
            }

            if (txtAppliedTime != null && !string.IsNullOrEmpty(application.createdAt))
            {
                if (DateTime.TryParse(application.createdAt, out DateTime appliedTime))
                {
                    var elapsed = DateTime.UtcNow - appliedTime;
                    if (elapsed.TotalMinutes < 1)
                        txtAppliedTime.text = "Just now";
                    else if (elapsed.TotalHours < 1)
                        txtAppliedTime.text = $"{(int)elapsed.TotalMinutes}m ago";
                    else if (elapsed.TotalDays < 1)
                        txtAppliedTime.text = $"{(int)elapsed.TotalHours}h ago";
                    else
                        txtAppliedTime.text = $"{(int)elapsed.TotalDays}d ago";
                }
                else
                {
                    txtAppliedTime.text = "";
                }
            }

            if (pendingBadge != null)
                pendingBadge.SetActive(application.status == "Pending");

            if (btnApprove != null)
            {
                btnApprove.onClick.RemoveAllListeners();
                btnApprove.onClick.AddListener(() => OnApproveClicked());
            }

            if (btnReject != null)
            {
                btnReject.onClick.RemoveAllListeners();
                btnReject.onClick.AddListener(() => OnRejectClicked());
            }
        }

        private void OnApproveClicked()
        {
            if (onApproveCallback != null)
                onApproveCallback();
        }

        private void OnRejectClicked()
        {
            if (onRejectCallback != null)
                onRejectCallback();
        }
    }
}