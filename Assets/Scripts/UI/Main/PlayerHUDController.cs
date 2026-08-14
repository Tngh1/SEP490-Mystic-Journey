using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Services;

public class PlayerHUDController : MonoBehaviour
{
    public static PlayerHUDController Instance { get; private set; }

    [Header("UI Reference Cache")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image expBarImage;
    [SerializeField] private TMP_Text expText;
    [SerializeField] private Image hpBarImage;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private float hpFillAnimDuration = 0.4f;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text gemText;

    [Header("Resource Change Animation")]
    [SerializeField] private TMP_FontAsset resourceChangeFont;
    [SerializeField, Min(0.2f)] private float resourceChangeDuration = 1.15f;
    [SerializeField, Min(20f)] private float resourceChangeRiseDistance = 68f;
    [SerializeField, Min(18f)] private float resourceChangeFontSize = 34f;

    [SerializeField] private TMP_Text corruptionText;
    [SerializeField] private Image corruptionBarImage;
    [SerializeField] private Image avatarImage;

    [Header("HUD Buttons")]
    [SerializeField] private GameObject settingsButtonObj;
    [SerializeField] private GameObject pauseButtonObj;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private TMP_Text levelUpPointsText;
    [SerializeField] private UILevelUpPanel levelUpPanel;

    [Header("HUD Groups")]
    [SerializeField] private GameObject nonCombatActionGroup;
    [SerializeField] private GameObject dungeonSpecificGroup;
    [SerializeField] private GameObject partyRosterContainer;

    [Header("Level-Gated Buttons")]
    [SerializeField] private GameObject chatButtonObj;
    [SerializeField] private GameObject friendButtonObj;
    [SerializeField] private GameObject dailyButtonObj;
    [SerializeField] private GameObject mailButtonObj;
    [SerializeField] private GameObject gachaButtonObj;
    [SerializeField] private GameObject shopButtonObj;
    [SerializeField] private GameObject guildButtonObj;
    [SerializeField] private GameObject bestiaryButtonObj;
    [SerializeField] private GameObject skillsButtonObj;

    [Header("Death Popup")]
    [SerializeField] private GameObject deathPopupPanel;
    [SerializeField] private Button btnAgain;
    [SerializeField] private Button btnQuit;
    [SerializeField] private Button btnRespawn;
    [SerializeField] private TMP_Text deathTitleText;
    [SerializeField] private TMP_Text deathSubtitleText;
    [SerializeField, HideInInspector] private TMP_FontAsset deathTitleFont;
    [SerializeField, HideInInspector] private TMP_FontAsset deathBodyFont;
    [SerializeField, HideInInspector] private Sprite deathPanelSprite;
    [SerializeField, HideInInspector] private Sprite deathSkullSprite;
    [SerializeField, HideInInspector] private Sprite deathPrimaryButtonSprite;
    [SerializeField, HideInInspector] private Sprite deathSecondaryButtonSprite;

    [Header("Colors")]
    [SerializeField] private Color expBarColor = new Color(0.35f, 0.78f, 0.98f); // Light Sky Blue
    [SerializeField] private Color highHealthColor = new Color(0.298f, 0.686f, 0.314f);  // #4CAF50
    [SerializeField] private Color mediumHealthColor = new Color(1f, 0.92f, 0.23f);       // #FFEB3B

    [SerializeField] private Color hpEmptyColor = new Color32(46, 31, 25, 255);
    [SerializeField] private Color lowHealthColor = new Color(0.956f, 0.263f, 0.212f);    // #F44336


    private Coroutine _updateLoopCoroutine;
    private bool _isRefreshing;
    private bool _isCurrencyRefreshing;
    private bool _profileRefreshQueued;
    private bool _currencyRefreshQueued;

    private int _lastHp = -1;
    private int _lastMaxHp = -1;
    private bool _isHpInitialized = false;
    private Coroutine _hpFillCoroutine;
    private Vector3 _hpBarOriginalScale = Vector3.one;
    private Transform _hpBarContainer;
    private readonly List<GameObject> _resourceDeltaPopups = new List<GameObject>();

    /// <summary>
    /// Cached currency balance — updated every time the HUD receives a fresh balance
    /// from the API. Used by UIConfirmPurchase to cap "Max" quantity to what the
    /// player can actually afford without making a separate API call.
    /// </summary>
    public static decimal CachedGold { get; private set; } = -1m;
    public static decimal CachedGems { get; private set; } = -1m;

    public int CurrentHp => _lastHp >= 0 ? _lastHp : (PlayerEntity.Instance != null ? PlayerEntity.Instance.CurrentHealth : 0);
    public int MaxHp => _lastMaxHp > 0 ? _lastMaxHp : (PlayerEntity.Instance != null ? PlayerEntity.Instance.MaxHealth : 0);

    // MenuButton (Toggle) trong Left: bấm để hiện/ẩn các nút còn lại cho gọn màn hình.
    // Dùng CanvasGroup trên Left để bật/tắt cả cụm — KHÔNG đụng SetActive của các nút, vì
    // visibility từng nút do 2 hệ level-gate độc lập quản (ApplyLevelGating +
    // MainFeatureUnlockRuntime). CanvasGroup là lớp phủ riêng: menu đóng ẩn cả cụm, menu
    // mở hiện đúng những nút đã đủ level. MenuButton có CanvasGroup ignoreParentGroups
    // riêng nên luôn hiện/bấm được.
    private bool _menuOpen;
    private bool _menuWired;
    private CanvasGroup _leftGroup;
    private GameObject _menuOpenIcon;  // Icon (menu) — hiện khi menu ĐANG đóng
    private GameObject _menuCloseIcon; // CloseIcon (X) — hiện khi menu ĐANG mở

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        FindHUDReferences();
        FindDeathPanelReferences();
    }

    private float _hudEnableTime = 0f;

    private void OnEnable()
    {
        _hudEnableTime = Time.unscaledTime;
        _isHpInitialized = false;
        ResetHpTransientEffects();
        StartHUDLoop();
        if (levelUpButton != null)
        {
            levelUpButton.onClick.AddListener(OnLevelUpButtonClicked);
        }
        PlayerEntity.OnHealthChanged += HandleHealthChanged;
        WorldRuntimeEvents.QuestsChanged -= OnQuestsOrCurrencyChanged;
        WorldRuntimeEvents.QuestsChanged += OnQuestsOrCurrencyChanged;
        WorldRuntimeEvents.CurrencyChanged -= OnQuestsOrCurrencyChanged;
        WorldRuntimeEvents.CurrencyChanged += OnQuestsOrCurrencyChanged;

        // Subscribe to NetworkPlayer death event
        if (NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.OnDied += ShowDeathPopup;
        }
        NetworkPlayer.OnAnyReadyStateChanged += UpdateDeathPopupState;

    }

    private void OnDisable()
    {
        StopHUDLoop();
        ResetHpTransientEffects();
        ClearResourceDeltaPopups();
        if (levelUpButton != null)
        {
            levelUpButton.onClick.RemoveListener(OnLevelUpButtonClicked);
        }
        PlayerEntity.OnHealthChanged -= HandleHealthChanged;
        WorldRuntimeEvents.QuestsChanged -= OnQuestsOrCurrencyChanged;
        WorldRuntimeEvents.CurrencyChanged -= OnQuestsOrCurrencyChanged;

        // Unsubscribe from NetworkPlayer death event
        if (NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.OnDied -= ShowDeathPopup;
        }
        NetworkPlayer.OnAnyReadyStateChanged -= UpdateDeathPopupState;

    }

    private void OnQuestsOrCurrencyChanged()
    {
        UpdateQuestPointers();
        RefreshHUD();
    }

    private void OnLevelUpButtonClicked()
    {
        if (levelUpPanel == null)
        {
            levelUpPanel = UnityEngine.Object.FindFirstObjectByType<UILevelUpPanel>(FindObjectsInactive.Include);
        }

        if (levelUpPanel != null)
        {
            levelUpPanel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[PlayerHUDController] UILevelUpPanel is null and not found in scene!");
        }
    }

    private Transform FindChildRecursive(Transform parent, string exactName)
    {
        if (parent.name == exactName) return parent;
        foreach (Transform child in parent)
        {
            var result = FindChildRecursive(child, exactName);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>
    /// Resolve a button in the left action column. It lives under
    /// "NonCombatActionGroup/Left"; a plain "Left/..." path returns null and fails
    /// SILENTLY (no hover effect, no reference), so fall back to a name search.
    /// </summary>
    private Transform FindLeft(string buttonName)
        => transform.Find("NonCombatActionGroup/Left/" + buttonName)
           ?? transform.Find("Left/" + buttonName)
           ?? FindChildRecursive(transform, buttonName);

    public void FindHUDReferences()
    {
        if (playerNameText == null) playerNameText = transform.Find("TopBar/Button/PlayerNameText")?.GetComponent<TMP_Text>();
        if (levelText == null) levelText = transform.Find("TopBar/Button/Avatar/Level/LevelText")?.GetComponent<TMP_Text>();
        if (avatarImage == null) avatarImage = transform.Find("TopBar/Button/Avatar")?.GetComponent<Image>();
        if (expBarImage == null)
        {
            expBarImage = transform.Find("TopBar/Button/ExpBar/ExpFill")?.GetComponent<Image>()
                       ?? transform.Find("TopBar/Button/Avatar/ExpBar/ExpFill")?.GetComponent<Image>()
                       ?? FindChildRecursive(transform, "ExpFill")?.GetComponent<Image>();
        }
        if (expText == null)
        {
            expText = transform.Find("TopBar/Button/ExpBar/ExpNumber")?.GetComponent<TMP_Text>()
                   ?? transform.Find("TopBar/Button/Avatar/ExpBar/ExpNumber")?.GetComponent<TMP_Text>()
                   ?? FindChildRecursive(transform, "ExpNumber")?.GetComponent<TMP_Text>()
                   ?? FindChildRecursive(transform, "ExpText")?.GetComponent<TMP_Text>();
        }
        if (hpBarImage == null) hpBarImage = transform.Find("TopBar/Button/HPBar/HPFill")?.GetComponent<Image>();

        // fillAmount only applies to Filled images; force it so a Simple-typed
        // sprite in the scene doesn't silently render the bar permanently full.
        MakeHorizontalFill(expBarImage);
        MakeHorizontalFill(hpBarImage);
        SetupHpEffects();
        if (hpText == null) hpText = transform.Find("TopBar/Button/HPBar/HPNumber")?.GetComponent<TMP_Text>();
        if (energyText == null)
        {
            energyText = transform.Find("TopBar/Center_Resources/EnergyBox/EnergyText")?.GetComponent<TMP_Text>();
        }
        if (goldText == null)
        {
            goldText = transform.Find("TopBar/Center_Resources/GoldBox/GoldText")?.GetComponent<TMP_Text>()
                    ?? transform.Find("TopBar/Center_Resources/GoldBox")?.GetComponentInChildren<TMP_Text>()
                    ?? transform.Find("TopBar/Center_Resources/CoinBox/CoinText")?.GetComponent<TMP_Text>()
                    ?? transform.Find("TopBar/Center_Resources/CoinBox")?.GetComponentInChildren<TMP_Text>();
        }

        if (gemText == null)
        {
            gemText = transform.Find("TopBar/Center_Resources/GemBox/GemText")?.GetComponent<TMP_Text>()
                   ?? transform.Find("TopBar/Center_Resources/GemBox")?.GetComponentInChildren<TMP_Text>()
                   ?? transform.Find("TopBar/Center_Resources/GemsBox/GemsText")?.GetComponent<TMP_Text>()
                   ?? transform.Find("TopBar/Center_Resources/GemsBox")?.GetComponentInChildren<TMP_Text>()
                   ?? transform.Find("TopBar/Center_Resources/DiamondBox/DiamondText")?.GetComponent<TMP_Text>()
                   ?? transform.Find("TopBar/Center_Resources/DiamondBox")?.GetComponentInChildren<TMP_Text>()
                   ?? transform.Find("TopBar/Center_Resources/Gem/GemText")?.GetComponent<TMP_Text>()
                   ?? transform.Find("TopBar/Center_Resources/Gem")?.GetComponentInChildren<TMP_Text>();
        }
        if (corruptionText == null)
        {
            corruptionText = transform.Find("Corruption/CorruptionNumber")?.GetComponent<TMP_Text>()
                          ?? transform.Find("TopBar/Center_Resources/CorruptionBox/CorruptionText")?.GetComponent<TMP_Text>();
        }

        if (corruptionBarImage == null)
        {
            corruptionBarImage = transform.Find("Corruption/CorruptionBar/CorruptionFill")?.GetComponent<Image>()
                              ?? transform.Find("TopBar/Center_Resources/CorruptionBox/CorruptionFill")?.GetComponent<Image>();
        }

        if (levelUpButton == null)
        {
            var lb = FindChildRecursive(transform, "LevelUpButton");
            if (lb != null)
            {
                levelUpButton = lb.GetComponent<Button>();
                if (levelUpButton == null) levelUpButton = lb.gameObject.AddComponent<Button>();
                
                var img = lb.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                
                lb.SetAsLastSibling();
            }
        }
        if (levelUpPointsText == null && levelUpButton != null)
        {
            levelUpPointsText = levelUpButton.GetComponentInChildren<TMP_Text>();
        }

        MakeHorizontalFill(corruptionBarImage);

        if (settingsButtonObj == null)
        {
            var btn = transform.Find("TopBar/Right_Buttons/SettingButton");
            if (btn != null) settingsButtonObj = btn.gameObject;
        }

        if (pauseButtonObj == null)
        {
            var btn = transform.Find("TopBar/Right_Buttons/PauseButton");
            if (btn != null) pauseButtonObj = btn.gameObject;
        }

        if (chatButtonObj == null)
        {
            var btn = transform.Find("ChatButton");
            if (btn != null) chatButtonObj = btn.gameObject;
        }
        if (friendButtonObj == null)
        {
            var btn = FindLeft("FriendButton");
            if (btn != null) friendButtonObj = btn.gameObject;
        }
        if (dailyButtonObj == null)
        {
            var btn = FindLeft("DailyButton");
            if (btn != null) dailyButtonObj = btn.gameObject;
        }
        if (mailButtonObj == null)
        {
            var btn = transform.Find("TopBar/Right_Buttons/MailButton");
            if (btn != null) mailButtonObj = btn.gameObject;
        }
        if (gachaButtonObj == null)
        {
            var btn = FindLeft("GachaButton");
            if (btn != null) gachaButtonObj = btn.gameObject;
        }
        if (shopButtonObj == null)
        {
            var btn = FindLeft("ShopButton");
            if (btn != null) shopButtonObj = btn.gameObject;
        }
        if (guildButtonObj == null)
        {
            var btn = FindLeft("GuildButton");
            if (btn != null) guildButtonObj = btn.gameObject;
        }
        if (bestiaryButtonObj == null)
        {
            var btn = FindLeft("BestiaryButton");
            if (btn != null) bestiaryButtonObj = btn.gameObject;
        }
        if (skillsButtonObj == null)
        {
            var btn = transform.Find("BottomCenter/Skills/SkillButton")
                   ?? transform.Find("BottomCenter/Skills")
                   ?? transform.Find("Skills");
            if (btn != null) skillsButtonObj = btn.gameObject;
        }

        // Same hover-scale transition the party panel uses on its Start/Ready buttons.
        AddHoverEffect(FindLeft("DailyButton"));
        AddHoverEffect(FindLeft("GachaButton"));
        AddHoverEffect(FindLeft("ShopButton"));
        AddHoverEffect(FindLeft("FriendButton"));
        AddHoverEffect(FindLeft("GuildButton"));
        AddHoverEffect(FindLeft("BestiaryButton"));
        AddHoverEffect(FindLeft("InventoryButton"));
        AddHoverEffect(transform.Find("ChatButton"));
        AddHoverEffect(transform.Find("BottomCenter/Skills/SkillButton"));
        AddHoverEffect(transform.Find("TopBar/Right_Buttons/MailButton"));
        AddHoverEffect(transform.Find("TopBar/Right_Buttons/SettingButton"));

        WireMenuButton();

        ConfigureResourceText(energyText);
        ConfigureResourceText(goldText);
        ConfigureResourceText(gemText);

        if (nonCombatActionGroup == null)
        {
            var grp = transform.Find("NonCombatActionGroup") ?? transform.Find("Left");
            if (grp != null) nonCombatActionGroup = grp.gameObject;
        }
        
        if (dungeonSpecificGroup == null)
        {
            var grp = transform.Find("DungeonSpecificGroup");
            if (grp != null) dungeonSpecificGroup = grp.gameObject;
        }

        if (partyRosterContainer == null)
        {
            var grp = transform.Find("PartyRosterContainer");
            if (grp != null) partyRosterContainer = grp.gameObject;
        }

        // Đảm bảo trạng thái nút bấm đúng với map hiện tại khi vừa vào game
        bool inDungeon = false;
        if (DungeonManager.Instance != null)
        {
            inDungeon = DungeonManager.Instance.IsInDungeon;
        }
        ToggleDungeonMode(inDungeon);
    }

    public void ToggleDungeonMode(bool isInDungeon)
    {
        if (settingsButtonObj != null) settingsButtonObj.SetActive(!isInDungeon);
        if (pauseButtonObj != null) pauseButtonObj.SetActive(isInDungeon);

        if (nonCombatActionGroup != null) nonCombatActionGroup.SetActive(!isInDungeon);
        if (dungeonSpecificGroup != null) dungeonSpecificGroup.SetActive(isInDungeon);

        // Danh sách HP/avatar của đồng đội chỉ có nghĩa trong dungeon. Container này được lưu
        // m_IsActive: 0 trong Main.unity và trước đây KHÔNG có code nào bật nó, nên
        // UIDungeonPartyRoster.OnEnable chưa từng chạy và party không bao giờ hiện.
        if (partyRosterContainer != null) partyRosterContainer.SetActive(isInDungeon);
    }

    public void StartHUDLoop()
    {
        StopHUDLoop();
        _updateLoopCoroutine = StartCoroutine(UpdateHUDLoop());
    }

    public void StopHUDLoop()
    {
        if (_updateLoopCoroutine != null)
        {
            StopCoroutine(_updateLoopCoroutine);
            _updateLoopCoroutine = null;
        }
    }

    private IEnumerator UpdateHUDLoop()
    {
        while (true)
        {
            RefreshHUD();
            yield return new WaitForSeconds(15.0f);
        }
    }

    private void HandleHealthChanged(int currentHp, int maxHp)
    {
        FindHUDReferences();
        UpdateStatsUI(currentHp, maxHp);
    }

    public void ForceRefreshHUD()
    {
        RefreshHUD();
    }

    public void RefreshHUD()
    {
        RefreshProfile();
        RefreshCurrencyBalance();
    }

    private void RefreshProfile()
    {
        if (_isRefreshing)
        {
            _profileRefreshQueued = true;
            return;
        }

        _isRefreshing = true;

        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                // Update WorldState Level so other parts of the game are aware
                WorldState.PlayerLevel = profile.Level;
                WorldState.PlayerName = profile.DisplayName ?? profile.AccountEmail;
                GameStateService.Instance.CorruptionLevel = profile.CorruptionLevel;

                UpdateProfileUI(profile);
                CompleteProfileRefresh();
            },
            error =>
            {
                Debug.LogWarning($"[PlayerHUDController] Failed to refresh profile: {error.Message}");
                CompleteProfileRefresh();
            }
        );
    }

    private void CompleteProfileRefresh()
    {
        _isRefreshing = false;
        if (!_profileRefreshQueued) return;

        _profileRefreshQueued = false;
        RefreshProfile();
    }

    private void EnsureFilledImageMode(Image img)
    {
        if (img == null) return;
        if (img.type != Image.Type.Filled)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    public void ApplyCorruption(float corruptionLevel)
    {
        FindHUDReferences();
        if (corruptionText != null)
        {
            corruptionText.text = $"{Mathf.RoundToInt(corruptionLevel)}/100";
        }
        if (corruptionBarImage != null)
        {
            EnsureFilledImageMode(corruptionBarImage);
            corruptionBarImage.fillAmount = Mathf.Clamp01(corruptionLevel / 100f);
        }
    }

    public void ApplyHealth(int currentHp, int maxHp)
    {
        FindHUDReferences();
        UpdateStatsUI(currentHp, maxHp);
    }

    public void ApplyEnergy(int currentEnergy, int maxEnergy)
    {
        FindHUDReferences();
        UpdateEnergyUI(currentEnergy, maxEnergy);
    }

    public void ApplyStats(PlayerStatsResponse stats)
    {
        if (stats == null)
            return;

        FindHUDReferences();
        UpdateStatsUI(stats);
    }

    public void RefreshCurrencyBalance()
    {
        if (_isCurrencyRefreshing)
        {
            _currencyRefreshQueued = true;
            return;
        }

        _isCurrencyRefreshing = true;

        CurrencyApi.Instance.GetMyBalance(
            balance =>
            {
                ApplyCurrencyBalance(balance);
                CompleteCurrencyRefresh();
            },
            error =>
            {
                Debug.LogWarning($"[PlayerHUDController] Failed to refresh currency balance: {error.Message}");
                CompleteCurrencyRefresh();
            }
        );
    }

    private void CompleteCurrencyRefresh()
    {
        _isCurrencyRefreshing = false;
        if (!_currencyRefreshQueued) return;

        _currencyRefreshQueued = false;
        RefreshCurrencyBalance();
    }

    public void ApplyCurrencyBalance(CurrencyBalanceResponse balance)
    {
        if (balance == null)
            return;

        FindHUDReferences();
        UpdateCurrencyUI(balance.Gold, balance.Gems);
    }

    /// <summary>
    /// Đổi avatar trên HUD ngay lập tức. Không có hàm này thì avatar chỉ đổi ở vòng lặp
    /// RefreshHUD kế tiếp — tức người chơi phải chờ tới 15 giây mới thấy ảnh mới.
    /// </summary>
    public void ApplyAvatar(string avatarUrl)
    {
        // Also publish it to the networked avatar so party members see the right picture
        // in the in-dungeon roster — a proxy cannot fetch another player's profile.
        NetworkPlayer.PublishLocalAvatar(avatarUrl);

        FindHUDReferences();
        if (avatarImage == null) return;

        var sprite = NetworkPlayer.ResolveAvatarSprite(avatarUrl);
        if (sprite != null)
            avatarImage.sprite = sprite;
    }

    private int _lastKnownLevel = -1;

    private void UpdateProfileUI(PlayerProfileResponse profile)
    {
        if (_lastKnownLevel != -1 && profile.Level > _lastKnownLevel)
        {
            if (levelUpPanel != null && !levelUpPanel.gameObject.activeInHierarchy)
            {
                levelUpPanel.gameObject.SetActive(true);
            }
        }
        _lastKnownLevel = profile.Level;

        if (playerNameText != null)
        {
            playerNameText.text = profile.DisplayName ?? profile.AccountEmail;
        }

        if (levelText != null)
        {
            levelText.text = profile.Level.ToString();
        }

        // Apply level-gating for buttons
        ApplyLevelGating(profile.Level);

        if (levelUpButton != null)
        {
            levelUpButton.gameObject.SetActive(profile.AvailableStatPoints > 0);
            if (levelUpPointsText != null)
            {
                levelUpPointsText.text = profile.AvailableStatPoints.ToString();
            }
        }

        UpdateEnergyUI(profile.Energy, profile.MaxEnergy);

        if (corruptionText != null)
        {
            corruptionText.text = $"{Mathf.RoundToInt(profile.CorruptionLevel)}/100";
        }

        if (corruptionBarImage != null)
        {
            EnsureFilledImageMode(corruptionBarImage);
            corruptionBarImage.fillAmount = Mathf.Clamp01(profile.CorruptionLevel / 100f);
        }

        // NOT gated on avatarImage: ApplyAvatar also publishes the avatar to the network so
        // party members can draw it in the dungeon roster. Gating the call on a HUD Image
        // reference meant that whenever transform.Find("TopBar/Button/Avatar") missed, the
        // network publish never ran either, WorldState.AvatarUrl stayed empty, and every
        // proxy silently fell back to avatar_1. ApplyAvatar null-checks the Image itself.
        ApplyAvatar(profile.AvatarUrl);

        UpdateCurrencyUI(profile.Gold, profile.Gems);

        if (expBarImage != null || expText != null)
        {
            int level = profile.Level;
            int totalExp = profile.ExperiencePoints;

            // profile.ExperiencePoints là tổng lũy kế từ backend, không reset khi lên level,
            // nên phải trừ mốc EXP của level hiện tại mới ra đúng phần dư sau khi lên cấp
            // (VD: lên Level 4 với tổng 314 EXP, mốc Level 4 là 300 -> dư 14, không phải 314).
            // Hai mốc tổng EXP, khớp với PlayerProfile.RequiredTotalExperienceForLevel ở backend:
            // (level - 1) * 100 cho level hiện tại, level * 100 cho level kế tiếp.
            int currentLevelFloor = (level - 1) * 100;
            int nextLevelFloor = level * 100;

            int expIntoLevel = Mathf.Max(0, totalExp - currentLevelFloor);

            // Thanh EXP đo phần dư trong level, nên mẫu số là khoảng cách giữa hai mốc
            // (100 EXP), KHÔNG phải mốc tổng lũy kế (VD: Level 5 hiện 14/100, không phải 14/500).
            int targetExp = nextLevelFloor - currentLevelFloor;
            if (targetExp <= 0) targetExp = 100;

            float expRatio = (float)expIntoLevel / targetExp;
            if (level >= 100)
            {
                expRatio = 1f;
                if (expText != null) expText.text = "EXP: MAX";
            }
            else
            {
                if (expText != null) expText.text = $"EXP: {expIntoLevel}/{targetExp}";
            }

            if (expBarImage != null)
            {
                EnsureFilledImageMode(expBarImage);
                expBarImage.fillAmount = Mathf.Clamp01(expRatio);
            }
        }
    }

    private void UpdateStatsUI(PlayerStatsResponse stats)
    {
        if (stats == null) return;
        UpdateStatsUI(stats.CurrentHp, stats.MaxHp);
    }

    private void UpdateStatsUI(int currentHp, int maxHp)
    {

        ConfigureHpBackground();
        if (maxHp <= 0) return;

        float targetRatio = Mathf.Clamp01((float)currentHp / (float)maxHp);
        // Tính previousFill dựa trên _lastHp & _lastMaxHp trước đó để đảm bảo luôn chênh lệch mốc chuẩn
        float previousFill = (_lastMaxHp > 0 && _lastHp >= 0 && _isHpInitialized) ? Mathf.Clamp01((float)_lastHp / (float)_lastMaxHp) : targetRatio;

        // Guard: 2.5s đầu tiên sau khi Login/bật HUD là thời gian đồng bộ dữ liệu ban đầu từ Server.
        bool isGracePeriod = (Time.unscaledTime - _hudEnableTime) < 2.5f;
        bool isDamageHit = _isHpInitialized && !isGracePeriod && (currentHp < _lastHp);

        if (_isHpInitialized && !isGracePeriod)
        {
            if (isDamageHit)
            {
                TriggerDamagePulseEffect(); // Chỉ co nảy ngọn thanh HP ở bên phải, không dùng bất kỳ Hào quang viền đỏ nào
            }
        }
        else
        {
            _isHpInitialized = true;
        }

        _lastHp    = currentHp;
        _lastMaxHp = maxHp;

        if (hpBarImage != null)
        {
            if (_hpFillCoroutine != null) StopCoroutine(_hpFillCoroutine);
            _hpFillCoroutine = StartCoroutine(AnimateHpFill(targetRatio, previousFill, isDamageHit, isGracePeriod));
        }

        if (hpText != null)
        {
            hpText.text = currentHp + " / " + maxHp;
        }
    }

    [SerializeField] private Image hpDamageCatchupImage;

    private void SetupHpEffects()
    {
        if (hpBarImage == null) return;

        if (_hpBarContainer == null)
        {
            _hpBarContainer = hpBarImage.transform.parent;
            if (_hpBarContainer != null && _hpBarOriginalScale == Vector3.one)
            {
                _hpBarOriginalScale = _hpBarContainer.localScale;
            }
        }

        // Tạo 2nd Layer: Lớp Máu Đuổi Sát Thương (Khớp 100% hình dáng & vị trí nội bộ của hpBarImage)
        if (hpDamageCatchupImage == null && hpBarImage != null && hpBarImage.transform.parent != null)
        {
            Transform parent = hpBarImage.transform.parent;
            var catchupTr = parent.Find("HPDamageCatchup");
            if (catchupTr != null)
            {
                hpDamageCatchupImage = catchupTr.GetComponent<Image>();
            }
            else
            {
                GameObject catchupObj = new GameObject("HPDamageCatchup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                catchupObj.transform.SetParent(parent, false);
                
                // Đặt ngay phía sau hpBarImage (Thấp hơn 1 bậc sibling)
                int hpBarIndex = hpBarImage.transform.GetSiblingIndex();
                catchupObj.transform.SetSiblingIndex(Mathf.Max(0, hpBarIndex));

                hpDamageCatchupImage = catchupObj.GetComponent<Image>();
            }
        }

        if (hpDamageCatchupImage != null && hpBarImage != null)
        {
            RectTransform barRt = hpBarImage.GetComponent<RectTransform>();
            RectTransform catchupRt = hpDamageCatchupImage.GetComponent<RectTransform>();
            if (barRt != null && catchupRt != null)
            {
                catchupRt.anchorMin = barRt.anchorMin;
                catchupRt.anchorMax = barRt.anchorMax;
                // Thụt lùi 4px bên phải và 2px trên dưới để dải máu trắng nằm gọn 100% trong lòng khung HP
                catchupRt.offsetMin = new Vector2(barRt.offsetMin.x + 2f, barRt.offsetMin.y + 2f);
                catchupRt.offsetMax = new Vector2(barRt.offsetMax.x - 4f, barRt.offsetMax.y - 2f);
                catchupRt.pivot     = barRt.pivot;
            }

            hpDamageCatchupImage.sprite = GetSolidWhiteSprite();
            hpDamageCatchupImage.type = Image.Type.Filled;
            hpDamageCatchupImage.fillMethod = hpBarImage.fillMethod;
            hpDamageCatchupImage.fillOrigin = hpBarImage.fillOrigin;
            hpDamageCatchupImage.raycastTarget = false;
            hpDamageCatchupImage.color = new Color(0.55f, 0.08f, 0.06f, 0.85f);

            hpDamageCatchupImage.gameObject.SetActive(false);
            MakeHorizontalFill(hpDamageCatchupImage);
        }

    }

    private void ResetHpTransientEffects()
    {
        if (_hpScalePulseCoroutine != null)
        {
            StopCoroutine(_hpScalePulseCoroutine);
            _hpScalePulseCoroutine = null;
        }

        SetupHpEffects();

        if (hpDamageCatchupImage != null)
        {
            hpDamageCatchupImage.gameObject.SetActive(false);
        }

        if (_hpBarContainer != null)
        {
            _hpBarContainer.localScale = _hpBarOriginalScale;
        }
    }

    private void ConfigureHpBackground()
    {
        if (hpBarImage == null || hpBarImage.transform.parent == null) return;

        Image background = hpBarImage.transform.parent.GetComponent<Image>();
        if (background != null)
        {
            background.color = hpEmptyColor;
            background.raycastTarget = false;
        }
    }


    private Coroutine _hpScalePulseCoroutine;

    public void TriggerDamagePulseEffect()
    {
        if (hpBarImage == null) return;
        SetupHpEffects();

        if (_hpScalePulseCoroutine != null) StopCoroutine(_hpScalePulseCoroutine);
        _hpScalePulseCoroutine = StartCoroutine(DamagePulseRoutine());
    }

    private IEnumerator DamagePulseRoutine()
    {
        if (hpBarImage == null) yield break;

        float duration = 0.35f;
        float elapsed = 0f;

        // Ép Pivot về góc bên trái (0.0, 0.5)
        // -> Phía bên trái dính liền Avatar CỐ ĐỊNH 100%, chỉ nảy co rút nhẹ ở ngọn đầu bên phải thanh HP
        RectTransform barRt = hpBarImage.GetComponent<RectTransform>();
        RectTransform catchupRt = hpDamageCatchupImage != null ? hpDamageCatchupImage.GetComponent<RectTransform>() : null;

        Vector2 origPivot = barRt != null ? barRt.pivot : new Vector2(0f, 0.5f);
        if (barRt != null) barRt.pivot = new Vector2(0f, 0.5f);
        if (catchupRt != null) catchupRt.pivot = new Vector2(0f, 0.5f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float sin = Mathf.Sin(t * Mathf.PI);

            // Co nảy 8% ở ngọn đầu bên phải
            float scaleX = 1f - (sin * 0.08f);

            if (barRt != null) barRt.localScale = new Vector3(scaleX, 1f, 1f);
            if (catchupRt != null) catchupRt.localScale = new Vector3(scaleX, 1f, 1f);

            yield return null;
        }

        if (barRt != null)
        {
            barRt.localScale = Vector3.one;
            barRt.pivot = origPivot;
        }
        if (catchupRt != null)
        {
            catchupRt.localScale = Vector3.one;
            catchupRt.pivot = origPivot;
        }
    }

    private IEnumerator AnimateHpFill(float targetFill, float previousFill, bool isDamageHit, bool isGracePeriod)
    {
        if (hpBarImage == null) yield break;
        SetupHpEffects();

        if (isGracePeriod || !_isHpInitialized)
        {
            hpBarImage.fillAmount = targetFill;
            if (hpDamageCatchupImage != null)
            {
                hpDamageCatchupImage.fillAmount = targetFill;
                hpDamageCatchupImage.gameObject.SetActive(false);
            }
            yield break;
        }

        if (isDamageHit || previousFill > targetFill + 0.005f)
        {
            if (hpDamageCatchupImage != null)
            {
                hpDamageCatchupImage.fillAmount = previousFill;
                hpDamageCatchupImage.gameObject.SetActive(true);
            }

            hpBarImage.fillAmount = targetFill;

            if (hpDamageCatchupImage != null)
            {
                yield return new WaitForSeconds(0.05f);

                float catchupStart = hpDamageCatchupImage.fillAmount;
                const float duration = 0.35f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    hpDamageCatchupImage.fillAmount = Mathf.Lerp(catchupStart, targetFill, t);
                    yield return null;
                }

                hpDamageCatchupImage.fillAmount = targetFill;
                hpDamageCatchupImage.gameObject.SetActive(false);
            }
        }
        else
        {
            if (hpDamageCatchupImage != null)
            {
                hpDamageCatchupImage.fillAmount = targetFill;
                hpDamageCatchupImage.gameObject.SetActive(false);
            }

            float startFill = hpBarImage.fillAmount;
            float duration = Mathf.Max(0.1f, hpFillAnimDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                hpBarImage.fillAmount = Mathf.Lerp(startFill, targetFill, t);
                yield return null;
            }

            hpBarImage.fillAmount = targetFill;
        }
    }

    private decimal _lastGold = -1m;
    private decimal _lastGems = -1m;
    private int _lastEnergy = -1;

    private void UpdateEnergyUI(int currentEnergy, int maxEnergy)
    {
        if (energyText == null)
            return;

        if (_lastEnergy >= 0 && currentEnergy != _lastEnergy)
        {
            int delta = currentEnergy - _lastEnergy;
            Color energyColor = new Color(0.08f, 0.98f, 0.44f, 1.0f);
            ShowResourceDelta(energyText, delta, energyColor);

            if (delta > 0)
                TriggerResourceGlowEffect(energyText, energyColor, "EnergyGlowAura");
        }

        _lastEnergy = currentEnergy;
        energyText.text = currentEnergy + "/" + maxEnergy;
    }

    private void UpdateCurrencyUI(decimal gold, decimal gems)
    {
        if (goldText == null || gemText == null)
            FindHUDReferences();

        if (_lastGold >= 0m && gold != _lastGold && goldText != null)
        {
            decimal delta = gold - _lastGold;
            Color goldColor = new Color(1.00f, 0.84f, 0.15f, 1.0f);
            ShowResourceDelta(goldText, delta, goldColor);

            if (delta > 0m)
                TriggerResourceGlowEffect(goldText, goldColor, "GoldGlowAura");
        }

        if (_lastGems >= 0m && gems != _lastGems && gemText != null)
        {
            decimal delta = gems - _lastGems;
            Color gemColor = new Color(0.00f, 0.90f, 1.00f, 1.0f);
            ShowResourceDelta(gemText, delta, gemColor);

            if (delta > 0m)
                TriggerResourceGlowEffect(gemText, gemColor, "GemGlowAura");
        }

        _lastGold = gold;
        _lastGems = gems;
        CachedGold = gold;
        CachedGems = gems;

        if (goldText != null)
        {
            goldText.text = FormatCurrencyAmount(gold);
        }

        if (gemText != null)
        {
            gemText.text = FormatCurrencyAmount(gems);
        }
    }

    private void ShowResourceDelta(TMP_Text targetText, decimal delta, Color gainColor)
    {
        if (targetText == null || delta == 0m || !isActiveAndEnabled)
            return;

        Transform container = targetText.transform.parent != null
            ? targetText.transform.parent
            : targetText.transform;

        GameObject popupObject = new GameObject(
            targetText.name + "Delta",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        popupObject.layer = targetText.gameObject.layer;
        popupObject.transform.SetParent(container, false);
        popupObject.transform.SetAsLastSibling();

        RectTransform popupRect = popupObject.GetComponent<RectTransform>();
        popupRect.anchorMin = popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.sizeDelta = new Vector2(180f, 52f);

        float targetX = targetText.rectTransform != null
            ? targetText.rectTransform.anchoredPosition.x
            : 0f;
        Vector2 startPosition = new Vector2(targetX, 34f);
        popupRect.anchoredPosition = startPosition;

        TextMeshProUGUI popupText = popupObject.GetComponent<TextMeshProUGUI>();
        popupText.font = resourceChangeFont != null ? resourceChangeFont : targetText.font;
        popupText.fontSize = resourceChangeFontSize;
        popupText.fontStyle = FontStyles.Bold;
        popupText.alignment = TextAlignmentOptions.Center;
        popupText.textWrappingMode = TextWrappingModes.NoWrap;
        popupText.overflowMode = TextOverflowModes.Overflow;
        popupText.raycastTarget = false;
        popupText.outlineWidth = 0.16f;
        popupText.outlineColor = new Color32(30, 20, 14, 230);
        popupText.text = (delta > 0m ? "+" : "-") + FormatCurrencyAmount(System.Math.Abs(delta));
        popupText.color = delta > 0m ? gainColor : new Color(1f, 0.28f, 0.24f, 1f);

        _resourceDeltaPopups.Add(popupObject);
        StartCoroutine(ResourceDeltaRoutine(popupObject, popupRect, popupText, startPosition));
    }

    private IEnumerator ResourceDeltaRoutine(
        GameObject popupObject,
        RectTransform popupRect,
        TMP_Text popupText,
        Vector2 startPosition)
    {
        Color baseColor = popupText.color;
        float duration = Mathf.Max(0.2f, resourceChangeDuration);
        float elapsed = 0f;

        while (elapsed < duration && popupObject != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float enter = Mathf.Clamp01(t / 0.22f);
            float rise = Mathf.SmoothStep(0f, 1f, t);
            float fade = 1f - Mathf.SmoothStep(0.58f, 1f, t);

            popupRect.anchoredPosition = startPosition + Vector2.up * (resourceChangeRiseDistance * rise);
            float bounce = Mathf.Sin(enter * Mathf.PI) * 0.22f;
            popupRect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, enter) * (1f + bounce);
            popupText.color = new Color(baseColor.r, baseColor.g, baseColor.b, fade);

            yield return null;
        }

        if (popupObject != null)
        {
            _resourceDeltaPopups.Remove(popupObject);
            Destroy(popupObject);
        }
    }

    private void ClearResourceDeltaPopups()
    {
        for (int i = _resourceDeltaPopups.Count - 1; i >= 0; i--)
        {
            if (_resourceDeltaPopups[i] != null)
                Destroy(_resourceDeltaPopups[i]);
        }

        _resourceDeltaPopups.Clear();
    }

    private static Sprite _solidWhiteSprite;

    private static Sprite GetSolidWhiteSprite()
    {
        if (_solidWhiteSprite != null) return _solidWhiteSprite;

        int sz = 64;
        Texture2D tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] cols = new Color32[sz * sz];
        float cornerRadius = 6f;

        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float dx = 0f;
                float dy = 0f;

                if (x < cornerRadius) dx = cornerRadius - x;
                else if (x > sz - 1 - cornerRadius) dx = x - (sz - 1 - cornerRadius);

                if (y < cornerRadius) dy = cornerRadius - y;
                else if (y > sz - 1 - cornerRadius) dy = y - (sz - 1 - cornerRadius);

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = dist > cornerRadius ? 0f : 1f;

                cols[y * sz + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels32(cols);
        tex.Apply();

        _solidWhiteSprite = Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f), sz);
        return _solidWhiteSprite;
    }

    private static Sprite _hudSoftAuraSprite;

    private static Sprite GetSoftAuraSprite()
    {
        if (_hudSoftAuraSprite != null) return _hudSoftAuraSprite;

        int w = 256;
        int h = 96;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[w * h];
        float borderWidth = 40f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(borderWidth - x, x - (w - 1 - borderWidth)));
                float dy = Mathf.Max(0f, Mathf.Max(borderWidth - y, y - (h - 1 - borderWidth)));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Smoothstep + Exponential falloff cho viền aura loang nhẹ cực kỳ mềm mại kiểu gacha
                float norm = Mathf.Clamp01(dist / borderWidth);
                float alpha = Mathf.SmoothStep(1f, 0f, norm);
                alpha = Mathf.Pow(alpha, 1.6f);

                pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        _hudSoftAuraSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(40, 40, 40, 40));
        return _hudSoftAuraSprite;
    }

    private void TriggerResourceGlowEffect(TMP_Text targetText, Color auraColor, string glowName)
    {
        if (targetText == null) return;
        Transform container = targetText.transform.parent;
        if (container == null) container = targetText.transform;

        StartCoroutine(ResourceGlowRoutine(container, targetText, auraColor, glowName));
    }

    private IEnumerator ResourceGlowRoutine(Transform container, TMP_Text targetText, Color auraColor, string glowName)
    {
        Vector3 origScale = container.localScale;
        Color origTextColor = targetText.color;

        Image glowImg = null;
        Transform glowTr = container.Find(glowName);
        if (glowTr != null)
        {
            glowImg = glowTr.GetComponent<Image>();
        }
        else
        {
            GameObject glowObj = new GameObject(glowName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glowObj.transform.SetParent(container, false);
            glowObj.transform.SetAsFirstSibling();

            RectTransform rt = glowObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-16, -16); // Hào quang tỏa 16px xung quanh ô tài nguyên
            rt.offsetMax = new Vector2(16, 16);
            rt.anchoredPosition = Vector2.zero;

            glowImg = glowObj.GetComponent<Image>();
            glowImg.sprite = GetSoftAuraSprite();
            glowImg.type = Image.Type.Simple;
            glowImg.raycastTarget = false;
        }

        float duration = 0.70f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float sinPulse = Mathf.Sin(t * Mathf.PI);

            // Phóng to nhẹ 10% và nhấp nháy hào quang sinh động
            float scaleMultiplier = 1f + (sinPulse * 0.10f);
            container.localScale = origScale * scaleMultiplier;

            if (glowImg != null)
            {
                glowImg.color = new Color(auraColor.r, auraColor.g, auraColor.b, sinPulse * 0.95f);
            }

            targetText.color = Color.Lerp(origTextColor, auraColor, sinPulse * 0.75f);

            yield return null;
        }

        if (glowImg != null)
        {
            glowImg.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0f);
        }
        container.localScale = origScale;
        targetText.color = origTextColor;
    }

    private static void ConfigureResourceText(TMP_Text text)
    {
        if (text == null)
            return;

        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private static void MakeHorizontalFill(Image img)
    {
        if (img == null) return;
        img.enabled = true;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private void ApplyLevelGating(int playerLevel)
    {
        // RefreshHUD chạy mỗi 15s VÀ mỗi lần ăn exp/vàng/nhận thưởng — kể cả trong hầm ngục.
        // Mấy nút dưới đây nằm trong NonCombatActionGroup mà ToggleDungeonMode(true) đã ẩn,
        // nên SetActive(true) ở đây bật lại cụm nút/tab bên trái ngay giữa hầm ngục.
        // Ra khỏi hầm ngục, ToggleDungeonMode(false) + RefreshHUD kế tiếp sẽ gating lại đúng.
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
            return;

        // Toàn bộ HUD mở khi bước sang Chương 2 (AutumnPumpkin).
        // Cấp 3 là cấp người chơi đạt được khi VÀO Chương 2 theo đường cong exp
        // trong seed, nên đây chính là mốc "vừa qua Chương 1". Chương 1 là phần
        // hướng dẫn (Talk/Collect/EquipSkill), giữ HUD gọn để không rối người mới.
        //
        // Trước đây mọi nút mở ở cấp 10 — rơi vào giữa Chương 4, tức là đi gần hết
        // game mới có Shop/Gacha/Chat/Bestiary.
        bool unlocked = playerLevel >= 3;

        // Luôn mở: Mail
        if (mailButtonObj != null) mailButtonObj.SetActive(true);

        if (shopButtonObj != null) shopButtonObj.SetActive(unlocked);
        if (bestiaryButtonObj != null) bestiaryButtonObj.SetActive(unlocked);
        if (dailyButtonObj != null) dailyButtonObj.SetActive(unlocked);
        if (chatButtonObj != null) chatButtonObj.SetActive(unlocked);
        if (friendButtonObj != null) friendButtonObj.SetActive(unlocked);
        if (gachaButtonObj != null) gachaButtonObj.SetActive(unlocked);
        if (guildButtonObj != null) guildButtonObj.SetActive(unlocked);

        EnsureUnlockHighlight(shopButtonObj, unlocked);
        EnsureUnlockHighlight(bestiaryButtonObj, unlocked);
        EnsureUnlockHighlight(dailyButtonObj, unlocked);
        EnsureUnlockHighlight(chatButtonObj, unlocked);
        EnsureUnlockHighlight(friendButtonObj, unlocked);
        EnsureUnlockHighlight(gachaButtonObj, unlocked);
        EnsureUnlockHighlight(guildButtonObj, unlocked);
    }

    private static void AddHoverEffect(Transform t)
    {
        if (t == null) return;
        if (t.GetComponent<UIHoverScaleEffect>() == null)
            t.gameObject.AddComponent<UIHoverScaleEffect>();
    }

    private void WireMenuButton()
    {
        var menuTr = FindChildRecursive(transform, "MenuButton");
        if (menuTr == null) return;

        AddHoverEffect(menuTr);

        // Idempotent: FindHUDReferences có thể chạy lại, chỉ gắn listener 1 lần.
        if (_menuWired) return;
        _menuWired = true;

        var openIcon = menuTr.Find("Icon");
        if (openIcon != null) _menuOpenIcon = openIcon.gameObject;
        var closeIcon = menuTr.Find("CloseIcon");
        if (closeIcon != null) _menuCloseIcon = closeIcon.gameObject;

        var leftTr = menuTr.parent;
        var parentCg = leftTr.GetComponent<CanvasGroup>();
        if (parentCg != null)
        {
            parentCg.alpha = 1f;
            parentCg.interactable = true;
            parentCg.blocksRaycasts = true;
        }

        var toggle = menuTr.GetComponent<Toggle>();
        if (toggle != null)
        {
            toggle.isOn = _menuOpen;
            toggle.onValueChanged.AddListener(SetMenuOpen);
        }
        else
        {
            var btn = menuTr.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => SetMenuOpen(!_menuOpen));
            }
            else
            {
                var newBtn = menuTr.gameObject.AddComponent<Button>();
                newBtn.onClick.AddListener(() => SetMenuOpen(!_menuOpen));
            }
        }

        ApplyMenuVisibility();
    }

    private void SetMenuOpen(bool open)
    {
        _menuOpen = open;
        ApplyMenuVisibility();
    }

    private void ApplyMenuVisibility()
    {
        var leftTr = FindChildRecursive(transform, "Left");
        if (leftTr != null)
        {
            for (int i = 0; i < leftTr.childCount; i++)
            {
                var child = leftTr.GetChild(i);
                if (child.name == "MenuButton") continue;
                
                var cg = child.GetComponent<CanvasGroup>();
                if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
                
                cg.alpha = _menuOpen ? 1f : 0f;
                cg.interactable = _menuOpen;
                cg.blocksRaycasts = _menuOpen;
            }
        }

        // Đổi icon MenuButton: đóng -> icon menu, mở -> icon X (đóng).
        if (_menuOpenIcon != null) _menuOpenIcon.SetActive(!_menuOpen);
        if (_menuCloseIcon != null) _menuCloseIcon.SetActive(_menuOpen);
    }

    private static string FormatCurrencyAmount(decimal amount)
    {
        return amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
    }

    private void EnsureUnlockHighlight(GameObject obj, bool unlocked)
    {
        if (obj == null || !unlocked) return;
        
        string key = $"Feature_Clicked_{obj.name}";
        if (PlayerPrefs.GetInt(key, 0) == 1) return;

        var highlight = obj.GetComponent<MysticJourney.UI.Effects.UIHighlightPulse>();
        if (highlight == null) highlight = obj.AddComponent<MysticJourney.UI.Effects.UIHighlightPulse>();

        var btn = obj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => 
            {
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
                var h = obj.GetComponent<MysticJourney.UI.Effects.UIHighlightPulse>();
                if (h != null) Destroy(h);
            });
        }
    }

    private void EnsureQuestPointer(GameObject obj, bool add)
    {
        if (obj == null) return;
        var pointer = obj.GetComponentInChildren<MysticJourney.UI.Effects.UIQuestPointer>();
        if (add)
        {
            if (pointer == null) 
            {
                var go = new GameObject("QuestPointer");
                go.transform.SetParent(obj.transform, false);
                var text = go.AddComponent<TMPro.TextMeshProUGUI>();
                text.text = "!";
                text.color = Color.yellow;
                text.fontSize = 40;
                text.alignment = TextAlignmentOptions.Center;
                
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-10, -10);

                var effect = go.AddComponent<MysticJourney.UI.Effects.UIQuestPointer>();
                effect.moveAmount = 5f;
            }
        }
        else
        {
            if (pointer != null) Destroy(pointer.gameObject);
        }
    }

    private void UpdateQuestPointers()
    {
        var manager = QuestManager.Instance;
        if (manager == null) return;
        var quests = manager.GetMainQuests();
        if (quests == null) return;

        var active = MysticJourney.Core.Utilities.QuestUtils.PickPreferredQuest(quests);
        if (active == null || MysticJourney.Core.Utilities.QuestUtils.IsStatus(active, "Claimed"))
        {
            ClearAllQuestPointers();
            return;
        }

        // Chỉ nhắc nút UI khi quest đã ĐƯỢC NHẬN (InProgress). Quest NotStarted vẫn đang chờ
        // nói chuyện với NPC — nếu nhắc sớm thì ô Skill nảy lên ngay ở nhiệm vụ Talk trước đó.
        if (!MysticJourney.Core.Utilities.QuestUtils.IsStatus(active, "InProgress"))
        {
            ClearAllQuestPointers();
            return;
        }

        bool gacha = false, guild = false, shop = false, daily = false, skill = false;
        var objType = active.ObjectiveType?.ToLower() ?? "";
        
        if (objType == "gacha") gacha = true;
        if (objType == "guild") guild = true;
        if (objType == "shop" || objType == "buy") shop = true;
        if (objType == "achievement" || objType == "daily") daily = true;
        if (objType == "equipskill" || objType == "skill") skill = true;

        if (skillsButtonObj == null)
        {
            var btn = transform.Find("BottomCenter/Skills/SkillButton")
                   ?? transform.Find("BottomCenter/Skills")
                   ?? transform.Find("Skills");
            if (btn != null) skillsButtonObj = btn.gameObject;
        }

        EnsureQuestPointer(gachaButtonObj, gacha);
        EnsureQuestPointer(guildButtonObj, guild);
        EnsureQuestPointer(shopButtonObj, shop);
        EnsureQuestPointer(dailyButtonObj, daily);
        EnsureQuestPointer(skillsButtonObj, skill);
        EnsureHighlightPulse(skillsButtonObj, skill);
    }
    
    private void EnsureHighlightPulse(GameObject obj, bool add)
    {
        if (obj == null) return;
        var pulse = obj.GetComponent<MysticJourney.UI.Effects.UIHighlightPulse>();
        if (add)
        {
            if (pulse == null) obj.AddComponent<MysticJourney.UI.Effects.UIHighlightPulse>();
        }
        else
        {
            if (pulse != null) Destroy(pulse);
        }
    }

    private void ClearAllQuestPointers()
    {
        EnsureQuestPointer(gachaButtonObj, false);
        EnsureQuestPointer(guildButtonObj, false);
        EnsureQuestPointer(shopButtonObj, false);
        EnsureQuestPointer(dailyButtonObj, false);
        EnsureQuestPointer(skillsButtonObj, false);
        EnsureHighlightPulse(skillsButtonObj, false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // NetworkPlayer Local Subscription
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from NetworkPlayer.Spawned() when Local is set.
    /// Ensures the HUD subscribes to the local player's death event.
    /// </summary>
    public void SubscribeToLocalPlayer(NetworkPlayer localPlayer)
    {
        if (localPlayer == null) return;

        // Unsubscribe from old player if any
        if (NetworkPlayer.Local != null && NetworkPlayer.Local != localPlayer)
        {
            NetworkPlayer.Local.OnDied -= ShowDeathPopup;
        }

        // Subscribe to new player
        localPlayer.OnDied += ShowDeathPopup;
    }

    /// <summary>
    /// Call this when leaving a dungeon / disconnecting.
    /// </summary>
    public void UnsubscribeFromLocalPlayer()
    {
        if (NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.OnDied -= ShowDeathPopup;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Death Popup
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Show the death popup with Again (respawn) and Quit options.
    /// </summary>
    private bool _isDeathPopupShowing = false;
    
    public void ShowDeathPopup()
    {
        if (_isDeathPopupShowing) return;
        _isDeathPopupShowing = true;
        StartCoroutine(ShowDeathPopupCoroutine());
    }

    private GameObject _deathRedOverlay;
    private GameObject _worldDeathContent;
    private TextMeshProUGUI _deathTitleText;
    private TextMeshProUGUI _deathSubtitleText;
    private Button _deathPrimaryButton;
    private Button _deathSecondaryButton;
    private Button _deathRespawnButton;

    private void FindDeathPanelReferences()
    {
        if (deathPopupPanel == null)
            deathPopupPanel = transform.root.Find("DeathPanel")?.gameObject;

        if (deathPopupPanel == null) return;

        if (deathTitleText == null)
            deathTitleText = FindChildRecursive(deathPopupPanel.transform, "Title")?.GetComponent<TMP_Text>();
        if (deathSubtitleText == null)
            deathSubtitleText = FindChildRecursive(deathPopupPanel.transform, "Subtitle")?.GetComponent<TMP_Text>();
        if (btnAgain == null)
            btnAgain = FindChildRecursive(deathPopupPanel.transform, "AgainButton")?.GetComponent<Button>();
        if (btnQuit == null)
            btnQuit = FindChildRecursive(deathPopupPanel.transform, "QuitButton")?.GetComponent<Button>();
        if (btnRespawn == null)
            btnRespawn = FindChildRecursive(deathPopupPanel.transform, "RespawnButton")?.GetComponent<Button>();

        _deathRedOverlay = deathPopupPanel;
        _worldDeathContent = deathPopupPanel;
        _deathTitleText = deathTitleText as TextMeshProUGUI;
        _deathSubtitleText = deathSubtitleText as TextMeshProUGUI;
        _deathPrimaryButton = btnAgain;
        _deathSecondaryButton = btnQuit;
        _deathRespawnButton = btnRespawn;
        deathPopupPanel.SetActive(false);
    }

    private IEnumerator ShowDeathPopupCoroutine()
    {
        Debug.Log("[PlayerHUDController] Player died. Waiting for animation and fading red overlay...");

        if (_deathRedOverlay == null) FindDeathPanelReferences();
        if (_deathRedOverlay == null)
        {
            Debug.LogError("[PlayerHUDController] DeathPanel is not assigned in Main scene.");
            _isDeathPopupShowing = false;
            yield break;
        }

        bool inDungeon = DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon;
        PrepareDeathPanelForFade(inDungeon);

        _deathRedOverlay.SetActive(true);
        _deathRedOverlay.transform.SetAsLastSibling();

        // Fade in over 2 seconds
        float t = 0;
        var image = _deathRedOverlay.GetComponent<Image>();
        while (t < 2f)
        {
            t += Time.deltaTime;
            image.color = new Color(0.7f, 0f, 0f, (t / 2f) * 0.6f); // Semi-transparent red
            yield return null;
        }

        // Wait 1 more second before showing the popup
        yield return new WaitForSeconds(1f);

        if (inDungeon)
        {
            ShowDungeonDeathPopup();
        }
        else
        {
            ShowWorldDeathPopup();
        }
    }

    private void PrepareDeathPanelForFade(bool inDungeon)
    {
        // DeathPanel is also the red overlay. Activating it before the fade previously
        // exposed the scene's default RespawnButton for three seconds, then swapped to
        // Again/Quit after the dungeon check, which looked like two death popups.
        if (_deathTitleText != null)
            _deathTitleText.text = inDungeon ? "DEFEATED" : "YOU HAVE FALLEN";
        if (_deathSubtitleText != null)
            _deathSubtitleText.text = inDungeon
                ? "Stand with your party, or retreat from the darkness."
                : "The old gods have not claimed you yet.";

        if (_deathPrimaryButton != null)
            _deathPrimaryButton.gameObject.SetActive(false);
        if (_deathSecondaryButton != null)
            _deathSecondaryButton.gameObject.SetActive(false);
        if (_deathRespawnButton != null)
            _deathRespawnButton.gameObject.SetActive(false);
    }

    private void ShowWorldDeathPopup()
    {
        Debug.Log("[PlayerHUDController] Showing WORLD death popup...");
        ShowStyledDeathContent(false);
    }

    private void BuildDeathContent()
    {
        _worldDeathContent = new GameObject("StyledDeathContent", typeof(RectTransform));
        _worldDeathContent.transform.SetParent(_deathRedOverlay.transform, false);
        var contentRect = _worldDeathContent.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        var card = new GameObject("DeathCard", typeof(RectTransform), typeof(Image), typeof(Outline));
        card.transform.SetParent(_worldDeathContent.transform, false);
        var cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(680f, 470f);
        var cardImage = card.GetComponent<Image>();
        cardImage.sprite = deathPanelSprite;
        cardImage.type = Image.Type.Simple;
        cardImage.color = deathPanelSprite != null ? Color.white : new Color(0.08f, 0.045f, 0.04f, 0.98f);
        var cardOutline = card.GetComponent<Outline>();
        cardOutline.effectColor = new Color(0.12f, 0.015f, 0.01f, 1f);
        cardOutline.effectDistance = new Vector2(6f, -6f);

        if (deathSkullSprite != null)
        {
            var skull = new GameObject("Skull", typeof(RectTransform), typeof(Image));
            skull.transform.SetParent(card.transform, false);
            var skullRect = skull.GetComponent<RectTransform>();
            skullRect.anchorMin = skullRect.anchorMax = new Vector2(0.5f, 0.5f);
            skullRect.anchoredPosition = new Vector2(0f, 168f);
            skullRect.sizeDelta = new Vector2(112f, 112f);
            var skullImage = skull.GetComponent<Image>();
            skullImage.sprite = deathSkullSprite;
            skullImage.preserveAspect = true;
            skullImage.raycastTarget = false;
        }

        _deathTitleText = CreateDeathText("Title", card.transform, deathTitleFont,
            "YOU HAVE FALLEN", 66f, new Color(0.84f, 0.18f, 0.14f), new Vector2(0f, 92f), new Vector2(580f, 82f));
        _deathTitleText.fontStyle = FontStyles.Bold;
        _deathTitleText.outlineWidth = 0.2f;
        _deathTitleText.outlineColor = new Color32(34, 8, 7, 255);

        _deathSubtitleText = CreateDeathText("Subtitle", card.transform, deathBodyFont,
            "The old gods have not claimed you yet.", 31f, new Color(0.88f, 0.82f, 0.68f),
            new Vector2(0f, 25f), new Vector2(560f, 62f));

        var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(card.transform, false);
        var dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
        dividerRect.anchoredPosition = new Vector2(0f, -24f);
        dividerRect.sizeDelta = new Vector2(430f, 3f);
        divider.GetComponent<Image>().color = new Color(0.58f, 0.42f, 0.19f, 0.9f);

        _deathPrimaryButton = CreateDeathButton("RiseAgainButton", card.transform,
            deathPrimaryButtonSprite, "RISE AGAIN", new Vector2(0f, -116f));
        _deathSecondaryButton = CreateDeathButton("LeaveDungeonButton", card.transform,
            deathSecondaryButtonSprite, "LEAVE", new Vector2(125f, -116f));
    }

    private TextMeshProUGUI CreateDeathText(string objectName, Transform parent, TMP_FontAsset font,
        string value, float fontSize, Color color, Vector2 position, Vector2 size)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font != null ? font : playerNameText?.font;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(18f, fontSize * 0.65f);
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateDeathButton(string objectName, Transform parent, Sprite sprite, string label, Vector2 position)
    {
        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(230f, 82f);

        var image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : new Color(0.45f, 0.12f, 0.09f, 1f);

        var button = buttonObject.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.88f, 0.65f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        button.colors = colors;

        var labelText = CreateDeathText("Label", buttonObject.transform, deathBodyFont, label, 29f,
            Color.white, Vector2.zero, Vector2.zero);
        var labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);
        labelText.outlineWidth = 0.12f;
        labelText.outlineColor = new Color32(30, 12, 8, 255);

        if (buttonObject.GetComponent<UIHoverScaleEffect>() == null)
            buttonObject.AddComponent<UIHoverScaleEffect>();
        return button;
    }

    private void ShowStyledDeathContent(bool inDungeon)
    {
        if (_worldDeathContent == null) return;

        _worldDeathContent.SetActive(true);
        _deathTitleText.text = inDungeon ? "DEFEATED" : "YOU HAVE FALLEN";
        _deathSubtitleText.text = inDungeon
            ? "Stand with your party, or retreat from the darkness."
            : "The old gods have not claimed you yet.";

        _deathPrimaryButton.onClick.RemoveAllListeners();
        _deathSecondaryButton.onClick.RemoveAllListeners();
        if (_deathRespawnButton != null)
            _deathRespawnButton.onClick.RemoveAllListeners();

        _deathPrimaryButton.gameObject.SetActive(inDungeon);
        _deathSecondaryButton.gameObject.SetActive(inDungeon);
        if (_deathRespawnButton != null)
            _deathRespawnButton.gameObject.SetActive(!inDungeon);

        if (inDungeon)
        {
            deathPopupPanel = _worldDeathContent;
            btnAgain = _deathPrimaryButton;
            btnQuit = _deathSecondaryButton;
            btnAgain.onClick.AddListener(OnAgainClicked);
            btnQuit.onClick.AddListener(OnQuitClicked);
            UpdateDeathPopupState();
        }
        else
        {
            if (_deathRespawnButton != null)
                _deathRespawnButton.onClick.AddListener(OnWorldRespawnClicked);
            else
                Debug.LogError("[PlayerHUDController] RespawnButton is not assigned in DeathPanel.");
        }
    }

    private void OnWorldRespawnClicked()
    {
        Debug.Log("[PlayerHUDController] OnWorldRespawnClicked - respawning at map spawn point...");
        _isDeathPopupShowing = false;

        if (_deathRedOverlay != null)
        {
            _deathRedOverlay.SetActive(false);
        }

        Vector3 spawnPos = WorldState.LastPosition;
        
        // [FIX] Nếu Map có kịch bản Cập Bến Thuyền, ưu tiên hồi sinh người chơi ở vị trí cập bến (trên bờ)
        var boatArrival = UnityEngine.Object.FindFirstObjectByType<BoatAutoArrival>();
        if (boatArrival != null && boatArrival.shoreSpawnPoint != null)
        {
            spawnPos = boatArrival.shoreSpawnPoint.position;
        }
        else
        {
            var spawner = UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null && spawner.SpawnPoint != null)
            {
                spawnPos = spawner.SpawnPoint.position;
            }
            else
            {
                GameObject targetSpawnPoint = GameObject.FindGameObjectWithTag("PlayerSpawn") ?? GameObject.Find("PlayerSpawn") ?? GameObject.Find("SceneTransitionGoblinMine");
                if (targetSpawnPoint != null) spawnPos = targetSpawnPoint.transform.position;
            }
        }

        if (NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.RPC_WorldRespawn(spawnPos);
        }
        else if (PlayerEntity.Instance != null)
        {
            PlayerEntity.Instance.WorldRespawn(spawnPos);
        }
        else
        {
            Debug.LogWarning("[PlayerHUDController] Cannot respawn. Player not found.");
        }
    }

    public void HideDeathPopup()
    {
        _isDeathPopupShowing = false;
        if (deathPopupPanel != null)
        {
            if (MysticJourney.UI.UIPopup.Instance != null && deathPopupPanel == MysticJourney.UI.UIPopup.Instance.PopupContainer)
            {
                MysticJourney.UI.UIPopup.Instance.HidePopup();
            }
            else
            {
                deathPopupPanel.SetActive(false);
            }
        }
        if (_deathRedOverlay != null)
        {
            _deathRedOverlay.SetActive(false);
        }
    }

    private void ShowDungeonDeathPopup()
    {
        Debug.Log("[PlayerHUDController] Showing DUNGEON death popup...");

        if (deathPopupPanel == null || deathPopupPanel == _worldDeathContent)
        {
            ShowStyledDeathContent(true);
            return;
        }

        // Try to auto-find the death popup panel if not assigned
        if (deathPopupPanel == null)
        {
            deathPopupPanel = transform.Find("DeathPopup")?.gameObject;
            if (deathPopupPanel == null)
            {
                deathPopupPanel = GameObject.Find("DeathPopup");
            }
        }

        if (deathPopupPanel != null)
        {
            deathPopupPanel.SetActive(true);
            deathPopupPanel.transform.SetAsLastSibling();

            // Auto-wire buttons if not assigned
            if (btnAgain == null)
            {
                var btn = deathPopupPanel.transform.Find("AgainButton");
                if (btn != null) btnAgain = btn.GetComponent<Button>();
            }
            if (btnQuit == null)
            {
                var btn = deathPopupPanel.transform.Find("QuitButton");
                if (btn != null) btnQuit = btn.GetComponent<Button>();
            }

            if (btnAgain != null)
            {
                btnAgain.interactable = true;
                var txt = btnAgain.GetComponentInChildren<TMP_Text>();
                if (txt != null) txt.text = "Again";

                btnAgain.onClick.RemoveAllListeners();
                btnAgain.onClick.AddListener(OnAgainClicked);
            }
            if (btnQuit != null)
            {
                btnQuit.onClick.RemoveAllListeners();
                btnQuit.onClick.AddListener(OnQuitClicked);
            }
            
            UpdateDeathPopupState();
        }
        else
        {
            // Fallback to the shared designer-authored UIPopup if no custom death panel exists.
            Debug.Log("[PlayerHUDController] No DeathPopup found, using UIPopup fallback.");
            MysticJourney.UI.UIPopup.Instance.ShowConfirm(
                "YOU DIED",
                "You have been defeated in battle.",
                onConfirm: OnAgainClicked,
                onCancel: OnQuitClicked,
                confirmText: "Again",
                cancelText: "Quit",
                autoClose: false
            );
            
            deathPopupPanel = MysticJourney.UI.UIPopup.Instance.PopupContainer;
            btnAgain = MysticJourney.UI.UIPopup.Instance.BtnConfirm;
        }
    }

    /// <summary>
    /// Update the death popup "Again" button text based on ready states.
    /// </summary>
    private void UpdateDeathPopupState()
    {
        if (deathPopupPanel == null || !deathPopupPanel.activeInHierarchy) return;
        if (btnAgain == null) return;

        int readyCount = NetworkPlayer.All.Count(p => p.IsReadyToRestart);
        int totalCount = NetworkPlayer.All.Count;

        var txt = btnAgain.GetComponentInChildren<TMP_Text>();
        if (txt == null) return;

        if (readyCount > 0)
        {
            txt.text = $"Waiting... ({readyCount}/{totalCount})";
        }
        else
        {
            // The host clears every ready flag once the restart fires or a vote is
            // abandoned; without this the button stayed disabled on "Waiting..." forever.
            txt.text = "Again";
            btnAgain.interactable = true;
        }
    }

    /// <summary>
    /// Handle "Again" button click - respawn the player.
    /// </summary>
    private void OnAgainClicked()
    {
        Debug.Log("[PlayerHUDController] OnAgainClicked - requesting respawn...");

        // Disable button to prevent spam
        if (btnAgain != null)
        {
            btnAgain.interactable = false;
        }

        // Request ready to restart via NetworkPlayer
        if (NetworkPlayer.Local != null)
        {
            NetworkPlayer.Local.RPC_SetReadyToRestart();
        }
        else
        {
            Debug.Log("[PlayerHUDController] Single-player restart.");
            HideDeathPopup();
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.RestartDungeon();
            }
        }
    }

    /// <summary>
    /// Handle "Quit" button click - leave dungeon and return to world.
    /// </summary>
    private void OnQuitClicked()
    {
        Debug.Log("[PlayerHUDController] OnQuitClicked - leaving dungeon...");

        // Hide death popup
        HideDeathPopup();

        // DungeonManager sở hữu teardown Photon + scene; shutdown trước làm mất trạng thái
        // dungeon room và khiến pipeline ReturnToWorldMap bỏ qua bước migrate cần thiết.

        // Return to previous map via DungeonManager if in dungeon
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
        {
            if (PlayerEntity.Instance != null && PlayerEntity.Instance.CurrentHealth <= 0)
            {
                PlayerEntity.Instance.WorldRespawn(WorldState.LastPosition);
            }
            DungeonManager.Instance.ReturnToWorldMap();
        }
    }
}
