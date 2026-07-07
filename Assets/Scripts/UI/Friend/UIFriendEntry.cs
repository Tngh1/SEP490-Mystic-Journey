using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models;
using MysticJourney.API.Endpoints;

namespace UI.Friend
{
    public class UIFriendEntry : MonoBehaviour
    {
        [Header("Common Data")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text classText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Image avatarImage;
        
        [Header("Master Button (For Detail View)")]
        [SerializeField] private Button mainButton;

        [Header("Inline Action Buttons")]
        [SerializeField] private Button addFriendButton;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private Button unblockButton;

        private UIFriendPanel parentPanel;
        private int currentProfileId;

        public void SetupAsFriend(FriendDto friend, UIFriendPanel panel)
        {
            parentPanel = panel;
            currentProfileId = friend.FriendProfileId;

            if (nameText != null) nameText.text = friend.FriendName;
            if (levelText != null) levelText.text = $"Lv.{friend.FriendLevel}";
            if (classText != null) classText.text = friend.Class;
            
            if (statusText != null)
            {
                statusText.text = friend.IsOnline ? $"<color=green>Online</color> - {friend.CurrentMap}" : $"<color=gray>Offline ({friend.LastOnline})</color>";
            }

            if (mainButton != null)
            {
                mainButton.gameObject.SetActive(true);
                mainButton.onClick.RemoveAllListeners();
                mainButton.onClick.AddListener(() => parentPanel.SelectFriend(friend));
            }

            HideInlineButtons();
        }

        public void SetupAsRequest(PendingFriendRequestDto req, UIFriendPanel panel)
        {
            parentPanel = panel;
            currentProfileId = req.RequesterId;

            if (nameText != null) nameText.text = req.RequesterName;
            if (levelText != null) levelText.text = $"Lv.{req.RequesterLevel}";
            if (classText != null) classText.text = req.Class;
            if (statusText != null) statusText.text = $"Sent: {req.CreatedAt}";

            if (mainButton != null) mainButton.gameObject.SetActive(false); // No detail view for requests
            HideInlineButtons();

            if (acceptButton != null)
            {
                acceptButton.gameObject.SetActive(true);
                acceptButton.onClick.RemoveAllListeners();
                acceptButton.onClick.AddListener(() => OnAcceptClicked());
            }

            if (declineButton != null)
            {
                declineButton.gameObject.SetActive(true);
                declineButton.onClick.RemoveAllListeners();
                declineButton.onClick.AddListener(() => OnDeclineClicked());
            }
        }

        public void SetupAsSearch(FriendSearchDto searchResult, UIFriendPanel panel)
        {
            parentPanel = panel;
            currentProfileId = searchResult.ProfileId;

            if (nameText != null) nameText.text = searchResult.CharacterName;
            if (levelText != null) levelText.text = $"Lv.{searchResult.Level}";
            if (classText != null) classText.text = searchResult.Class;
            if (statusText != null) statusText.text = searchResult.IsOnline ? "<color=green>Online</color>" : "<color=gray>Offline</color>";

            if (mainButton != null) mainButton.gameObject.SetActive(false); // No detail view for search
            HideInlineButtons();

            if (addFriendButton != null)
            {
                addFriendButton.gameObject.SetActive(true);
                addFriendButton.onClick.RemoveAllListeners();
                var btnText = addFriendButton.GetComponentInChildren<TMP_Text>();
                var btnImage = addFriendButton.GetComponent<UnityEngine.UI.Image>();

                if (btnText != null)
                {
                    btnText.enableWordWrapping = false;
                    btnText.overflowMode = TextOverflowModes.Overflow;
                }

                switch (searchResult.RelationshipStatus)
                {
                    case FriendRelationshipStatus.Self:
                        if (btnText != null) btnText.text = "You";
                        if (btnImage != null) btnImage.enabled = false;
                        addFriendButton.interactable = false;
                        break;
                    case FriendRelationshipStatus.None:
                        if (btnText != null) btnText.text = "";
                        if (btnImage != null) btnImage.enabled = true;
                        addFriendButton.interactable = true;
                        addFriendButton.onClick.AddListener(() => 
                        {
                            if (btnText != null) btnText.text = "...";
                            addFriendButton.interactable = false;
                            OnAddFriendClicked();
                        });
                        break;
                    case FriendRelationshipStatus.RequestSent:
                        if (btnText != null) btnText.text = "Sent";
                        if (btnImage != null) btnImage.enabled = false;
                        addFriendButton.interactable = false;
                        break;
                    case FriendRelationshipStatus.RequestReceived:
                        if (btnText != null) btnText.text = "Accept";
                        if (btnImage != null) btnImage.enabled = false;
                        addFriendButton.interactable = true;
                        addFriendButton.onClick.AddListener(() => 
                        {
                            if (btnText != null) btnText.text = "...";
                            addFriendButton.interactable = false;
                            OnAcceptClicked();
                        });
                        break;
                    case FriendRelationshipStatus.Friend:
                        if (btnText != null) btnText.text = "Friend";
                        if (btnImage != null) btnImage.enabled = false;
                        addFriendButton.interactable = false;
                        break;
                    case FriendRelationshipStatus.Blocked:
                        if (btnText != null) btnText.text = "Blocked";
                        if (btnImage != null) btnImage.enabled = false;
                        addFriendButton.interactable = false;
                        break;
                }
            }
        }

        public void SetupAsBlock(FriendProfileDto blockResult, UIFriendPanel panel)
        {
            parentPanel = panel;
            currentProfileId = blockResult.ProfileId;

            if (nameText != null) nameText.text = blockResult.CharacterName;
            if (levelText != null) levelText.text = $"Lv.{blockResult.Level}";
            if (classText != null) classText.text = blockResult.Class;
            if (statusText != null) statusText.gameObject.SetActive(false);

            if (mainButton != null) mainButton.gameObject.SetActive(false); // No detail view for blocks
            HideInlineButtons();

            if (unblockButton != null)
            {
                unblockButton.gameObject.SetActive(true);
                unblockButton.onClick.RemoveAllListeners();
                unblockButton.onClick.AddListener(() => OnUnblockClicked());
            }
        }

        private void HideInlineButtons()
        {
            if (addFriendButton != null) addFriendButton.gameObject.SetActive(false);
            if (acceptButton != null) acceptButton.gameObject.SetActive(false);
            if (declineButton != null) declineButton.gameObject.SetActive(false);
            if (unblockButton != null) unblockButton.gameObject.SetActive(false);
        }

        private void OnAcceptClicked()
        {
            FriendApi.AcceptFriendRequest(currentProfileId, (res) => parentPanel.RefreshData(), err => Debug.LogError(err.Message));
        }

        private void OnDeclineClicked()
        {
            FriendApi.DeclineFriendRequest(currentProfileId, (res) => parentPanel.RefreshData(), err => Debug.LogError(err.Message));
        }

        private void OnAddFriendClicked()
        {
            FriendApi.SendFriendRequest(currentProfileId, (res) => parentPanel.RefreshData(), err => Debug.LogError(err.Message));
        }

        private void OnUnblockClicked()
        {
            FriendApi.UnblockPlayer(currentProfileId, (res) => parentPanel.RefreshData(), err => Debug.LogError(err.Message));
        }
    }
}
