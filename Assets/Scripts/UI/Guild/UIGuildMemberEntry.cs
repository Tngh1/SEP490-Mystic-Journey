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

        public void Setup(GuildMemberResponseDto member)
        {
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
                avatarImage.sprite = null;
                if (!string.IsNullOrWhiteSpace(member.playerAvatarUrl))
                {
                    var cached = RemoteSpriteCache.GetCached(member.playerAvatarUrl);
                    if (cached != null)
                    {
                        avatarImage.sprite = cached;
                    }
                    else
                    {
                        RemoteSpriteCache.Load(this, member.playerAvatarUrl, (sprite) =>
                        {
                            if (sprite != null && avatarImage != null)
                                avatarImage.sprite = sprite;
                        });
                    }
                }
            }
        }
    }
}