using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;

namespace UI.Friend
{
    public class UIAvatarSelectionPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Transform avatarListContainer;
        [SerializeField] private GameObject avatarButtonPrefab; // Prefab có chứa Image và Button

        private string _selectedAvatarId;
        private int _myProfileId;
        private UIFriendProfilePanel _profilePanel;

        // Cấu hình danh sách avatar mặc định
        private readonly List<string> _availableAvatars = new List<string>
        {
            "avatar_1", "avatar_2", "avatar_3", "avatar_4", "avatar_5",
            "avatar_6", "avatar_7", "avatar_8", "avatar_9", "avatar_10"
        };

        private void Awake()
        {
            // closeButton bỏ trống trong Inspector nên nút X của panel này không làm gì cả
            // (onClick trong scene cũng rỗng). Tự tìm theo tên để không phụ thuộc việc gán tay.
            if (closeButton == null)
                closeButton = transform.Find("CloseButton")?.GetComponent<Button>();

            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);

            if (saveButton != null)
                saveButton.onClick.AddListener(SaveAvatar);

            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                if (btn != null && btn.GetComponent<UIHoverScaleEffect>() == null)
                    btn.gameObject.AddComponent<UIHoverScaleEffect>();
            }
        }

        public void OpenPanel(int myProfileId, string currentAvatarId, UIFriendProfilePanel profilePanel)
        {
            _myProfileId = myProfileId;
            _selectedAvatarId = currentAvatarId;
            _profilePanel = profilePanel;

            gameObject.SetActive(true);
            PopulateAvatarList();
        }

        public void ClosePanel()
        {
            gameObject.SetActive(false);
        }

        private void PopulateAvatarList()
        {
            // Xoá các item cũ
            foreach (Transform child in avatarListContainer)
            {
                Destroy(child.gameObject);
            }

            // Tạo các item mới
            foreach (var avatarId in _availableAvatars)
            {
                GameObject btnObj = Instantiate(avatarButtonPrefab, avatarListContainer);
                Image img = btnObj.GetComponent<Image>();
                Button btn = btnObj.GetComponent<Button>();

                Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarId}");
                if (avatarSprite != null)
                {
                    img.sprite = avatarSprite;
                }
                else
                {
                    Debug.LogWarning($"[UIAvatarSelection] Không tìm thấy sprite cho {avatarId} trong Resources/Avatars/");
                }

                // Đánh dấu avatar đang được chọn
                if (avatarId == _selectedAvatarId)
                {
                    btnObj.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f); // Highlight đơn giản
                }

                btn.onClick.AddListener(() =>
                {
                    SelectAvatar(avatarId);
                });
            }
        }

        private void SelectAvatar(string avatarId)
        {
            _selectedAvatarId = avatarId;
            PopulateAvatarList(); // Cập nhật lại UI để hiển thị highlight
        }

        private void SaveAvatar()
        {
            if (string.IsNullOrEmpty(_selectedAvatarId))
            {
                Debug.LogWarning("[UIAvatarSelection] Chưa chọn avatar nào!");
                return;
            }

            var updateRequest = new UpdatePlayerProfileRequest
            {
                AvatarUrl = _selectedAvatarId
            };

            // Vô hiệu hóa nút Save để tránh bấm nhiều lần
            if (saveButton != null) saveButton.interactable = false;

            PlayerApi.Instance.UpdateProfile(_myProfileId, updateRequest,
                onSuccess: (response) =>
                {
                    Debug.Log($"[UIAvatarSelection] Lưu Avatar thành công: {response.AvatarUrl}");
                    if (saveButton != null) saveButton.interactable = true;
                    
                    // Cập nhật lại giao diện Profile
                    if (_profilePanel != null)
                    {
                        _profilePanel.UpdateAvatarImage(response.AvatarUrl);
                    }
                    ClosePanel();
                },
                onError: (error) =>
                {
                    Debug.LogError($"[UIAvatarSelection] Lỗi khi lưu avatar: {error.Message}");
                    if (saveButton != null) saveButton.interactable = true;
                });
        }
    }
}
