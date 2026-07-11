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
        [SerializeField] private TMP_Text guildText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text friendsCountText;
        [SerializeField] private Button closeButton;



        [Header("Achievement List")]
        [SerializeField] private GameObject achievementListPanel;
        [SerializeField] private Button achievementListCloseButton;
        [SerializeField] private ScrollRect achievementScrollRect;
        [SerializeField] private Transform achievementContent;
        [SerializeField] private GameObject achievementItemPrefab;
        [SerializeField] private TMP_Text achievementSummaryText;

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
        
        private void Start()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            if (viewAchievementListButton != null)
                viewAchievementListButton.onClick.AddListener(ShowAchievementListView);

            if (achievementListCloseButton != null)
                achievementListCloseButton.onClick.AddListener(() =>
                {
                    if (achievementListPanel != null)
                        achievementListPanel.SetActive(false);
                });

            if (achievementDetailCloseButton != null)
                achievementDetailCloseButton.onClick.AddListener(CloseAchievementPopup);

            if (achievementDetailPanel != null)
                achievementDetailPanel.SetActive(false);
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
            gameObject.SetActive(true);
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
            if (guildText != null) guildText.text = $"Guild: {profile.Guild}";
            if (titleText != null) titleText.text = $"Title: {profile.Title}";
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
                    achievementSummaryText.text = $"Achievements: {_ownedAchievementMap.Count}/{_achievementCatalog.Count}";

                CloseAchievementPopup();
            }, err =>
            {
                Debug.LogWarning($"Failed to load owned achievements: {err.Message}");
                _ownedAchievementMap.Clear();
                PopulateAchievementList();
                if (achievementSummaryText != null)
                    achievementSummaryText.text = $"Achievements: 0/{_achievementCatalog.Count}";
                ClearAchievementDetail();
            });
        }

        private void PopulateAchievementList()
        {
            ClearAchievementList();

            if (achievementContent == null || achievementItemPrefab == null)
                return;

            foreach (var achievement in _achievementCatalog)
            {
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
                string statusLabel = isOwned ? "Unlocked" : "Locked";
                string displayText = $"<color={rarityHex}>{achievement.Name}</color>\n<size=70%>{rarityLabel}</size>\n<size=60%>{statusLabel}</size>";

                var tmpText = item.GetComponentInChildren<TMP_Text>(true);
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

                var image = item.GetComponent<Image>();
                if (image != null)
                {
                    image.color = isOwned ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.55f);
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

            if (achievementDetailBuffText != null)
            {
                achievementDetailBuffText.text = GetBuffDescription(achievement.Point, achievement.Type);
            }

            if (achievementDetailBadgeText != null)
            {
                achievementDetailBadgeText.text = GetRarityLabel(achievement.Point);
                achievementDetailBadgeText.color = GetRarityColor(achievement.Point);
            }

            ApplyAchievementIcon(achievement.IconUrl);
        }

        public void ShowAchievementListView()
        {
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
    }
}
