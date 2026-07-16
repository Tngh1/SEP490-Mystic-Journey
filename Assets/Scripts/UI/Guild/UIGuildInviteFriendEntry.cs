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
        public UnityEngine.UI.Image avatarImage; // Thêm biến để kéo avatar vào
        public Button cardButton; // Nút bao phủ toàn bộ thẻ để bấm chọn
        public GameObject selectedOverlay; // Hiển thị mờ/đổi màu khi được chọn
        
        private int targetId;
        private bool isSelected = false;
        private System.Action<int, bool> onToggleCallback;
        
        public void Setup(FriendDto friend, System.Action<int, bool> toggleCallback)
        {
            targetId = friend.FriendProfileId;
            onToggleCallback = toggleCallback;
            isSelected = false;

            if (nameText != null) nameText.text = friend.FriendName;
            if (levelText != null) levelText.text = $"Lv. {friend.FriendLevel}";
            if (selectedOverlay != null) selectedOverlay.SetActive(false);
            
            if (cardButton != null)
            {
                cardButton.interactable = true;
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(OnCardClicked);
            }
        }
        
        private void OnCardClicked()
        {
            isSelected = !isSelected;
            if (selectedOverlay != null) selectedOverlay.SetActive(isSelected);
            
            onToggleCallback?.Invoke(targetId, isSelected);
        }
    }
}
