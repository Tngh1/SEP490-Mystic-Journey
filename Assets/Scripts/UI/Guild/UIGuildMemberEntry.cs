using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models;

namespace MysticJourney.UI.Guild
{
    public class UIGuildMemberEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txtMemberName;
        [SerializeField] private TextMeshProUGUI txtLevel;
        [SerializeField] private TextMeshProUGUI txtMedals;
        [SerializeField] private TextMeshProUGUI txtFeats;
        [SerializeField] private TextMeshProUGUI txtStatus;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Image onlineIndicator;
        [SerializeField] private Button btnKick;

        private bool canKick;

        public void Setup(GuildMemberResponseDto member, bool canKick = false, System.Action<int> onKick = null, bool isKickMode = false)
        {
            this.canKick = canKick;

            if (txtMemberName != null) txtMemberName.text = member.playerDisplayName;
            if (txtLevel != null) txtLevel.text = $"Lv. {member.playerLevel}";

            if (txtMedals != null) txtMedals.text = member.medals.ToString();
            if (txtFeats != null) txtFeats.text = member.feats.ToString();

            if (txtStatus != null)
            {
                txtStatus.text = member.isOnline ? "Online" : "Offline";
                txtStatus.color = member.isOnline ? Color.green : Color.gray;
            }

            if (onlineIndicator != null)
            {
                onlineIndicator.color = member.isOnline ? Color.green : Color.gray;
                onlineIndicator.enabled = member != null;
            }

            if (avatarImage != null)
            {
                avatarImage.enabled = true;
                string avatarUrl = string.IsNullOrWhiteSpace(member.playerAvatarUrl) ? "avatar_1" : member.playerAvatarUrl;
                Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
                if (avatarSprite != null)
                {
                    avatarImage.sprite = avatarSprite;
                }
            }

            if (btnKick != null)
            {
                btnKick.gameObject.SetActive(this.canKick && isKickMode);
                btnKick.onClick.RemoveAllListeners();
                btnKick.onClick.AddListener(() =>
                {
                    // Confirmation popup before kicking
                    MysticJourney.UI.UIPopupManager.Instance.ShowConfirm(
                        "Kick Member",
                        $"Are you sure you want to kick {member.playerDisplayName}?",
                        () => onKick?.Invoke(member.playerProfileId),
                        null
                    );
                });
            }
        }

        public void SetKickMode(bool isKickMode)
        {
            if (btnKick != null)
            {
                btnKick.gameObject.SetActive(this.canKick && isKickMode);
            }
        }
    }
}