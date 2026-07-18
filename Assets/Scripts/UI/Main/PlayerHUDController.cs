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

    [Header("Colors")]
    [SerializeField] private Color expBarColor = new Color(0.35f, 0.78f, 0.98f); // Light Sky Blue
    [SerializeField] private Color highHealthColor = new Color(0.298f, 0.686f, 0.314f);  // #4CAF50
    [SerializeField] private Color mediumHealthColor = new Color(1f, 0.92f, 0.23f);       // #FFEB3B
    [SerializeField] private Color lowHealthColor = new Color(0.956f, 0.263f, 0.212f);    // #F44336

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
    }

    private void OnDisable()
    {
        StopHUDLoop();
        if (levelUpButton != null)
        {
            levelUpButton.onClick.RemoveListener(OnLevelUpButtonClicked);
        }
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
        if (corruptionText == null) corruptionText = transform.Find("TopBar/Center_Resources/CorruptionBox/CorruptionText")?.GetComponent<TMP_Text>();

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
            yield return new WaitForSeconds(3.0f);
        }
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

        // Step 2: Refresh Character Stats (Current HP, Max HP)
        CharacterApi.Instance.GetMyStats(
            statsResponse =>
            {
                if (statsResponse != null)
                {
                    ApplyStats(statsResponse);
                }
            },
            error =>
            {
                Debug.LogWarning($"[PlayerHUDController] Failed to refresh stats: {error.Message}");
            }
        );

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
            expBarImage.color = expBarColor;
            
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
        float hpRatio = stats.MaxHp > 0 ? (float)stats.CurrentHp / stats.MaxHp : 0f;

        if (hpBarImage != null)
        {
            hpBarImage.fillAmount = Mathf.Clamp01(hpRatio);

            // Update HP Bar Color based on current HP percentage:
            // >= 50%: Green
            // >= 20% and < 50%: Yellow
            // < 20%: Red
            if (hpRatio >= 0.5f)
            {
                hpBarImage.color = highHealthColor;
            }
            else if (hpRatio >= 0.2f)
            {
                hpBarImage.color = mediumHealthColor;
            }
            else
            {
                hpBarImage.color = lowHealthColor;
            }
        }

        if (hpText != null)
        {
            hpText.text = stats.CurrentHp + " / " + stats.MaxHp;
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
}
