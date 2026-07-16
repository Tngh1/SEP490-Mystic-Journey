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
        
        public void OpenPanel()
        {
            this.gameObject.SetActive(true);
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
                        if (entry != null) entry.Setup(friend);
                    }
                },
                onError: (err) => {
                    if (loadingText != null) loadingText.SetActive(false);
                    UIPopupManager.Instance.ShowAlert("Error", "Could not load friend list: " + err.Message);
                }
            );
        }
    }
}
