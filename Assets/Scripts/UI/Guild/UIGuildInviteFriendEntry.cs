using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models;
using MysticJourney.API.Endpoints;

namespace MysticJourney.UI.Guild
{
    public class UIGuildInviteFriendEntry : MonoBehaviour
    {
        public TMP_Text nameText;
        public TMP_Text levelText;
        public Button inviteButton;
        private int targetId;
        
        public void Setup(FriendDto friend)
        {
            targetId = friend.FriendProfileId;
            if (nameText != null) nameText.text = friend.FriendName;
            if (levelText != null) levelText.text = $"Lv. {friend.FriendLevel}";
            
            if (inviteButton != null)
            {
                inviteButton.interactable = true;
                inviteButton.onClick.RemoveAllListeners();
                inviteButton.onClick.AddListener(OnInviteClicked);
            }
        }
        
        private void OnInviteClicked()
        {
            if (GuildUIManager.Instance == null || GuildUIManager.Instance.currentGuild == null) 
            {
                UIPopupManager.Instance.ShowAlert("Error", "You are not in a guild!");
                return;
            }
            
            int guildId = GuildUIManager.Instance.currentGuild.guildId;
            
            GuildApi.InviteMember(guildId, targetId,
                onSuccess: (res) => {
                    UIPopupManager.Instance.ShowAlert("Success", "Invitation sent!");
                    if (inviteButton != null) inviteButton.interactable = false;
                },
                onError: (err) => {
                    UIPopupManager.Instance.ShowAlert("Failed", err.Message);
                });
        }
    }
}
