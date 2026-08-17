using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models;
using MysticJourney.API.Endpoints;
using System.Collections.Generic;

namespace MysticJourney.UI.Guild
{
    // Executes mono behaviour operation.
    public class UIGuildInvitePanel : MonoBehaviour
    {
        public Transform contentContainer;
        public GameObject friendEntryPrefab;
        public GameObject loadingText;
        public GameObject emptyText;
        public Button btnInviteSelected;
        public Button btnCancel;

        private HashSet<int> selectedFriends = new HashSet<int>();

        // Initializes internal component caches and dependencies for UIGuildInvitePanel upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
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

        // Update visibility for panel; it updates active, updates invite button state, and loads friends.
        public void OpenPanel()
        {
            this.gameObject.SetActive(true);
            selectedFriends.Clear();
            UpdateInviteButtonState();
            LoadFriends();
        }

        // Update visibility for panel; it updates active.
        public void ClosePanel()
        {
            this.gameObject.SetActive(false);
        }

        // Executes load friends operation.
        private void LoadFriends()
        {
            if (loadingText != null) loadingText.SetActive(true);
            if (emptyText != null) emptyText.SetActive(false);

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
                    UIPopup.Instance.ShowAlert("Error", "Could not load friend list: " + err.Message);
                }
            );
        }

        // Executes on toggle friend selection operation.
        private void OnToggleFriendSelection(int friendId, bool isSelected)
        {
            if (isSelected) selectedFriends.Add(friendId);
            else selectedFriends.Remove(friendId);

            UpdateInviteButtonState();
        }

        // Executes update invite button state operation.
        private void UpdateInviteButtonState()
        {
            if (btnInviteSelected != null)
            {
                btnInviteSelected.interactable = selectedFriends.Count > 0;
            }
        }

        // Executes on invite selected clicked operation.
        private void OnInviteSelectedClicked()
        {
            if (selectedFriends.Count == 0) return;

            if (GuildUIManager.Instance == null || GuildUIManager.Instance.currentGuild == null)
            {
                UIPopup.Instance.ShowAlert("Error", "You are not in a guild!");
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

        // Executes check invite completion operation.
        private void CheckInviteCompletion(int total, int successCount, int processed, List<string> errors)
        {
            if (processed >= total)
            {
                if (successCount == total)
                {
                    UIPopup.Instance.ShowAlert("Done", $"Sent {successCount}/{total} invitations successfully!");
                }
                else if (successCount > 0)
                {
                    UIPopup.Instance.ShowAlert("Done", $"Sent {successCount}/{total} invitations successfully!\nFailed: {errors.Count}");
                }
                else
                {
                    string errorMsg = errors.Count > 0 ? errors[0] : "Failed to send invitations.";
                    UIPopup.Instance.ShowAlert("Failed", $"Could not invite:\n{errorMsg}");
                }
                ClosePanel();
            }
        }
    }
}
