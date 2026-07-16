using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models;
using MysticJourney.API.Endpoints;
using System.Collections.Generic;

namespace MysticJourney.UI.Guild
{
    public class UIGuildInvitePanel : MonoBehaviour
    {
        public Transform contentContainer;
        public GameObject friendEntryPrefab;
        public GameObject loadingText;
        public GameObject emptyText;
        public Button btnInviteSelected;
        public Button btnCancel; // Nút tắt panel
        
        private HashSet<int> selectedFriends = new HashSet<int>();
        
        private void Awake()
        {
            if (btnInviteSelected != null)
            {
                btnInviteSelected.onClick.AddListener(OnInviteSelectedClicked);
            }
            
            if (btnCancel != null)
            {
                btnCancel.onClick.AddListener(ClosePanel);
            }
        }
        
        public void OpenPanel()
        {
            this.gameObject.SetActive(true);
            selectedFriends.Clear();
            UpdateInviteButtonState();
            LoadFriends();
        }

        public void ClosePanel()
        {
            this.gameObject.SetActive(false);
        }

        private void LoadFriends()
        {
            if (loadingText != null) loadingText.SetActive(true);
            if (emptyText != null) emptyText.SetActive(false);
            
            // Clear old entries
            foreach (Transform t in contentContainer) 
            {
                Destroy(t.gameObject);
            }

            FriendApi.GetFriendList(
                onSuccess: (list) => {
                    if (loadingText != null) loadingText.SetActive(false);
                    if (list == null || list.Count == 0)
                    {
                        if (emptyText != null) emptyText.SetActive(true);
                        return;
                    }
                    
                    foreach (var friend in list)
                    {
                        var obj = Instantiate(friendEntryPrefab, contentContainer);
                        obj.SetActive(true);
                        var entry = obj.GetComponent<UIGuildInviteFriendEntry>();
                        if (entry != null) entry.Setup(friend, OnToggleFriendSelection);
                    }
                },
                onError: (err) => {
                    if (loadingText != null) loadingText.SetActive(false);
                    UIPopupManager.Instance.ShowAlert("Error", "Could not load friend list: " + err.Message);
                }
            );
        }
        
        private void OnToggleFriendSelection(int friendId, bool isSelected)
        {
            if (isSelected) selectedFriends.Add(friendId);
            else selectedFriends.Remove(friendId);
            
            UpdateInviteButtonState();
        }
        
        private void UpdateInviteButtonState()
        {
            if (btnInviteSelected != null)
            {
                btnInviteSelected.interactable = selectedFriends.Count > 0;
            }
        }
        
        private void OnInviteSelectedClicked()
        {
            if (selectedFriends.Count == 0) return;
            
            if (GuildUIManager.Instance == null || GuildUIManager.Instance.currentGuild == null) 
            {
                UIPopupManager.Instance.ShowAlert("Error", "You are not in a guild!");
                return;
            }
            
            int guildId = GuildUIManager.Instance.currentGuild.guildId;
            int total = selectedFriends.Count;
            int successCount = 0;
            int processed = 0;
            List<string> errors = new List<string>();
            
            btnInviteSelected.interactable = false;
            
            foreach (int targetId in selectedFriends)
            {
                GuildApi.InviteMember(guildId, targetId,
                    onSuccess: (res) => {
                        successCount++;
                        processed++;
                        CheckInviteCompletion(total, successCount, processed, errors);
                    },
                    onError: (err) => {
                        errors.Add(err.Message);
                        processed++;
                        CheckInviteCompletion(total, successCount, processed, errors);
                    });
            }
        }
        
        private void CheckInviteCompletion(int total, int successCount, int processed, List<string> errors)
        {
            if (processed >= total)
            {
                if (successCount == total)
                {
                    UIPopupManager.Instance.ShowAlert("Done", $"Sent {successCount}/{total} invitations successfully!");
                }
                else if (successCount > 0)
                {
                    UIPopupManager.Instance.ShowAlert("Done", $"Sent {successCount}/{total} invitations successfully!\nFailed: {errors.Count}");
                }
                else
                {
                    string errorMsg = errors.Count > 0 ? errors[0] : "Failed to send invitations.";
                    UIPopupManager.Instance.ShowAlert("Failed", $"Could not invite:\n{errorMsg}");
                }
                ClosePanel();
            }
        }
    }
}
