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

        [Tooltip("AchievementListPanel/NoAchievementText - hiện khi catalog rỗng.")]
        [SerializeField] private GameObject noAchievementText;

        [Header("Achievement Rarity Badges")]
        // Gán tay: mấy file Gacha*.png nằm ngoài Assets/Resources nên không Resources.Load được.
        [SerializeField] private Sprite commonBadgeIcon;
        [SerializeField] private Sprite uncommonBadgeIcon;
        [SerializeField] private Sprite rareBadgeIcon;
        [SerializeField] private Sprite legendaryBadgeIcon;

        [Header("Achievement Pagination")]
        [SerializeField] private Button achievementPrevButton;
        [SerializeField] private Button achievementNextButton;
        [SerializeField] private TMP_Text achievementPageText;

        private int _currentAchievementPage = 1;
        private const int ACHIEVEMENTS_PER_PAGE = 3;

        [Header("Achievement Detail")]
        [SerializeField] private GameObject achievementDetailPanel;
        [SerializeField] private Button achievementDetailCloseButton;
        [SerializeField] private Image achievementDetailIconImage;
        [SerializeField] private TMP_Text achievementDetailBadgeText;
        [SerializeField] private TMP_Text achievementDetailNameText;
        [SerializeField] private Button viewAchievementListButton;

        [Header("Achievement Detail - Reward Icons")]
        [SerializeField] private Image goldIcon;
        [SerializeField] private TMP_Text goldAmountText;
        [SerializeField] private Image gemIcon;
        [SerializeField] private TMP_Text gemAmountText;
        [SerializeField] private Transform itemSlotContainer;
        [SerializeField] private Transform buffContainer;
        [SerializeField] private GameObject inventorySlotPrefab;
        [SerializeField] private GameObject buffSlotPrefab;

        [Header("Stat Icons")]
        [SerializeField] private Sprite atkStatIcon;
        [SerializeField] private Sprite defStatIcon;
        [SerializeField] private Sprite hpStatIcon;
        [SerializeField] private Sprite critStatIcon;
        [SerializeField] private Sprite spdStatIcon;

        private readonly List<GameObject> _achievementItemInstances = new();
        private readonly Dictionary<int, PlayerAchievementResponse> _ownedAchievementMap = new();
        private List<AchievementResponse> _achievementCatalog = new();
        private FriendProfileDto _currentProfile;
        private PlayerMeAchievementsResponse _currentAchievements;
        private PlayerAchievementResponse _selectedAchievement;
        private bool _isCurrentPlayerProfile;
        
        private void Awake()
        {
            DisableBlockingBackgroundRaycast();
            BindCloseButtons();
            BindProfileActions();
            AddHoverEffects();
        }

        private void OnEnable()
        {
            DisableBlockingBackgroundRaycast();
            // UIManager toggles this panel instead of recreating it. Rebind on
            // every enable so scene/prefab variants with missing Inspector
            // references still get working edit controls.
            BindProfileActions();
        }

        private void DisableBlockingBackgroundRaycast()
        {
            // This full-panel decoration is rendered above the avatar/name edit
            // buttons in Main.unity. It must not consume their pointer events.
            var background = transform.Find("Background")?.GetComponent<Graphic>();
            if (background != null)
                background.raycastTarget = false;
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

            // Scene để panel này activeSelf=true; chỉ ShowAchievementListView mới được mở nó.
            if (achievementListPanel != null)
                achievementListPanel.SetActive(false);

            if (nameChangeSaveButton != null)
                nameChangeSaveButton.onClick.AddListener(OnNameChangeSaveClicked);

            if (nameChangeCancelButton != null)
                nameChangeCancelButton.onClick.AddListener(CloseNameChangePanel);

            if (nameChangePanel != null)
                nameChangePanel.SetActive(false);
        }

        private void BindProfileActions()
        {
            // Bind in Awake so the first click works immediately after this initially
            // inactive panel is enabled. RemoveListener keeps the binding idempotent.
            if (editAvatarButton == null)
                editAvatarButton = transform.Find("LeftPanel/EditAvatarButton")?.GetComponent<Button>();
            if (avatarSelectionPanel == null)
                avatarSelectionPanel = GetComponentInChildren<UIAvatarSelectionPanel>(true);
            if (editNameButton == null)
                editNameButton = GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name == "ChangeNameButton");
            if (nameChangePanel == null)
                nameChangePanel = GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == "NameChangePanel" || t.name == "NameChangePopup")?.gameObject;
            if (nameChangeInput == null)
                nameChangeInput = GetComponentInChildren<TMP_InputField>(true);
            if (nameChangeSaveButton == null)
                nameChangeSaveButton = GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name.IndexOf("Save", StringComparison.OrdinalIgnoreCase) >= 0);
            if (nameChangeCancelButton == null)
                nameChangeCancelButton = GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(button => button.name.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0);

            if (editAvatarButton != null)
            {
                editAvatarButton.onClick.RemoveListener(OpenAvatarSelection);
                editAvatarButton.onClick.AddListener(OpenAvatarSelection);
            }

            if (editNameButton != null)
            {
                editNameButton.onClick.RemoveListener(OpenNameChangePanel);
                editNameButton.onClick.AddListener(OpenNameChangePanel);
            }
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
                    if (friendsCountText != null) friendsCountText.text = "N/A";
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
            if (levelText != null) levelText.text = profile.Level.ToString();
            string className = string.IsNullOrEmpty(profile.Class) ? "Knight" : profile.Class;
            if (classText != null) classText.text = className;
            ApplyClassArt(className);
            if (guildText != null)
                guildText.text = string.IsNullOrWhiteSpace(profile.Guild) ? "No Guild" : profile.Guild;
            if (titleText != null)
                titleText.text = string.IsNullOrWhiteSpace(profile.Title) ? "No Title" : profile.Title;

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

        public void OpenNameChangePanel()
        {
            if (nameChangePanel != null)
            {
                nameChangePanel.transform.SetAsLastSibling();
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

        public void OpenAvatarSelection()
        {
            if (avatarSelectionPanel == null)
                avatarSelectionPanel = GetComponentInChildren<UIAvatarSelectionPanel>(true);

            if (avatarSelectionPanel != null)
            {
                int myProfileId = MysticJourney.Core.Services.GameStateService.Instance.PlayerProfileId;
                if (myProfileId <= 0)
                    Debug.LogWarning("[UIFriendProfilePanel] Cannot edit avatar: local profile ID is not ready.");

                string currentAvatar = _currentProfile != null && !string.IsNullOrEmpty(_currentProfile.AvatarUrl)
                    ? _currentProfile.AvatarUrl
                    : "avatar_1";
                avatarSelectionPanel.OpenPanel(myProfileId, currentAvatar, this);
            }
            else
            {
                Debug.LogWarning("[UIFriendProfilePanel] Cannot open avatar editor: AvatarSelectionPanel is missing.");
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
                friendsCountText.text = "...";

            FriendApi.GetFriendList(friends =>
            {
                if (friendsCountText != null)
                    friendsCountText.text = (friends?.Count ?? 0).ToString();
            }, err =>
            {
                Debug.LogWarning($"Failed to load friends count: {err.Message}");
                if (friendsCountText != null)
                    friendsCountText.text = "N/A";
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

                BindAchievementRow(item.transform, achievement, owned);

                var button = item.GetComponent<Button>() ?? item.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    var capturedAchievement = achievement;
                    var capturedOwnedAchievement = owned;
                    button.onClick.RemoveAllListeners();
                    // Achievement rewards are useful before unlock as well as
                    // after unlock, so every catalog row must open its detail.
                    // The owned record is still passed through to render the
                    // locked/unlocked state.
                    button.interactable = true;
                    button.onClick.AddListener(() => SelectAchievement(capturedAchievement, capturedOwnedAchievement));
                }
            }

            if (noAchievementText != null)
                noAchievementText.SetActive(totalItems == 0);

            if (achievementScrollRect != null)
            {
                achievementScrollRect.verticalNormalizedPosition = 1f;
            }

            UpdatePaginationUI(totalPages);
        }

        // Row layout mới (AchievementItem.prefab): mỗi dữ liệu một widget riêng thay vì nhồi
        // hết vào một label rich-text.
        private void BindAchievementRow(Transform row, AchievementResponse achievement, PlayerAchievementResponse owned)
        {
            bool isOwned = owned != null;
            int required = Mathf.Max(1, achievement.RequiredValue);
            int progress = Mathf.Clamp(owned?.Progress ?? 0, 0, required);
            bool isUnlocked = isOwned && (owned.IsCompleted || owned.Progress >= achievement.RequiredValue);
            float ratio = isUnlocked ? 1f : (float)progress / required;

            SetRowText(row, "TitleText", achievement.Name);
            SetRowText(row, "Description", achievement.Description);
            SetRowText(row, "PercentText", $"{Mathf.RoundToInt(ratio * 100f)}%");

            var fill = row.Find("ProgressBar/ProgressFill")?.GetComponent<Image>();
            if (fill != null) fill.fillAmount = ratio;

            var badge = row.Find("Type")?.GetComponent<Image>();
            if (badge != null)
            {
                var badgeSprite = GetRarityBadge(achievement.Point);
                badge.sprite = badgeSprite;
                badge.enabled = badgeSprite != null;
            }

            var icon = row.Find("IconBg/Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                var sprite = string.IsNullOrEmpty(achievement.IconUrl)
                    ? null
                    : Resources.Load<Sprite>($"Icons/Titles/{achievement.IconUrl}");
                if (sprite != null) icon.sprite = sprite;
                icon.color = isUnlocked ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }

            // Khoá = ổ khoá trên icon, không dim cả row nữa: row bị CanvasGroup alpha 0.35 thì
            // đọc không nổi chữ mà cũng chẳng biết vì sao.
            row.Find("IconBg/LockBg")?.gameObject.SetActive(!isUnlocked);
            row.Find("IconBg/LockIcon")?.gameObject.SetActive(!isUnlocked);
        }

        private void SetRowText(Transform row, string childName, string value)
        {
            var text = row.Find(childName)?.GetComponent<TMP_Text>();
            if (text != null) text.text = value ?? string.Empty;
        }

        private Sprite GetRarityBadge(int point)
        {
            return GetRarityTier(point) switch
            {
                0 => commonBadgeIcon,
                1 => uncommonBadgeIcon,
                2 => rareBadgeIcon,
                _ => legendaryBadgeIcon
            };
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

            // --- Reward: Icon Gold + Icon Gem ---
            decimal rewardGold = achievement.RewardGold > 0
                ? achievement.RewardGold
                : ownedAchievement?.RewardGold ?? 0;
            int rewardGem = achievement.RewardGem > 0
                ? achievement.RewardGem
                : ownedAchievement?.RewardGem ?? 0;

            if (goldAmountText != null)
                goldAmountText.text = rewardGold.ToString("0");
            if (gemAmountText != null)
                gemAmountText.text = rewardGem.ToString();

            // --- Reward Item: InventorySlot_Prefab ---
            PopulateItemSlots(achievement);

            // --- Buff: Icon + Number (InventorySlot_Prefab) ---
            PopulateBuffSlots(achievement);

            bool isUnlocked = ownedAchievement != null && (ownedAchievement.IsCompleted || ownedAchievement.Progress >= achievement.RequiredValue);

            var detailSprite = string.IsNullOrEmpty(achievement.IconUrl)
                ? null
                : Resources.Load<Sprite>($"Icons/Titles/{achievement.IconUrl}");
            if (detailSprite != null && achievementDetailIconImage != null)
            {
                achievementDetailIconImage.sprite = detailSprite;
                achievementDetailIconImage.enabled = true;
                achievementDetailIconImage.color = isUnlocked
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.55f);
            }
            else
            {
                // IconUrl rỗng hoặc không có file trong Resources → thử URL tuyệt đối.
                ApplyAchievementIcon(achievement.IconUrl);
            }

            if (achievementDetailBadgeText != null)
            {
                achievementDetailBadgeText.text = GetRarityLabel(achievement.Point);
                achievementDetailBadgeText.color = GetRarityColor(achievement.Point);
            }
        }

        private void PopulateItemSlots(AchievementResponse achievement)
        {
            ClearContainer(itemSlotContainer);

            if (itemSlotContainer == null || inventorySlotPrefab == null)
                return;

            if (achievement.RewardItemId.HasValue && achievement.RewardItemId.Value > 0 && achievement.RewardQuantity > 0)
            {
                var slotGo = Instantiate(inventorySlotPrefab, itemSlotContainer);
                slotGo.transform.localScale = Vector3.one;
                var slot = slotGo.GetComponent<UIBaseItemSlot>();
                if (slot != null)
                {
                    string itemName = !string.IsNullOrEmpty(achievement.RewardItemName) ? achievement.RewardItemName : "Item";
                    Sprite itemIcon = null;
                    // Try to load item icon from Resources
                    if (!string.IsNullOrEmpty(achievement.RewardItemName))
                    {
                        itemIcon = Resources.Load<Sprite>($"Item/{achievement.RewardItemName}")
                                ?? Resources.Load<Sprite>($"Icons/Items/{achievement.RewardItemName}");
                    }
                    string amountText = achievement.RewardQuantity > 1 ? $"x{achievement.RewardQuantity}" : "";
                    slot.SetupCustom(itemName, amountText, itemIcon);
                }
            }
        }

        private void PopulateBuffSlots(AchievementResponse achievement)
        {
            ClearContainer(buffContainer);

            if (buffContainer == null)
                return;

            string buffDesc = !string.IsNullOrEmpty(achievement.BuffDescription)
                ? achievement.BuffDescription
                : GetBuffDescription(achievement.Point, achievement.Type);

            // Remove "Buff: " prefix and " | Type: xxx" suffix if present
            if (buffDesc.StartsWith("Buff: "))
                buffDesc = buffDesc.Substring(6);
            int pipeIdx = buffDesc.IndexOf(" | ");
            if (pipeIdx >= 0)
                buffDesc = buffDesc.Substring(0, pipeIdx);

            GameObject prefabToUse = buffSlotPrefab != null ? buffSlotPrefab : inventorySlotPrefab;
            if (prefabToUse == null) return;

            // Parse "+1 ATK, +2 DEF, +1 HP, +1 Crit" → individual stat entries
            string[] parts = buffDesc.Split(',');
            foreach (string raw in parts)
            {
                string part = raw.Trim();
                if (string.IsNullOrEmpty(part)) continue;

                // Extract stat name and value, e.g. "+2 ATK" → statName="ATK", statValue="+2"
                string statName = "";
                string statValue = "";
                int spaceIdx = part.IndexOf(' ');
                if (spaceIdx > 0)
                {
                    statValue = part.Substring(0, spaceIdx);  // "+2"
                    statName = part.Substring(spaceIdx + 1);   // "ATK"
                }
                else
                {
                    statName = part;
                }

                var slotGo = Instantiate(prefabToUse, buffContainer);
                slotGo.transform.localScale = Vector3.one;

                Sprite statIcon = GetStatIcon(statName);

                var buffSlot = slotGo.GetComponent<UIBuffSlot>();
                if (buffSlot != null)
                {
                    buffSlot.Setup(part, statIcon);
                }
                else
                {
                    var baseSlot = slotGo.GetComponent<UIBaseItemSlot>();
                    if (baseSlot != null)
                    {
                        baseSlot.SetupCustom(statName, statValue, statIcon);
                    }
                }
            }
        }

        private Sprite GetStatIcon(string statName)
        {
            if (string.Equals(statName, "ATK", StringComparison.OrdinalIgnoreCase)) return atkStatIcon;
            if (string.Equals(statName, "DEF", StringComparison.OrdinalIgnoreCase)) return defStatIcon;
            if (string.Equals(statName, "HP", StringComparison.OrdinalIgnoreCase)) return hpStatIcon;
            if (string.Equals(statName, "CRIT", StringComparison.OrdinalIgnoreCase)) return critStatIcon;
            if (string.Equals(statName, "SPD", StringComparison.OrdinalIgnoreCase)) return spdStatIcon;
            return null;
        }

        private void ClearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Destroy(container.GetChild(i).gameObject);
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
            if (achievementDetailBadgeText != null) achievementDetailBadgeText.text = string.Empty;
            if (achievementDetailIconImage != null)
            {
                achievementDetailIconImage.sprite = null;
                achievementDetailIconImage.enabled = false;
            }

            // Clear reward icons
            if (goldAmountText != null) goldAmountText.text = "0";
            if (gemAmountText != null) gemAmountText.text = "0";
            ClearContainer(itemSlotContainer);
            ClearContainer(buffContainer);

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
            UIPopupBox.Show(transform, "Logout", "Log out of your account?",
                () => MysticJourney.Core.Services.SessionService.Logout());
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
