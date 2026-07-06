using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

public class PlayerHUDController : MonoBehaviour
{
    public static PlayerHUDController Instance { get; private set; }

    [Header("UI Reference Cache")]
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image expBarImage;
    [SerializeField] private Image hpBarImage;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text gemText;

    [Header("HUD Buttons")]
    [SerializeField] private GameObject settingsButtonObj;
    [SerializeField] private GameObject pauseButtonObj;

    [Header("HP Bar Color Customization")]
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
    }

    private void OnDisable()
    {
        StopHUDLoop();
    }

    public void FindHUDReferences()
    {
        if (playerNameText == null) playerNameText = transform.Find("TopBar/Button/PlayerNameText")?.GetComponent<TMP_Text>();
        if (levelText == null) levelText = transform.Find("TopBar/Button/LevelText")?.GetComponent<TMP_Text>();
        if (expBarImage == null) expBarImage = transform.Find("TopBar/Button/ExpBar")?.GetComponent<Image>();
        if (hpBarImage == null) hpBarImage = transform.Find("BottomCenter/HPBar")?.GetComponent<Image>();
        if (hpText == null) hpText = transform.Find("BottomCenter/HPBar/HPText")?.GetComponent<TMP_Text>();
        if (energyText == null)
        {
            energyText = transform.Find("TopBar/Center_Resources/EnergyBox/EnergyText")?.GetComponent<TMP_Text>();
        }
        if (goldText == null) goldText = transform.Find("TopBar/Center_Resources/GoldBox/GoldText")?.GetComponent<TMP_Text>();
        if (gemText == null) gemText = transform.Find("TopBar/Center_Resources/GemBox/GemText")?.GetComponent<TMP_Text>();

        if (settingsButtonObj == null) 
        {
            var btn = transform.Find("TopRight/SettingsButton"); // Tùy chỉnh đường dẫn này theo UI thực tế
            if (btn != null) settingsButtonObj = btn.gameObject;
        }

        if (pauseButtonObj == null)
        {
            var btn = transform.Find("TopRight/PauseButton"); // Tùy chỉnh đường dẫn này theo UI thực tế
            if (btn != null) pauseButtonObj = btn.gameObject;
        }

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

        // Step 1: Refresh Profile (Username, Level, Experience)
        PlayerApi.Instance.GetMyProfile(
            profile =>
            {
                // Update WorldState Level so other parts of the game are aware
                WorldState.PlayerLevel = profile.Level;
                WorldState.PlayerName = profile.DisplayName ?? profile.AccountEmail;

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

        if (energyText != null)
        {
            energyText.text = profile.Energy + "/" + profile.MaxEnergy;
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

    private static string FormatCurrencyAmount(decimal amount)
    {
        return amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
    }
}
