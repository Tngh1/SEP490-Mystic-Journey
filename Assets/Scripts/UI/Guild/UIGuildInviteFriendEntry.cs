using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Models;
using MysticJourney.API.Endpoints;

namespace MysticJourney.UI.Guild
{
    // Executes mono behaviour operation.
    // Validates input parameters against null or empty values.
    public class UIGuildInviteFriendEntry : MonoBehaviour
    {
        public TMP_Text nameText;
        public TMP_Text levelText;
        public UnityEngine.UI.Image avatarImage;
        public Button cardButton;
        public GameObject selectedOverlay;

        private int targetId;
        private bool isSelected = false;
        private System.Action<int, bool> onToggleCallback;

        // Executes setup operation.
        public void Setup(FriendDto friend, System.Action<int, bool> toggleCallback)
        {
            targetId = friend.FriendProfileId;
            onToggleCallback = toggleCallback;
            isSelected = false;

            if (nameText != null) nameText.text = friend.FriendName;
            if (levelText != null) levelText.text = $"Lv. {friend.FriendLevel}";
            if (selectedOverlay != null)
            {
                selectedOverlay.SetActive(false);
                var cg = selectedOverlay.GetComponent<CanvasGroup>();
                if (cg == null) cg = selectedOverlay.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
            }

            if (avatarImage != null)
            {
                avatarImage.enabled = true;
                string avatarUrl = string.IsNullOrWhiteSpace(friend.FriendAvatarUrl) ? "avatar_1" : friend.FriendAvatarUrl;
                Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
                if (avatarSprite != null)
                {
                    avatarImage.sprite = avatarSprite;
                }
            }

            if (cardButton != null)
            {
                cardButton.interactable = true;
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(OnCardClicked);
            }
        }

        // Executes on card clicked operation.
        private void OnCardClicked()
        {
            isSelected = !isSelected;
            if (selectedOverlay != null) selectedOverlay.SetActive(isSelected);

            onToggleCallback?.Invoke(targetId, isSelected);
        }
    }
}
