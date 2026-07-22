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

    private void OnEnable()
    {
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
        if (levelUpPanel != null)
        {
            levelUpPanel.gameObject.SetActive(true);
        }
    }

    public void FindHUDReferences()
    {
        if (playerNameText == null) playerNameText = transform.Find("TopBar/Button/PlayerNameText")?.GetComponent<TMP_Text>();
        if (levelText == null) levelText = transform.Find("TopBar/Button/Avatar/Level/LevelText")?.GetComponent<TMP_Text>();
        if (avatarImage == null) avatarImage = transform.Find("TopBar/Button/Avatar")?.GetComponent<Image>();
        if (expBarImage == null) expBarImage = transform.Find("TopBar/Button/ExpBar/ExpFill")?.GetComponent<Image>();
        if (expText == null) expText = transform.Find("TopBar/Button/ExpBar/ExpNumber")?.GetComponent<TMP_Text>();
        if (hpBarImage == null) hpBarImage = transform.Find("TopBar/Button/HPBar/HPFill")?.GetComponent<Image>();

        // fillAmount only applies to Filled images; force it so a Simple-typed
        // sprite in the scene doesn't silently render the bar permanently full.
        MakeHorizontalFill(expBarImage);
        MakeHorizontalFill(hpBarImage);
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
            var btn = transform.Find("Left/FriendButton");
            if (btn != null) friendButtonObj = btn.gameObject;
        }
        if (dailyButtonObj == null)
        {
            var btn = transform.Find("Left/DailyButton");
            if (btn != null) dailyButtonObj = btn.gameObject;
        }
        if (mailButtonObj == null)
        {
            var btn = transform.Find("TopBar/Right_Buttons/MailButton");
            if (btn != null) mailButtonObj = btn.gameObject;
        }
        if (gachaButtonObj == null)
        {
            var btn = transform.Find("Left/GachaButton");
            if (btn != null) gachaButtonObj = btn.gameObject;
        }
        if (shopButtonObj == null)
        {
            var btn = transform.Find("Left/ShopButton");
            if (btn != null) shopButtonObj = btn.gameObject;
        }
        if (guildButtonObj == null)
        {
            var btn = transform.Find("Left/GuildButton");
            if (btn != null) guildButtonObj = btn.gameObject;
        }
        if (bestiaryButtonObj == null)
        {
            var btn = transform.Find("Left/BestiaryButton");
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
        AddHoverEffect(transform.Find("Left/DailyButton"));
        AddHoverEffect(transform.Find("Left/GachaButton"));
        AddHoverEffect(transform.Find("Left/ShopButton"));
        AddHoverEffect(transform.Find("Left/FriendButton"));
        AddHoverEffect(transform.Find("Left/GuildButton"));
        AddHoverEffect(transform.Find("Left/BestiaryButton"));
        AddHoverEffect(transform.Find("Left/InventoryButton"));
        AddHoverEffect(transform.Find("ChatButton"));
        AddHoverEffect(transform.Find("BottomCenter/Skills/SkillButton"));
        AddHoverEffect(transform.Find("TopBar/Right_Buttons/MailButton"));
        AddHoverEffect(transform.Find("TopBar/Right_Buttons/SettingButton"));

        WireMenuButton();

        ConfigureResourceText(energyText);
        ConfigureResourceText(goldText);
        ConfigureResourceText(gemText);

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

    public void RefreshHUD()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        
        // Send heartbeat to keep status Online
        PlayerApi.Instance.SendHeartbeat();

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

        // Step 2: Character Stats (Current HP, Max HP) are now updated in real-time via PlayerEntity.OnHealthChanged event.
        // We no longer poll CharacterApi.Instance.GetMyStats here to save HTTP traffic.

        RefreshCurrencyBalance();
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

    private void UpdateProfileUI(PlayerProfileResponse profile)
    {
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
            energyText.text = profile.Energy + "/" + profile.MaxEnergy;
        }

        if (corruptionText != null)
        {
            corruptionText.text = $"{Mathf.RoundToInt(profile.CorruptionLevel)}/100";
        }

        if (corruptionBarImage != null)
        {
            corruptionBarImage.fillAmount = Mathf.Clamp01(profile.CorruptionLevel / 100f);
        }

        if (avatarImage != null)
        {
            string avatarUrl = string.IsNullOrEmpty(profile.AvatarUrl) ? "avatar_1" : profile.AvatarUrl;
            Sprite avatarSprite = Resources.Load<Sprite>($"Avatars/{avatarUrl}");
            if (avatarSprite != null)
            {
                avatarImage.sprite = avatarSprite;
            }
        }

        UpdateCurrencyUI(profile.Gold, profile.Gems);

        if (expBarImage != null)
        {
            // Experience required formula: (Level - 1) * 100
            int level = profile.Level;
            int totalExp = profile.ExperiencePoints;
            int baseExpForLevel = (level - 1) * 100;
            int currentExpInLevel = Mathf.Max(0, totalExp - baseExpForLevel);
            int requiredExpForNextLevel = 100; // Each level step needs 100 exp from the previous

            float expRatio = (float)currentExpInLevel / requiredExpForNextLevel;
            if (level >= 100)
            {
                expRatio = 1f;
                if (expText != null) expText.text = "EXP: MAX";
            }
            else
            {
                if (expText != null) expText.text = $"EXP: {currentExpInLevel}/{requiredExpForNextLevel}";
            }

            expBarImage.fillAmount = Mathf.Clamp01(expRatio);
        }
    }

    private void UpdateStatsUI(PlayerStatsResponse stats)
    {
        if (stats == null) return;
        UpdateStatsUI(stats.CurrentHp, stats.MaxHp);
    }

    private void UpdateStatsUI(int currentHp, int maxHp)
    {
        float hpRatio = maxHp > 0 ? (float)currentHp / maxHp : 0f;

        if (hpBarImage != null)
        {
            hpBarImage.fillAmount = Mathf.Clamp01(hpRatio);
        }

        if (hpText != null)
        {
            hpText.text = currentHp + " / " + maxHp;
        }
    }

    private void UpdateCurrencyUI(decimal gold, decimal gems)
    {
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
        bool level10 = playerLevel >= 10;

        // Always unlock: Mail
        if (mailButtonObj != null) mailButtonObj.SetActive(true);

        // Level 10 unlock: Daily (Achievement), Chat, Friend, Gacha, Shop, Guild, Bestiary
        if (dailyButtonObj != null) dailyButtonObj.SetActive(level10);
        if (chatButtonObj != null) chatButtonObj.SetActive(level10);
        if (friendButtonObj != null) friendButtonObj.SetActive(level10);
        if (gachaButtonObj != null) gachaButtonObj.SetActive(level10);
        if (shopButtonObj != null) shopButtonObj.SetActive(level10);
        if (guildButtonObj != null) guildButtonObj.SetActive(level10);
        if (bestiaryButtonObj != null) bestiaryButtonObj.SetActive(level10);

        EnsureUnlockHighlight(dailyButtonObj, level10);
        EnsureUnlockHighlight(chatButtonObj, level10);
        EnsureUnlockHighlight(friendButtonObj, level10);
        EnsureUnlockHighlight(gachaButtonObj, level10);
        EnsureUnlockHighlight(shopButtonObj, level10);
        EnsureUnlockHighlight(guildButtonObj, level10);
        EnsureUnlockHighlight(bestiaryButtonObj, level10);
    }

    private static void AddHoverEffect(Transform t)
    {
        if (t == null) return;
        if (t.GetComponent<UIHoverScaleEffect>() == null)
            t.gameObject.AddComponent<UIHoverScaleEffect>();
    }

    private void WireMenuButton()
    {
        var menuTr = transform.Find("Left/MenuButton");
        if (menuTr == null) return;

        AddHoverEffect(menuTr);

        // MenuButton phải luôn hiện & bấm được dù CanvasGroup của Left tắt cụm nút.
        var menuGroup = menuTr.GetComponent<CanvasGroup>();
        if (menuGroup == null) menuGroup = menuTr.gameObject.AddComponent<CanvasGroup>();
        menuGroup.ignoreParentGroups = true;
        menuGroup.alpha = 1f;
        menuGroup.interactable = true;
        menuGroup.blocksRaycasts = true;

        // Idempotent: FindHUDReferences có thể chạy lại, chỉ gắn listener 1 lần.
        if (_menuWired) return;
        _menuWired = true;

        var openIcon = menuTr.Find("Icon");
        if (openIcon != null) _menuOpenIcon = openIcon.gameObject;
        var closeIcon = menuTr.Find("CloseIcon");
        if (closeIcon != null) _menuCloseIcon = closeIcon.gameObject;

        var leftTr = menuTr.parent;
        _leftGroup = leftTr.GetComponent<CanvasGroup>();
        if (_leftGroup == null) _leftGroup = leftTr.gameObject.AddComponent<CanvasGroup>();

        var toggle = menuTr.GetComponent<Toggle>();
        if (toggle != null)
        {
            toggle.isOn = _menuOpen;
            toggle.onValueChanged.AddListener(SetMenuOpen);
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
        if (_leftGroup != null)
        {
            _leftGroup.alpha = _menuOpen ? 1f : 0f;
            _leftGroup.interactable = _menuOpen;
            _leftGroup.blocksRaycasts = _menuOpen;
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
    public void ShowDeathPopup()
    {
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

        if (_deathRedOverlay != null)
        {
            _deathRedOverlay.SetActive(false);
        }

        Vector3 spawnPos = WorldState.LastPosition;
        
        var spawner = UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>();
        if (spawner != null && spawner.SpawnPoint != null)
        {
            spawnPos = spawner.SpawnPoint.position;
        }
        else
        {
            GameObject targetSpawnPoint = GameObject.Find("PlayerSpawn") ?? GameObject.Find("SceneTransitionGoblinMine");
            if (targetSpawnPoint != null) spawnPos = targetSpawnPoint.transform.position;
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
        if (deathPopupPanel != null)
        {
            deathPopupPanel.SetActive(false);
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
                cancelText: "Quit"
            );
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

        if (readyCount > 0)
        {
            var txt = btnAgain.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = $"Waiting... ({readyCount}/{totalCount})";
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
            Debug.LogWarning("[PlayerHUDController] NetworkPlayer.Local is null, cannot respawn.");
        }
    }

    /// <summary>
    /// Handle "Quit" button click - leave dungeon and return to world.
    /// </summary>
    private void OnQuitClicked()
    {
        Debug.Log("[PlayerHUDController] OnQuitClicked - leaving dungeon...");

        // Hide death popup
        if (deathPopupPanel != null)
        {
            deathPopupPanel.SetActive(false);
        }
        if (_deathRedOverlay != null)
        {
            _deathRedOverlay.SetActive(false);
        }

        // Disconnect and return to world
        var photon = PhotonManager.Instance;
        if (photon != null && photon.IsConnected)
        {
            photon.Shutdown(notify: true);
        }

        // Return to previous map via DungeonManager if in dungeon
        if (DungeonManager.Instance != null && DungeonManager.Instance.IsInDungeon)
        {
            DungeonManager.Instance.ReturnToWorldMap();
        }
    }
}
