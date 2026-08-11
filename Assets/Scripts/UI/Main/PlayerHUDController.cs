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
    [SerializeField] private Image hpGlowImage;
    [SerializeField] private Color healGlowColor = new Color(0.96f, 0.42f, 0.52f, 0.40f); // Soft Rose-Ruby Glow - Gentle & Soothing Aura Tint
    [SerializeField] private float hpFillAnimDuration = 0.4f;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text gemText;
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

    [Header("Colors")]
    [SerializeField] private Color expBarColor = new Color(0.35f, 0.78f, 0.98f); // Light Sky Blue
    [SerializeField] private Color highHealthColor = new Color(0.298f, 0.686f, 0.314f);  // #4CAF50
    [SerializeField] private Color mediumHealthColor = new Color(1f, 0.92f, 0.23f);       // #FFEB3B
    [SerializeField] private Color lowHealthColor = new Color(0.956f, 0.263f, 0.212f);    // #F44336


    private Coroutine _updateLoopCoroutine;
    private bool _isRefreshing;
    private bool _isCurrencyRefreshing;

    private int _lastHp = -1;
    private int _lastMaxHp = -1;
    private bool _isHpInitialized = false;
    private Coroutine _hpFillCoroutine;
    private Coroutine _hpGlowCoroutine;
    private Vector3 _hpBarOriginalScale = Vector3.one;
    private Transform _hpBarContainer;

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
    }

    private float _hudEnableTime = 0f;

    private void OnEnable()
    {
        _hudEnableTime = Time.unscaledTime;
        _isHpInitialized = false;
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
        RefreshCurrencyBalance();
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
        SetupHpGlowImage();
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
        _isRefreshing = false;
        _isCurrencyRefreshing = false;
        RefreshHUD();
    }

    public void RefreshHUD()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        // Step 1: Refresh Profile (Level, Exp)
        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                // Update WorldState Level so other parts of the game are aware
                WorldState.PlayerLevel = profile.Level;
                WorldState.PlayerName = profile.DisplayName ?? profile.AccountEmail;
                GameStateService.Instance.CorruptionLevel = profile.CorruptionLevel;

                UpdateProfileUI(profile);
                _isRefreshing = false;
            },
            error =>
            {
                Debug.LogWarning($"[PlayerHUDController] Failed to refresh profile: {error.Message}");
                _isRefreshing = false;
            }
        );

        RefreshCurrencyBalance();
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

    public void ApplyStats(PlayerStatsResponse stats)
    {
        if (stats == null)
            return;

        FindHUDReferences();
        UpdateStatsUI(stats);
    }

    public void RefreshCurrencyBalance()
    {
        if (_isCurrencyRefreshing) return;
        _isCurrencyRefreshing = true;

        CurrencyApi.Instance.GetMyBalance(
            balance =>
            {
                ApplyCurrencyBalance(balance);
                _isCurrencyRefreshing = false;
            },
            error =>
            {
                Debug.LogWarning($"[PlayerHUDController] Failed to refresh currency balance: {error.Message}");
                _isCurrencyRefreshing = false;
            }
        );
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
            levelText.text = "Lv " + profile.Level;
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

        if (energyText != null)
        {
            if (_lastEnergy >= 0 && profile.Energy > _lastEnergy)
            {
                // Energy tăng -> Tỏa Hào Quang Xanh Lục Bảo (#14FA70) + Phóng to 10% êm mắt
                TriggerResourceGlowEffect(energyText, new Color(0.08f, 0.98f, 0.44f, 1.0f), "EnergyGlowAura");
            }
            _lastEnergy = profile.Energy;
            energyText.text = profile.Energy + "/" + profile.MaxEnergy;
        }

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
        if (maxHp <= 0) return;

        float targetRatio = Mathf.Clamp01((float)currentHp / (float)maxHp);
        // Tính previousFill dựa trên _lastHp & _lastMaxHp trước đó để đảm bảo luôn chênh lệch mốc chuẩn
        float previousFill = (_lastMaxHp > 0 && _lastHp >= 0 && _isHpInitialized) ? Mathf.Clamp01((float)_lastHp / (float)_lastMaxHp) : targetRatio;

        // Guard: 2.5s đầu tiên sau khi Login/bật HUD là thời gian đồng bộ dữ liệu ban đầu từ Server.
        bool isGracePeriod = (Time.unscaledTime - _hudEnableTime) < 2.5f;
        bool isDamageHit = _isHpInitialized && !isGracePeriod && (currentHp < _lastHp);
        bool isHeal = _isHpInitialized && !isGracePeriod && (currentHp > _lastHp);

        if (_isHpInitialized && !isGracePeriod)
        {
            if (isHeal)
            {
                TriggerHealGlowEffect();
            }
            else if (isDamageHit)
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

    private void SetupHpGlowImage()
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
            hpDamageCatchupImage.color = new Color(1.00f, 1.00f, 1.00f, 0.95f); // Màu TRẮNG TINH 100% rực rỡ nổi bật cực kỳ dễ nhìn
            MakeHorizontalFill(hpDamageCatchupImage);
        }

        // Tạo Hào Quang Tỏa Ra Từ Viền Khung UI (Soft Gacha-style Frame Aura Radiating Outward)
        if (hpGlowImage == null && _hpBarContainer != null)
        {
            var glowTr = _hpBarContainer.Find("HPFrameAura") ?? _hpBarContainer.Find("HPGlow");
            if (glowTr != null)
            {
                hpGlowImage = glowTr.GetComponent<Image>();
            }
            else
            {
                GameObject glowObj = new GameObject("HPFrameAura", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                glowObj.transform.SetParent(_hpBarContainer, false);
                glowObj.transform.SetAsFirstSibling();

                RectTransform rect = glowObj.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-16, -16);
                rect.offsetMax = new Vector2(16, 16);

                hpGlowImage = glowObj.GetComponent<Image>();
            }
        }

        if (hpGlowImage != null)
        {
            // Luôn dùng Sprite Hào Quang Loang Nhẹ Kiểu Gacha (GetSoftAuraSprite) để viền mượt mà, không bị thô
            hpGlowImage.sprite = GetSoftAuraSprite();
            hpGlowImage.type = Image.Type.Sliced;
            hpGlowImage.raycastTarget = false;
            if (hpGlowImage.color.a <= 0.01f)
            {
                hpGlowImage.color = new Color(healGlowColor.r, healGlowColor.g, healGlowColor.b, 0f);
            }
        }
    }

    private Coroutine _hpScalePulseCoroutine;

    public void TriggerHealGlowEffect()
    {
        if (hpBarImage == null) return;
        SetupHpGlowImage();

        if (_hpGlowCoroutine != null) StopCoroutine(_hpGlowCoroutine);
        _hpGlowCoroutine = StartCoroutine(HealGlowRoutine());
    }

    public void TriggerDamagePulseEffect()
    {
        if (hpBarImage == null) return;
        SetupHpGlowImage();

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
        SetupHpGlowImage();

        // Nếu mới vào game / Login trong 2.5s đầu: Gán thẳng mốc HP chuẩn cho cả Máu Đỏ và Máu Trắng
        if (isGracePeriod || !_isHpInitialized)
        {
            hpBarImage.fillAmount = targetFill;
            if (hpDamageCatchupImage != null)
            {
                hpDamageCatchupImage.fillAmount = targetFill;
            }
            yield break;
        }

        // Nếu bị đánh mất HP (Damage hit): Máu Đỏ tụt ngay lập tức, Máu Truy Đổi phản ứng tức thì không bị trễ
        if (isDamageHit || previousFill > targetFill + 0.005f)
        {
            if (hpDamageCatchupImage != null)
            {
                hpDamageCatchupImage.fillAmount = previousFill; // Giữ mốc máu cũ 100% rõ ràng
                hpDamageCatchupImage.gameObject.SetActive(true);
            }

            hpBarImage.fillAmount = targetFill; // Máu chính tụt ngay lập tức

            if (hpDamageCatchupImage != null)
            {
                // Phản ứng tức thì (chỉ nghỉ 0.05s siêu mượt)
                yield return new WaitForSeconds(0.05f);

                float catchupStart = hpDamageCatchupImage.fillAmount;
                float duration = 0.35f;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    hpDamageCatchupImage.fillAmount = Mathf.Lerp(catchupStart, targetFill, t);
                    yield return null;
                }

                hpDamageCatchupImage.fillAmount = targetFill;
            }
        }
        else
        {
            // Nếu HP tăng (Hồi máu): Máu Truy Đổi mở rộng trước, Máu chính lướt mượt lên theo
            if (hpDamageCatchupImage != null)
            {
                hpDamageCatchupImage.fillAmount = targetFill;
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

    private IEnumerator HealGlowRoutine()
    {
        if (hpBarImage == null) yield break;

        float duration = 0.75f;
        float elapsed = 0f;

        RectTransform glowRt = hpGlowImage != null ? hpGlowImage.GetComponent<RectTransform>() : null;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / duration;
            float sinPulse = Mathf.Sin(normalizedTime * Mathf.PI);

            // Sóng Hào Quang Tỏa Rộng Mềm Mại Loang Nhẹ Kiểu Gacha (Soft Gacha Bloom Expansion)
            float glowAlpha = sinPulse * healGlowColor.a;
            float expandOffset = 16f + (sinPulse * 16f); // Tỏa mượt từ 16px ra ngoài 32px loang nhẹ mềm mại

            if (hpGlowImage != null)
            {
                hpGlowImage.color = new Color(healGlowColor.r, healGlowColor.g, healGlowColor.b, glowAlpha);
            }

            if (glowRt != null)
            {
                glowRt.offsetMin = new Vector2(-expandOffset, -expandOffset);
                glowRt.offsetMax = new Vector2(expandOffset, expandOffset);
            }

            yield return null;
        }

        if (hpGlowImage != null)
        {
            hpGlowImage.color = new Color(healGlowColor.r, healGlowColor.g, healGlowColor.b, 0f);
        }

        if (glowRt != null)
        {
            glowRt.offsetMin = new Vector2(-16f, -16f);
            glowRt.offsetMax = new Vector2(16f, 16f);
        }
    }

    private decimal _lastGold = -1m;
    private decimal _lastGems = -1m;
    private int _lastEnergy = -1;

    private void UpdateCurrencyUI(decimal gold, decimal gems)
    {
        if (_lastGold >= 0m && gold > _lastGold && goldText != null)
        {
            // Vàng tăng -> Tỏa Hào Quang Vàng Thần Thánh (#FFD700) + Phóng to 10% êm mắt
            TriggerResourceGlowEffect(goldText, new Color(1.00f, 0.84f, 0.15f, 1.0f), "GoldGlowAura");
        }

        if (_lastGems >= 0m && gems > _lastGems && gemText != null)
        {
            // Gem tăng -> Tỏa Hào Quang Kim Cương Cyan (#00E5FF) + Phóng to 10% êm mắt
            TriggerResourceGlowEffect(gemText, new Color(0.00f, 0.90f, 1.00f, 1.0f), "GemGlowAura");
        }

        _lastGold = gold;
        _lastGems = gems;
        CachedGold = gold;
        CachedGems = gems;

        if (goldText == null || gemText == null)
        {
            FindHUDReferences();
        }

        if (goldText != null)
        {
            goldText.text = FormatCurrencyAmount(gold);
        }

        if (gemText != null)
        {
            gemText.text = FormatCurrencyAmount(gems);
        }
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

    private IEnumerator ShowDeathPopupCoroutine()
    {
        Debug.Log("[PlayerHUDController] Player died. Waiting for animation and fading red overlay...");

        // Create red overlay
        if (_deathRedOverlay == null)
        {
            _deathRedOverlay = new GameObject("DeathRedOverlay");
            _deathRedOverlay.transform.SetParent(transform, false);
            var img = _deathRedOverlay.AddComponent<Image>();
            img.color = new Color(1f, 0f, 0f, 0f);
            
            var rect = _deathRedOverlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // --- World Death Content (Text & Button) ---
            _worldDeathContent = new GameObject("WorldDeathContent");
            _worldDeathContent.transform.SetParent(_deathRedOverlay.transform, false);
            var contentRect = _worldDeathContent.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // Title
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(_worldDeathContent.transform, false);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "THE LIGHT FADES...";
            titleText.color = new Color(1f, 0.2f, 0.2f, 1f);
            titleText.fontSize = 72;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontStyle = FontStyles.Bold;
            if (playerNameText != null) titleText.font = playerNameText.font;
            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.5f);
            titleRect.anchorMax = new Vector2(1, 0.5f);
            titleRect.anchoredPosition = new Vector2(0, 100);
            titleRect.sizeDelta = new Vector2(0, 100);

            // Subtitle
            var subObj = new GameObject("Subtitle");
            subObj.transform.SetParent(_worldDeathContent.transform, false);
            var subText = subObj.AddComponent<TextMeshProUGUI>();
            subText.text = "Your journey is not over yet.";
            subText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            subText.fontSize = 36;
            subText.alignment = TextAlignmentOptions.Center;
            if (playerNameText != null) subText.font = playerNameText.font;
            var subRect = subObj.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0, 0.5f);
            subRect.anchorMax = new Vector2(1, 0.5f);
            subRect.anchoredPosition = new Vector2(0, 30);
            subRect.sizeDelta = new Vector2(0, 50);

            // Respawn Button
            var btnObj = new GameObject("RespawnButton");
            btnObj.transform.SetParent(_worldDeathContent.transform, false);
            var btnImage = btnObj.AddComponent<Image>();
            btnImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            var btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(OnWorldRespawnClicked);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = new Vector2(0, -60);
            btnRect.sizeDelta = new Vector2(250, 70);

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "Respawn";
            btnText.color = Color.white;
            btnText.fontSize = 32;
            btnText.alignment = TextAlignmentOptions.Center;
            if (playerNameText != null) btnText.font = playerNameText.font;
            var btnTextRect = btnTextObj.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
        }
        
        if (_worldDeathContent != null) _worldDeathContent.SetActive(false);
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

        bool inDungeon = DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon;

        if (inDungeon)
        {
            ShowDungeonDeathPopup();
        }
        else
        {
            ShowWorldDeathPopup();
        }
    }

    private void ShowWorldDeathPopup()
    {
        Debug.Log("[PlayerHUDController] Showing WORLD death popup...");

        if (_worldDeathContent != null)
        {
            _worldDeathContent.SetActive(true);
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
            if (MysticJourney.UI.UIPopupManager.Instance != null && deathPopupPanel == MysticJourney.UI.UIPopupManager.Instance.PopupContainer)
            {
                MysticJourney.UI.UIPopupManager.Instance.HidePopup();
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
            // Fallback to UIPopupManager if no custom death panel exists
            Debug.Log("[PlayerHUDController] No DeathPopup found, using UIPopupManager fallback.");
            MysticJourney.UI.UIPopupManager.Instance.ShowConfirm(
                "YOU DIED",
                "You have been defeated in battle.",
                onConfirm: OnAgainClicked,
                onCancel: OnQuitClicked,
                confirmText: "Again",
                cancelText: "Quit",
                autoClose: false
            );
            
            deathPopupPanel = MysticJourney.UI.UIPopupManager.Instance.PopupContainer;
            btnAgain = MysticJourney.UI.UIPopupManager.Instance.BtnConfirm;
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

        // Disconnect and return to world
        var photon = PhotonManager.Instance;
        if (photon != null && photon.IsConnected)
        {
            photon.Shutdown(notify: true);
        }

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
