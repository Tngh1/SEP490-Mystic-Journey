using UnityEngine;
using UnityEngine.UI;
using TMPro;
using API.Models;
using API.Endpoints;

namespace UI.Friend
{
    public class UIFriendEntry : MonoBehaviour
    {
        [Header("Common")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text classText;
        [SerializeField] private Image avatarImage;

        [Header("Friend Specific")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button profileButton;
        [SerializeField] private Button unfriendButton;

        [Header("Request Specific")]
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        [Header("Search/Add Specific")]
        [SerializeField] private Button addFriendButton;
        [SerializeField] private Button blockButton;

        [Header("Block Specific")]
        [SerializeField] private Button unblockButton;

        private UIFriendPanel parentPanel;
        private int currentProfileId;
        private string token;

        public void SetupAsFriend(FriendDto friend, UIFriendPanel panel)
        {
            parentPanel = panel;
            token = panel.GetToken();
            currentProfileId = friend.FriendProfileId;

            if (nameText != null) nameText.text = friend.FriendName;
            if (levelText != null) levelText.text = $"Lv.{friend.FriendLevel}";
            if (classText != null) classText.text = friend.Class;
            
            if (statusText != null)
            {
                statusText.text = friend.IsOnline ? $"<color=green>Online</color> - {friend.LastMapName}" : $"<color=gray>Offline ({friend.LastOnline})</color>";
            }

            if (unfriendButton != null)
            {
                unfriendButton.gameObject.SetActive(true);
                unfriendButton.onClick.RemoveAllListeners();
                unfriendButton.onClick.AddListener(() => OnUnfriendClicked(friend.FriendProfileId));
            }

            if (profileButton != null)
            {
                profileButton.gameObject.SetActive(true);
                profileButton.onClick.RemoveAllListeners();
                profileButton.onClick.AddListener(() => OnProfileClicked(friend.FriendProfileId));
            }

            HideRequestButtons();
            HideSearchButtons();
            HideBlockButtons();
        }

        public void SetupAsRequest(PendingFriendRequestDto req, UIFriendPanel panel)
        {
            parentPanel = panel;
            token = panel.GetToken();
            currentProfileId = req.RequesterId;

            if (nameText != null) nameText.text = req.RequesterName;
            if (levelText != null) levelText.text = $"Lv.{req.RequesterLevel}";
            if (classText != null) classText.text = req.Class;
            if (statusText != null) statusText.text = $"Sent: {req.CreatedAt}";

            if (acceptButton != null)
            {
                acceptButton.gameObject.SetActive(true);
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(() => OnAcceptClicked(req.RequesterId));
            }

            if (declineButton != null)
            {
                declineButton.gameObject.SetActive(true);
                declineButton.onClick.RemoveAllListeners();
                declineButton.onClick.AddListener(() => OnDeclineClicked(req.RequesterId));
            }

            HideFriendButtons();
            HideSearchButtons();
            HideBlockButtons();
        }

        public void SetupAsSearch(FriendSearchDto searchResult, UIFriendPanel panel)
        {
            parentPanel = panel;
            token = panel.GetToken();
            currentProfileId = searchResult.ProfileId;

            if (nameText != null) nameText.text = searchResult.CharacterName;
            if (levelText != null) levelText.text = $"Lv.{searchResult.Level}";
            if (classText != null) classText.text = searchResult.Class;
            if (statusText != null) statusText.text = searchResult.IsOnline ? "<color=green>Online</color>" : "<color=gray>Offline</color>";

            if (addFriendButton != null)
            {
                addFriendButton.gameObject.SetActive(true);
                addFriendButton.onClick.RemoveAllListeners();
                var btnText = addFriendButton.GetComponentInChildren<TMP_Text>();

                switch (searchResult.RelationshipStatus)
                {
                    case FriendRelationshipStatus.Self:
                        if (btnText != null) btnText.text = "You";
                        addFriendButton.interactable = false;
                        break;
                    case FriendRelationshipStatus.None:
                        if (btnText != null) btnText.text = "Add Friend";
                        addFriendButton.interactable = true;
                        addFriendButton.onClick.AddListener(() => 
                        {
                            if (btnText != null) btnText.text = "Loading...";
                            addFriendButton.interactable = false;
                            OnAddFriendClicked(searchResult.ProfileId);
                        });
                        break;
                    case FriendRelationshipStatus.RequestSent:
                        if (btnText != null) btnText.text = "Request Sent";
                        addFriendButton.interactable = false;
                        break;
                    case FriendRelationshipStatus.RequestReceived:
                        if (btnText != null) btnText.text = "Accept";
                        addFriendButton.interactable = true;
                        addFriendButton.onClick.AddListener(() => 
                        {
                            if (btnText != null) btnText.text = "Loading...";
                            addFriendButton.interactable = false;
                            OnAcceptClicked(searchResult.ProfileId);
                        });
                        break;
                    case FriendRelationshipStatus.Friend:
                        if (btnText != null) btnText.text = "Friend";
                        addFriendButton.interactable = false;
                        break;
                    case FriendRelationshipStatus.Blocked:
                        if (btnText != null) btnText.text = "Blocked";
                        addFriendButton.interactable = false;
                        break;
                }
            }

            if (blockButton != null)
            {
                blockButton.gameObject.SetActive(true);
                blockButton.onClick.RemoveAllListeners();
                blockButton.onClick.AddListener(() => 
                {
                    var btnText = blockButton.GetComponentInChildren<TMP_Text>();
                    if (btnText != null) btnText.text = "Loading...";
                    blockButton.interactable = false;
                    OnBlockClicked(searchResult.ProfileId);
                });
            }

            HideFriendButtons();
            HideRequestButtons();
            HideBlockButtons();
        }

        public void SetupAsBlock(FriendProfileDto blockResult, UIFriendPanel panel)
        {
            parentPanel = panel;
            token = panel.GetToken();
            currentProfileId = blockResult.ProfileId;

            if (nameText != null) nameText.text = blockResult.CharacterName;
            if (levelText != null) levelText.text = $"Lv.{blockResult.Level}";
            if (classText != null) classText.text = blockResult.Class;
            if (statusText != null) statusText.gameObject.SetActive(false);

            if (unblockButton != null)
            {
                unblockButton.gameObject.SetActive(true);
                unblockButton.onClick.RemoveAllListeners();
                unblockButton.onClick.AddListener(() => OnUnblockClicked(blockResult.ProfileId));
            }

            HideFriendButtons();
            HideRequestButtons();
            HideSearchButtons();
        }

        private void HideFriendButtons()
        {
            if (unfriendButton != null) unfriendButton.gameObject.SetActive(false);
            if (profileButton != null) profileButton.gameObject.SetActive(false);
        }

        private void HideRequestButtons()
        {
            if (acceptButton != null) acceptButton.gameObject.SetActive(false);
            if (declineButton != null) declineButton.gameObject.SetActive(false);
        }

        private void HideSearchButtons()
        {
            if (addFriendButton != null) addFriendButton.gameObject.SetActive(false);
            if (blockButton != null) blockButton.gameObject.SetActive(false);
        }

        private void HideBlockButtons()
        {
            if (unblockButton != null) unblockButton.gameObject.SetActive(false);
        }

        private void OnAcceptClicked(int requesterId)
        {
            FriendApi.AcceptFriendRequest(token, requesterId, () => parentPanel.RefreshData(), err => Debug.LogError(err));
        }

        private void OnDeclineClicked(int requesterId)
        {
            FriendApi.DeclineFriendRequest(token, requesterId, () => parentPanel.RefreshData(), err => Debug.LogError(err));
        }

        private void OnUnfriendClicked(int friendId)
        {
            FriendApi.RemoveFriend(token, friendId, () => parentPanel.RefreshData(), err => Debug.LogError(err));
        }

        private void OnProfileClicked(int friendId)
        {
            var profilePanelObj = GameObject.Find("FriendProfilePanel"); // Simple way, better with UIManager
            if (profilePanelObj != null)
            {
                var panel = profilePanelObj.GetComponent<UIFriendProfilePanel>();
                panel?.ShowProfile(friendId, token);
            }
        }

        private void OnAddFriendClicked(int profileId)
        {
            FriendApi.SendFriendRequest(token, profileId, () => Debug.Log("Request sent"), err => Debug.LogError(err));
        }

        private void OnBlockClicked(int profileId)
        {
            FriendApi.BlockPlayer(token, profileId, () => parentPanel.RefreshData(), err => Debug.LogError(err));
        }

        private void OnUnblockClicked(int profileId)
        {
            FriendApi.UnblockPlayer(token, profileId, () => parentPanel.RefreshData(), err => Debug.LogError(err));
        }
    }
}
