using UnityEngine;
using UnityEngine.UI;
using TMPro;
using API.Endpoints;
using API.Models;

namespace UI.Friend
{
    public class UIFriendProfilePanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text classText;
        [SerializeField] private TMP_Text powerText;
        [SerializeField] private TMP_Text guildText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button closeButton;
        
        private void Start()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        public void ShowProfile(int profileId, string token)
        {
            gameObject.SetActive(true);
            
            // Show loading state if needed
            if (nameText != null) nameText.text = "Loading...";

            FriendApi.GetFriendProfile(token, profileId, profile =>
            {
                if (nameText != null) nameText.text = profile.CharacterName;
                if (levelText != null) levelText.text = $"Level: {profile.Level}";
                if (classText != null) classText.text = $"Class: {profile.Class}";
                if (powerText != null) powerText.text = $"Power: {profile.Power}";
                if (guildText != null) guildText.text = $"Guild: {profile.Guild}";
                if (titleText != null) titleText.text = $"Title: {profile.Title}";
                
                if (statusText != null)
                {
                    statusText.text = profile.IsOnline ? "<color=green>Online</color>" : $"<color=gray>Offline ({profile.LastOnline})</color>";
                }
            }, err => 
            {
                Debug.LogError($"Failed to load profile: {err}");
                if (nameText != null) nameText.text = "Error loading profile.";
            });
        }
    }
}
