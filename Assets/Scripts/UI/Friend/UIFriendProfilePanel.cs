using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models;
using MysticJourney.API.Models.Response;

namespace UI.Friend
{
    public class UIFriendProfilePanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private UnityEngine.UI.Image avatarImage;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text classText;
        [SerializeField] private TMP_Text guildText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text friendsCountText;
        [SerializeField] private Button closeButton;
        [Tooltip("PlayerProfilePanel/RightPanel/LogoutButton. Bỏ trống thì tự tìm theo tên.")]
        [SerializeField] private Button logoutButton;

        [Header("Class Art")]
        [Tooltip("Huy hiệu class nhỏ (LeftPanel/Bg_Class/Deco/ClassIcon).")]
        [SerializeField] private Image classIconImage;
        // Sprite gán tay: 3 file này nằm ngoài Assets/Resources nên không Resources.Load được.
        [SerializeField] private Sprite knightIcon;
        [SerializeField] private Sprite mageIcon;
        [SerializeField] private Sprite archerIcon;

        [Header("Avatar & Name Edit")]
        [SerializeField] private Button editAvatarButton;
        [SerializeField] private UIAvatarSelectionPanel avatarSelectionPanel;
        [SerializeField] private Button editNameButton;
        
        [Header("Name Change Panel")]
        [SerializeField] private GameObject nameChangePanel;
        [SerializeField] private TMP_InputField nameChangeInput;
        [SerializeField] private Button nameChangeSaveButton;
        [SerializeField] private Button nameChangeCancelButton;
        [SerializeField] private TMP_Text nameChangeMessageText;

        [Header("Achievement List")]
        [SerializeField] private GameObject achievementListPanel;
        [SerializeField] private Button achievementListCloseButton;
        [SerializeField] private ScrollRect achievementScrollRect;
        [SerializeField] private Transform achievementContent;
        [SerializeField] private GameObject achievementItemPrefab;
        [SerializeField] private TMP_Text achievementSummaryText;

        [Header("Achievement Pagination")]
        [SerializeField] private Button achievementPrevButton;
        [SerializeField] private Button achievementNextButton;
        [SerializeField] private TMP_Text achievementPageText;

        private int _currentAchievementPage = 1;
        private const int ACHIEVEMENTS_PER_PAGE = 5;

        [Header("Achievement Detail")]
        [SerializeField] private GameObject achievementDetailPanel;
        [SerializeField] private Button achievementDetailCloseButton;
        [SerializeField] private Image achievementDetailIconImage;
        [SerializeField] private TMP_Text achievementDetailBadgeText;
        [SerializeField] private TMP_Text achievementDetailNameText;
        [SerializeField] private TMP_Text achievementDetailTypeText;
        [SerializeField] private TMP_Text achievementDetailDescriptionText;
        [SerializeField] private TMP_Text achievementDetailProgressText;
        [SerializeField] private TMP_Text achievementDetailRewardText;
        [SerializeField] private TMP_Text achievementDetailStatusText;
        [SerializeField] private TMP_Text achievementDetailBuffText;
        [SerializeField] private Button viewAchievementListButton;

        private readonly List<GameObject> _achievementItemInstances = new();
        private readonly Dictionary<int, PlayerAchievementResponse> _ownedAchievementMap = new();
        private List<AchievementResponse> _achievementCatalog = new();
        private FriendProfileDto _currentProfile;
        private PlayerMeAchievementsResponse _currentAchievements;
        private PlayerAchievementResponse _selectedAchievement;
        private bool _isCurrentPlayerProfile;
        
        private void Awake()
        {
            BindCloseButtons();
            AddHoverEffects();
        }

        private void Start()
        {
            if (viewAchievementListButton != null)
                viewAchievementListButton.onClick.AddListener(ShowAchievementListView);

            if (achievementListCloseButton != null)
                achievementListCloseButton.onClick.AddListener(() =>
                {
                    if (achievementListPanel != null)
                        achievementListPanel.SetActive(false);
                });

            if (achievementPrevButton != null)
                achievementPrevButton.onClick.AddListener(OnPrevAchievementPage);

            if (achievementNextButton != null)
                achievementNextButton.onClick.AddListener(OnNextAchievementPage);

            if (achievementDetailCloseButton != null)
                achievementDetailCloseButton.onClick.AddListener(CloseAchievementPopup);

            if (achievementDetailPanel != null)
                achievementDetailPanel.SetActive(false);

            if (editAvatarButton != null)
                editAvatarButton.onClick.AddListener(OpenAvatarSelection);

            if (editNameButton != null)
                editNameButton.onClick.AddListener(OpenNameChangePanel);

            if (nameChangeSaveButton != null)
                nameChangeSaveButton.onClick.AddListener(OnNameChangeSaveClicked);

            if (nameChangeCancelButton != null)
                nameChangeCancelButton.onClick.AddListener(CloseNameChangePanel);

            if (nameChangePanel != null)
                nameChangePanel.SetActive(false);
        }

        public void ShowMyProfile()
        {
            int myProfileId = MysticJourney.Core.Services.GameStateService.Instance.PlayerProfileId;
            if (myProfileId > 0)
            {
                ShowProfile(myProfileId, null);
            }
            else
            {
                Debug.LogWarning("[UIFriendProfilePanel] Cannot find local Player Profile ID.");
            }
        }

        public void ShowProfile(int profileId, string token) // Token kept here for legacy signature if called from elsewhere, though unused in API
        {
            Debug.Log("[UIFriendProfilePanel] ShowProfile called for " + profileId);
            if (transform.parent != null) {
                Debug.Log($"[UIFriendProfilePanel] Parent is {transform.parent.name}, activeInHierarchy: {transform.parent.gameObject.activeInHierarchy}");
            }
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // Ensure it renders on top of FriendPanel
            ClearAchievementList();
            SetLoadingState();

            _isCurrentPlayerProfile = MysticJourney.Core.Services.GameStateService.Instance.PlayerProfileId == profileId;

            FriendApi.GetFriendProfile(profileId, profile =>
            {
                _currentProfile = profile;
                ApplyProfile(profile);

                if (_isCurrentPlayerProfile)
                {
                    LoadFriendsCount();
                    LoadAchievementCatalog();
                }
                else
                {
                    if (friendsCountText != null) friendsCountText.text = "Friends: N/A";
                    if (achievementSummaryText != null) achievementSummaryText.text = "Achievements: private";
                    ClearAchievementDetail();
                }
            }, err => 
            {
                Debug.LogError($"Failed to load profile: {err.Message}");
                if (nameText != null) nameText.text = "Error loading profile.";
            });
        }

        private void SetLoadingState()
        {
            if (nameText != null) nameText.text = "Loading...";
            if (levelText != null) levelText.text = string.Empty;
            if (classText != null) classText.text = string.Empty;
            if (guildText != null) guildText.text = string.Empty;
            if (titleText != null) titleText.text = string.Empty;
            if (friendsCountText != null) friendsCountText.text = string.Empty;
            if (achievementSummaryText != null) achievementSummaryText.text = string.Empty;
            if (achievementDetailPanel != null) achievementDetailPanel.SetActive(false);
            ClearAchievementDetail();
        }



        private void ApplyProfile(FriendProfileDto profile)
        {
            if (nameText != null) nameText.text = profile.CharacterName;
            if (levelText != null) levelText.text = $"Level {profile.Level}";
            string className = string.IsNullOrEmpty(profile.Class) ? "Knight" : profile.Class;
            if (classText != null) classText.text = className;
            ApplyClassArt(className);
            if (guildText != null) guildText.text = $"Guild: {profile.Guild}";
            if (titleText != null) titleText.text = $"Title: {profile.Title}";

            ApplyProfileAvatar(profile.AvatarUrl);

            if (editAvatarButton != null)
                editAvatarButton.gameObject.SetActive(_isCurrentPlayerProfile);

            if (editNameButton != null)
                editNameButton.gameObject.SetActive(_isCurrentPlayerProfile);

            // Achievement List button: hiện khi đang xem profile của chính mình.
            if (viewAchievementListButton != null)
                viewAchievementListButton.gameObject.SetActive(_isCurrentPlayerProfile);
        }

        /// <summary>Đổi huy hiệu class nhỏ. BE trả về string nên so sánh không phân biệt hoa/thường, không khớp thì về Knight.</summary>
        private void ApplyClassArt(string className)
        {
            Sprite icon = knightIcon;

            if (string.Equals(className, "Mage", StringComparison.OrdinalIgnoreCase))
            {
                icon = mageIcon;
            }
            else if (string.Equals(className, "Archer", StringComparison.OrdinalIgnoreCase))
            {
                icon = archerIcon;
            }

            if (classIconImage != null && icon != null) classIconImage.sprite = icon;
        }

        private void OpenNameChangePanel()
        {
            if (nameChangePanel != null)
            {
                if (nameChangeInput != null) nameChangeInput.text = "";
                if (nameChangeMessageText != null) nameChangeMessageText.text = "";
                
                // Cập nhật text của nút save dựa theo việc đổi tên có tốn phí hay không
                if (nameChangeSaveButton != null)
                {
                    var btnText = nameChangeSaveButton.GetComponentInChildren<TMP_Text>();
                    if (btnText != null)
                    {
                        bool isFree = _currentProfile != null && !_currentProfile.HasChangedName;
                        btnText.text = isFree ? "Save (Free)" : "Save (500 Gems)";
                    }
                }

                nameChangePanel.SetActive(true);
            }
        }

        private void CloseNameChangePanel()
        {
            if (nameChangePanel != null)
                nameChangePanel.SetActive(false);
        }

        private void OnNameChangeSaveClicked()
        {
            if (nameChangeInput == null) return;

            string newName = nameChangeInput.text.Trim();
            if (string.IsNullOrEmpty(newName) || newName.Length < 3 || newName.Length > 16)
            {
                if (nameChangeMessageText != null) nameChangeMessageText.text = "Name must be 3-16 chars.";
                return;
            }

            if (nameChangeSaveButton != null) nameChangeSaveButton.interactable = false;
            if (nameChangeMessageText != null) nameChangeMessageText.text = "Processing...";

            var request = new MysticJourney.API.Models.Request.ChangeNameRequestDto { NewName = newName };

            MysticJourney.API.Endpoints.PlayerApi.Instance.ChangeName(request,
                response =>
                {
                    if (nameChangeSaveButton != null) nameChangeSaveButton.interactable = true;
                    if (nameText != null) nameText.text = response.DisplayName;
                    // Cập nhật lại current profile
                    if (_currentProfile != null) _currentProfile.HasChangedName = true;
                    CloseNameChangePanel();
                },
                error =>
                {
                    if (nameChangeSaveButton != null) nameChangeSaveButton.interactable = true;
                    if (nameChangeMessageText != null) nameChangeMessageText.text = error.Message;
                    Debug.LogError($"Change Name Failed: {error.Message}");
                });
        }

        private void ApplyProfileAvatar(string avatarUrl)
        {
            if (avatarImage == null) return;

            if (string.IsNullOrEmpty(avatarUrl))
                avatarUrl = "avatar_1"; // Default avatar

            Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
            if (avatarSprite != null)
            {
                avatarImage.sprite = avatarSprite;
            }
        }

        private void OpenAvatarSelection()
        {
            if (_isCurrentPlayerProfile && avatarSelectionPanel != null && _currentProfile != null)
            {
                int myProfileId = MysticJourney.Core.Services.GameStateService.Instance.PlayerProfileId;
                avatarSelectionPanel.OpenPanel(myProfileId, _currentProfile.AvatarUrl, this);
            }
        }

        public void UpdateAvatarImage(string avatarUrl)
        {
            if (_currentProfile != null)
                _currentProfile.AvatarUrl = avatarUrl;

            ApplyProfileAvatar(avatarUrl);

            // Avatar còn hiện ở HUD (TopBar). HUD chỉ tự tải lại mỗi 15 giây nên nếu không đẩy
            // sang đây, người chơi đổi avatar xong vẫn thấy ảnh cũ ở góc màn hình.
            if (_isCurrentPlayerProfile)
                PlayerHUDController.Instance?.ApplyAvatar(avatarUrl);
        }

        private void LoadFriendsCount()
        {
            if (friendsCountText != null)
                friendsCountText.text = "Friends: ...";

            FriendApi.GetFriendList(friends =>
            {
                if (friendsCountText != null)
                    friendsCountText.text = $"Friends: {friends?.Count ?? 0}";
            }, err =>
            {
                Debug.LogWarning($"Failed to load friends count: {err.Message}");
                if (friendsCountText != null)
                    friendsCountText.text = "Friends: N/A";
            });
        }

        private void LoadAchievementCatalog()
        {
            if (achievementSummaryText != null)
                achievementSummaryText.text = "Achievements: loading...";

            AchievementApi.Instance.GetAll(1, 1000,
            response =>
            {
                _achievementCatalog = response?.Items != null
                    ? response.Items.Where(a => a != null && a.IsActive).OrderBy(a => GetRarityTier(a.Point)).ThenBy(a => a.Name).ToList()
                    : new List<AchievementResponse>();

                LoadOwnedAchievements();
            },
            err =>
            {
                Debug.LogWarning($"Failed to load achievement catalog: {err.Message}");
                if (achievementSummaryText != null)
                    achievementSummaryText.text = "Achievements: unavailable";
                ClearAchievementDetail();
            },
            isActive: true);
        }

        private void LoadOwnedAchievements()
        {
            AchievementApi.Instance.GetMyAchievements(response =>
            {
                _currentAchievements = response;
                _ownedAchievementMap.Clear();

                if (response?.Achievements != null)
                {
                    foreach (var owned in response.Achievements)
                    {
                        if (owned != null)
                        {
                            _ownedAchievementMap[owned.AchievementId] = owned;
                        }
                    }
                }

                PopulateAchievementList();

                if (achievementSummaryText != null)
                    achievementSummaryText.text = $"Total: {_ownedAchievementMap.Count}";

                CloseAchievementPopup();
            }, err =>
            {
                Debug.LogWarning($"Failed to load owned achievements: {err.Message}");
                _ownedAchievementMap.Clear();
                PopulateAchievementList();
                if (achievementSummaryText != null)
                    achievementSummaryText.text = $"Total: 0";
                ClearAchievementDetail();
            });
        }

        private void PopulateAchievementList()
        {
            ClearAchievementList();

            if (achievementContent == null || achievementItemPrefab == null)
                return;

            int totalItems = _achievementCatalog.Count;
            int totalPages = Mathf.CeilToInt((float)totalItems / ACHIEVEMENTS_PER_PAGE);
            if (totalPages < 1) totalPages = 1;

            if (_currentAchievementPage > totalPages) _currentAchievementPage = totalPages;
            if (_currentAchievementPage < 1) _currentAchievementPage = 1;

            int startIndex = (_currentAchievementPage - 1) * ACHIEVEMENTS_PER_PAGE;
            int endIndex = Mathf.Min(startIndex + ACHIEVEMENTS_PER_PAGE, totalItems);

            for (int i = startIndex; i < endIndex; i++)
            {
                var achievement = _achievementCatalog[i];
                var item = Instantiate(achievementItemPrefab, achievementContent);
                item.transform.localScale = Vector3.one;
                _achievementItemInstances.Add(item);

                var owned = _ownedAchievementMap.TryGetValue(achievement.AchievementId, out var ownedAchievement)
                    ? ownedAchievement
                    : null;
                var isOwned = owned != null;

                var canvasGroup = item.GetComponent<CanvasGroup>() ?? item.AddComponent<CanvasGroup>();
                canvasGroup.alpha = isOwned ? 1f : 0.35f;
                canvasGroup.interactable = isOwned;
                canvasGroup.blocksRaycasts = isOwned;

                string rarityHex = GetRarityColorHex(achievement.Point);
                string rarityLabel = GetRarityLabel(achievement.Point);
                string statusLabel = owned != null 
                    ? (owned.IsCompleted ? "Unlocked" : $"{owned.Progress}/{achievement.RequiredValue}") 
                    : $"0/{achievement.RequiredValue}";
                string displayText = $"<color={rarityHex}>{achievement.Name}</color>\n<size=70%>{rarityLabel}</size>\n<size=60%>{statusLabel}</size>";

                var titleTextTransform = item.transform.Find("TitleText");
                var tmpText = titleTextTransform != null ? titleTextTransform.GetComponent<TMP_Text>() : item.GetComponentInChildren<TMP_Text>(true);
                
                if (tmpText != null)
                {
                    tmpText.text = displayText;
                }
                else
                {
                    var legacyText = item.GetComponentInChildren<UnityEngine.UI.Text>(true);
                    if (legacyText != null)
                    {
                        legacyText.text = $"{achievement.Name}\n{rarityLabel}\n{statusLabel}";
                    }
                }

                var iconTransform = item.transform.Find("Icon");
                var image = iconTransform != null ? iconTransform.GetComponent<Image>() : item.GetComponent<Image>();
                
                if (image != null)
                {
                    image.color = isOwned ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.55f);
                    if (!string.IsNullOrEmpty(achievement.IconUrl))
                    {
                        var sprite = Resources.Load<Sprite>($"Icons/Titles/{achievement.IconUrl}");
                        if (sprite != null)
                        {
                            image.sprite = sprite;
                        }
                    }
                }

                var button = item.GetComponent<Button>() ?? item.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    var capturedAchievement = achievement;
                    var capturedOwnedAchievement = owned;
                    button.onClick.RemoveAllListeners();
                    button.interactable = isOwned;
                    if (isOwned)
                    {
                        button.onClick.AddListener(() => SelectAchievement(capturedAchievement, capturedOwnedAchievement));
                    }
                }
            }

            if (achievementScrollRect != null)
            {
                achievementScrollRect.verticalNormalizedPosition = 1f;
            }

            UpdatePaginationUI(totalPages);
        }

        private void UpdatePaginationUI(int totalPages)
        {
            if (achievementPageText != null)
            {
                achievementPageText.text = $"{_currentAchievementPage}/{totalPages}";
            }

            if (achievementPrevButton != null)
            {
                achievementPrevButton.interactable = _currentAchievementPage > 1;
            }

            if (achievementNextButton != null)
            {
                achievementNextButton.interactable = _currentAchievementPage < totalPages;
            }
        }

        private void OnPrevAchievementPage()
        {
            if (_currentAchievementPage > 1)
            {
                _currentAchievementPage--;
                PopulateAchievementList();
            }
        }

        private void OnNextAchievementPage()
        {
            int totalItems = _achievementCatalog.Count;
            int totalPages = Mathf.CeilToInt((float)totalItems / ACHIEVEMENTS_PER_PAGE);
            if (totalPages < 1) totalPages = 1;

            if (_currentAchievementPage < totalPages)
            {
                _currentAchievementPage++;
                PopulateAchievementList();
            }
        }

        private void ShowAchievementDetail(AchievementResponse achievement, PlayerAchievementResponse ownedAchievement)
        {
            if (achievement == null)
                return;

            _selectedAchievement = ownedAchievement;

            if (achievementDetailPanel != null)
                achievementDetailPanel.SetActive(true);

            if (achievementDetailNameText != null) achievementDetailNameText.text = achievement.Name;
            if (achievementDetailTypeText != null) achievementDetailTypeText.text = $"Rarity: {GetRarityLabel(achievement.Point)}";
            if (achievementDetailDescriptionText != null) achievementDetailDescriptionText.text = achievement.Description ?? "No description.";
            if (achievementDetailProgressText != null)
            {
                int progress = ownedAchievement?.Progress ?? 0;
                achievementDetailProgressText.text = $"Progress: {progress}/{achievement.RequiredValue}";
            }
            if (achievementDetailRewardText != null)
            {
                achievementDetailRewardText.text = $"Reward: {achievement.RewardGold} gold, {achievement.RewardGem} gem, {achievement.RewardQuantity} item(s)";
            }
            if (achievementDetailStatusText != null)
            {
                achievementDetailStatusText.text = ownedAchievement != null && ownedAchievement.IsCompleted
                    ? "Status: Unlocked"
                    : "Status: Locked";
            }

            if (achievementDetailIconImage != null && !string.IsNullOrEmpty(achievement.IconUrl))
            {
                var sprite = Resources.Load<Sprite>($"Icons/Titles/{achievement.IconUrl}");
                if (sprite != null)
                {
                    achievementDetailIconImage.sprite = sprite;
                    achievementDetailIconImage.enabled = true; // MUST ENABLE THE IMAGE
                    // Optional: You can make it opaque if unlocked, slightly transparent if locked
                    achievementDetailIconImage.color = (ownedAchievement != null && ownedAchievement.IsCompleted) 
                        ? new Color(1f, 1f, 1f, 1f) 
                        : new Color(1f, 1f, 1f, 0.55f);
                }
            }

            if (achievementDetailBuffText != null)
            {
                achievementDetailBuffText.text = !string.IsNullOrEmpty(achievement.BuffDescription) 
                    ? achievement.BuffDescription 
                    : GetBuffDescription(achievement.Point, achievement.Type);
            }

            if (achievementDetailBadgeText != null)
            {
                achievementDetailBadgeText.text = GetRarityLabel(achievement.Point);
                achievementDetailBadgeText.color = GetRarityColor(achievement.Point);
            }

            // Apply custom icon from Resources based on IconUrl
            if (!string.IsNullOrEmpty(achievement.IconUrl))
            {
                var sprite = Resources.Load<Sprite>($"Icons/Titles/{achievement.IconUrl}");
                if (sprite != null && achievementDetailIconImage != null)
                {
                    achievementDetailIconImage.sprite = sprite;
                }
            }
            else
            {
                ApplyAchievementIcon(achievement.IconUrl);
            }
        }

        public void ShowAchievementListView()
        {
            _currentAchievementPage = 1;
            PopulateAchievementList();

            if (achievementListPanel != null)
                achievementListPanel.SetActive(true);

            if (achievementDetailPanel != null)
                achievementDetailPanel.SetActive(false);

            if (achievementScrollRect != null)
            {
                achievementScrollRect.gameObject.SetActive(true);
                achievementScrollRect.verticalNormalizedPosition = 1f;
            }
        }


        public void SelectAchievement(AchievementResponse achievement, PlayerAchievementResponse ownedAchievement)
        {
            ShowAchievementDetail(achievement, ownedAchievement);
        }

        public void CloseAchievementPopup()
        {
            if (achievementDetailPanel != null)
                achievementDetailPanel.SetActive(false);

            if (achievementDetailCloseButton != null)
            {
                achievementDetailCloseButton.interactable = true;
            }

            _selectedAchievement = null;
        }

        private int GetRarityTier(int point)
        {
            if (point >= 80) return 3;
            if (point >= 50) return 2;
            if (point >= 25) return 1;
            return 0;
        }

        private string GetRarityLabel(int point)
        {
            return GetRarityTier(point) switch
            {
                0 => "Common",
                1 => "Uncommon",
                2 => "Rare",
                _ => "Legendary"
            };
        }

        private string GetRarityColorHex(int point)
        {
            return GetRarityTier(point) switch
            {
                0 => "#66BB6A",
                1 => "#42A5F5",
                2 => "#AB47BC",
                _ => "#FB8C00"
            };
        }

        private Color GetRarityColor(int point)
        {
            ColorUtility.TryParseHtmlString(GetRarityColorHex(point), out var color);
            return color;
        }

        private string GetBuffDescription(int point, string type)
        {
            int tier = GetRarityTier(point);
            string buff;

            switch (tier)
            {
                case 0:
                    buff = "+1 ATK, +1 DEF";
                    break;
                case 1:
                    buff = "+2 ATK, +1 DEF";
                    break;
                case 2:
                    buff = "+2 ATK, +2 DEF, +1 HP";
                    break;
                default:
                    buff = "+3 ATK, +2 DEF, +2 HP, +1 Crit";
                    break;
            }

            return $"Buff: {buff} | Type: {type}";
        }

        private void ClearAchievementDetail()
        {
            if (achievementDetailNameText != null) achievementDetailNameText.text = string.Empty;
            if (achievementDetailTypeText != null) achievementDetailTypeText.text = string.Empty;
            if (achievementDetailDescriptionText != null) achievementDetailDescriptionText.text = string.Empty;
            if (achievementDetailProgressText != null) achievementDetailProgressText.text = string.Empty;
            if (achievementDetailRewardText != null) achievementDetailRewardText.text = string.Empty;
            if (achievementDetailStatusText != null) achievementDetailStatusText.text = string.Empty;
            if (achievementDetailBuffText != null) achievementDetailBuffText.text = string.Empty;
            if (achievementDetailBadgeText != null) achievementDetailBadgeText.text = string.Empty;
            if (achievementDetailIconImage != null)
            {
                achievementDetailIconImage.sprite = null;
                achievementDetailIconImage.enabled = false;
            }

            _selectedAchievement = null;
        }

        private void ApplyAchievementIcon(string iconUrl)
        {
            if (achievementDetailIconImage == null)
                return;

            achievementDetailIconImage.enabled = false;

            if (string.IsNullOrWhiteSpace(iconUrl))
                return;

            if (Uri.TryCreate(iconUrl, UriKind.Absolute, out var absoluteUri) &&
                (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                StartCoroutine(LoadRemoteAchievementIcon(iconUrl));
                return;
            }

            var resourceSprite = Resources.Load<Sprite>(iconUrl);
            if (resourceSprite != null)
            {
                achievementDetailIconImage.sprite = resourceSprite;
                achievementDetailIconImage.enabled = true;
            }
        }

        private System.Collections.IEnumerator LoadRemoteAchievementIcon(string iconUrl)
        {
            using (var request = UnityWebRequestTexture.GetTexture(iconUrl))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[UIFriendProfilePanel] Failed to load achievement icon: {request.error}");
                    yield break;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                if (texture == null || achievementDetailIconImage == null)
                    yield break;

                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                achievementDetailIconImage.sprite = sprite;
                achievementDetailIconImage.enabled = true;
            }
        }

        private void ClearAchievementList()
        {
            if (achievementContent == null)
                return;

            for (int i = achievementContent.childCount - 1; i >= 0; i--)
            {
                Destroy(achievementContent.GetChild(i).gameObject);
            }

            _achievementItemInstances.Clear();
        }

        private void BindCloseButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(ClosePanel);
                closeButton.onClick.AddListener(ClosePanel);
            }

            var headerExitBtn = transform.Find("Header/ExitButton")?.GetComponent<Button>()
                             ?? transform.Find("ExitButton")?.GetComponent<Button>();
            if (headerExitBtn != null && headerExitBtn != closeButton)
            {
                headerExitBtn.onClick.RemoveListener(ClosePanel);
                headerExitBtn.onClick.AddListener(ClosePanel);
            }

            BindLogoutButton();
        }

        // LogoutButton nằm trong panel này (RightPanel/LogoutButton) nhưng onClick trong scene
        // RỖNG và không script nào tham chiếu tới nó → bấm vào không có gì xảy ra. Bind bằng code
        // để không phụ thuộc việc gán tay trong Inspector.
        private void BindLogoutButton()
        {
            if (logoutButton == null)
            {
                logoutButton = transform.Find("RightPanel/LogoutButton")?.GetComponent<Button>();

                if (logoutButton == null)
                {
                    var buttons = GetComponentsInChildren<Button>(true);
                    for (var i = 0; i < buttons.Length; i++)
                    {
                        if (buttons[i] != null &&
                            buttons[i].name.IndexOf("Logout", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            logoutButton = buttons[i];
                            break;
                        }
                    }
                }
            }

            if (logoutButton == null)
                return;

            logoutButton.onClick.RemoveListener(OnLogoutClicked);
            logoutButton.onClick.AddListener(OnLogoutClicked);
        }

        // Only meaningful for the signed-in account: this panel also shows other players' profiles,
        // where Logout makes no sense (and is easy to hit by accident).
        // Logout is not undoable (session gone, back to MainMenu), so confirm first.
        private void OnLogoutClicked()
        {
            PartyPopupConfirm.Show(transform, "Log out of your account?",
                MysticJourney.Core.Services.SessionService.Logout);
        }

        public void ClosePanel()
        {
            if (UIManager.Instance != null)
                UIManager.Instance.ClosePanel(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void AddHoverEffects()
        {
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn != null && btn.GetComponent<UIHoverScaleEffect>() == null)
                {
                    btn.gameObject.AddComponent<UIHoverScaleEffect>();
                }
            }
        }
    }
}
