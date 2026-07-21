using System.Collections;
using System.Globalization;
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



    private Coroutine _updateLoopCoroutine;
    private bool _isRefreshing;
    private bool _isCurrencyRefreshing;

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
        WorldRuntimeEvents.QuestsChanged -= UpdateQuestPointers;
        WorldRuntimeEvents.QuestsChanged += UpdateQuestPointers;
    }

    private void OnDisable()
    {
        StopHUDLoop();
        if (levelUpButton != null)
        {
            levelUpButton.onClick.RemoveListener(OnLevelUpButtonClicked);
        }
        PlayerEntity.OnHealthChanged -= HandleHealthChanged;
        WorldRuntimeEvents.QuestsChanged -= UpdateQuestPointers;
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
        if (goldText == null) goldText = transform.Find("TopBar/Center_Resources/GoldBox/GoldText")?.GetComponent<TMP_Text>();
        if (gemText == null) gemText = transform.Find("TopBar/Center_Resources/GemBox/GemText")?.GetComponent<TMP_Text>();
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
                var text = go.AddComponent<TMP_Text>();
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
        if (active == null) 
        {
            ClearAllQuestPointers();
            return;
        }

        bool gacha = false, guild = false, shop = false, daily = false;
        var objType = active.ObjectiveType?.ToLower();
        
        if (objType == "gacha") gacha = true;
        if (objType == "guild") guild = true;
        if (objType == "shop" || objType == "buy") shop = true;
        if (objType == "achievement" || objType == "daily") daily = true;

        EnsureQuestPointer(gachaButtonObj, gacha);
        EnsureQuestPointer(guildButtonObj, guild);
        EnsureQuestPointer(shopButtonObj, shop);
        EnsureQuestPointer(dailyButtonObj, daily);
    }
    
    private void ClearAllQuestPointers()
    {
        EnsureQuestPointer(gachaButtonObj, false);
        EnsureQuestPointer(guildButtonObj, false);
        EnsureQuestPointer(shopButtonObj, false);
        EnsureQuestPointer(dailyButtonObj, false);
    }
}
