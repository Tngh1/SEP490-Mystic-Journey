using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;

namespace UI.Friend
{
    // Executes mono behaviour operation.
    public class UIAvatarSelectionPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Transform avatarListContainer;
        [SerializeField] private GameObject avatarButtonPrefab;

        private string _selectedAvatarId;
        private int _myProfileId;
        private PlayerProfileUIManager _profilePanel;

        private readonly List<string> _availableAvatars = new List<string>
        {
            "avatar_1", "avatar_2", "avatar_3", "avatar_4", "avatar_5",
            "avatar_6", "avatar_7", "avatar_8", "avatar_9", "avatar_10"
        };

        // Initializes internal component caches and dependencies for UIAvatarSelectionPanel upon GameObject instantiation.
        // Executes during scene loading prior to Start to ensure critical references are wired up.
        private void Awake()
        {
            if (closeButton == null)
                closeButton = transform.Find("CloseButton")?.GetComponent<Button>();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(ClosePanel);
                closeButton.onClick.AddListener(ClosePanel);
            }

            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(SaveAvatar);
                saveButton.onClick.AddListener(SaveAvatar);
            }

            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                if (btn != null && btn.GetComponent<UIHoverScaleEffect>() == null)
                    btn.gameObject.AddComponent<UIHoverScaleEffect>();
            }
        }

        // Refresh visible state and subscribe the event handlers required while this component is active.
        private void OnEnable()
        {
            if (closeButton == null)
                closeButton = GetComponentsInChildren<Button>(true)
                    [System.Array.FindIndex(GetComponentsInChildren<Button>(true), b => b.name.IndexOf("Close", System.StringComparison.OrdinalIgnoreCase) >= 0)];
            if (saveButton == null)
                saveButton = GetComponentsInChildren<Button>(true)
                    [System.Array.FindIndex(GetComponentsInChildren<Button>(true), b => b.name.IndexOf("Save", System.StringComparison.OrdinalIgnoreCase) >= 0)];

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(ClosePanel);
                closeButton.onClick.AddListener(ClosePanel);
            }
            if (saveButton != null)
            {
                saveButton.onClick.RemoveListener(SaveAvatar);
                saveButton.onClick.AddListener(SaveAvatar);
            }
        }

        // Executes open panel operation.
        public void OpenPanel(int myProfileId, string currentAvatarId, PlayerProfileUIManager profilePanel)
        {
            _myProfileId = myProfileId;
            _selectedAvatarId = currentAvatarId;
            _profilePanel = profilePanel;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            PopulateAvatarList();
        }

        // Update visibility for panel; it updates active.
        public void ClosePanel()
        {
            gameObject.SetActive(false);
        }

        // Executes populate avatar list operation.
        private void PopulateAvatarList()
        {
            if (avatarListContainer == null || avatarButtonPrefab == null)
            {
                Debug.LogError("[UIAvatarSelection] Avatar list container or button prefab is not assigned.");
                return;
            }

            foreach (Transform child in avatarListContainer)
            {
                Destroy(child.gameObject);
            }

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

                if (avatarId == _selectedAvatarId)
                {
                    btnObj.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);
                }

                btn.onClick.AddListener(() =>
                {
                    SelectAvatar(avatarId);
                });
            }
        }

        // Executes select avatar operation.
        // Validates input parameters against null or empty values.
        private void SelectAvatar(string avatarId)
        {
            _selectedAvatarId = avatarId;
            PopulateAvatarList();
        }

        // Executes save avatar operation.
        // Validates input parameters against null or empty values.
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

            if (saveButton != null) saveButton.interactable = false;

            PlayerApi.Instance.UpdateProfile(_myProfileId, updateRequest,
                onSuccess: (response) =>
                {
                    Debug.Log($"[UIAvatarSelection] Lưu Avatar thành công: {response.AvatarUrl}");
                    if (saveButton != null) saveButton.interactable = true;

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
