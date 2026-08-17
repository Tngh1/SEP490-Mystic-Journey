using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;

// Initializes a new default instance of the StatIconMapping class.
[System.Serializable]
public struct StatIconMapping
{
    [Tooltip("Tên chỉ số từ Backend: MaxHp, Atk, Def, MoveSpeed, AttackSpeed, CritRate, CritDamage, DamageBonus")]
    public string statName;
    [Tooltip("File hình Sprite icon mà bạn muốn dùng cho chỉ số này (tên file sprite là gì cũng được)")]
    public Sprite icon;
}

// Executes mono behaviour operation.
public class LevelUpUIManager : MonoBehaviour
{
    [Header("UI Component References")]
    [SerializeField] private Button[] statButtons;
    [SerializeField] private TMP_Text[] statTexts;
    [Tooltip("Kéo 5 khung ảnh Image hiển thị Icon tương ứng trên 5 Nút bấm vào đây (nơi chứa quả tim đỏ)")]
    [SerializeField] private Image[] statIcons;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private TMP_Text remainingPointsText;

    [Header("Gán Sprite Icon theo loại chỉ số")]
    [Tooltip("Danh sách Sprite icon tương ứng với từng tên chỉ số")]
    [SerializeField] private StatIconMapping[] statIconMappings;
    [Tooltip("Sprite icon mặc định nếu không tìm thấy trong danh sách")]
    [SerializeField] private Sprite defaultStatIcon;

    private List<string> currentOptions;

    // Initializes internal component caches and dependencies for StatIconMapping upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        for (int i = 0; statButtons != null && i < statButtons.Length; i++)
        {
            int index = i;
            if (statButtons[i] != null)
            {
                statButtons[i].onClick.AddListener(() => OnStatButtonClicked(index));
            }
        }
        AutoDetectStatIcons();
        SetupButtonFeedback();
    }

    // Executes setup button feedback operation.
    private void SetupButtonFeedback()
    {
        AddButtonFeedback(closeButton);
        if (statButtons == null) return;
        foreach (var button in statButtons)
            AddButtonFeedback(button);
    }

    // Executes add button feedback operation.
    private static void AddButtonFeedback(Button button)
    {
        if (button == null) return;

        if (button.targetGraphic == null)
            button.targetGraphic = button.GetComponent<Graphic>();

        if (button.GetComponent<UIHoverScaleEffect>() == null)
            button.gameObject.AddComponent<UIHoverScaleEffect>();
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        FetchOptions();
    }

    // Executes auto detect stat icons operation.
    private void AutoDetectStatIcons()
    {
        if (statButtons == null) return;
        if (statIcons == null || statIcons.Length != statButtons.Length)
        {
            statIcons = new Image[statButtons.Length];
        }

        for (int i = 0; i < statButtons.Length; i++)
        {
            if (statButtons[i] == null) continue;
            if (statIcons[i] != null) continue;

            var iconT = statButtons[i].transform.Find("Icon")
                     ?? statButtons[i].transform.Find("StatIcon")
                     ?? statButtons[i].transform.Find("Image");

            if (iconT != null && iconT.TryGetComponent<Image>(out var img))
            {
                statIcons[i] = img;
            }
            else
            {
                var btnImg = statButtons[i].GetComponent<Image>();
                var childImgs = statButtons[i].GetComponentsInChildren<Image>(true);
                foreach (var ci in childImgs)
                {
                    if (ci != btnImg)
                    {
                        statIcons[i] = ci;
                        break;
                    }
                }
            }
        }
    }

    // Queries remaining available stat points and 3 random card options from backend API.
    public void FetchOptions()
    {
        SetLoading(true); // Display loading spinner overlay
        PlayerApi.Instance.GetMyProfile(
            onSuccess: profile =>
            {
                if (remainingPointsText != null)
                {
                    remainingPointsText.text = $"Remaining: {profile.AvailableStatPoints}"; // Display remaining unallocated attribute points
                }

                CharacterApi.Instance.GetLevelUpOptions(
                    onSuccess: options =>
                    {
                        SetLoading(false);
                        currentOptions = options; // Cache list of offered stat choices (e.g. MaxHp, Atk, CritRate)
                        UpdateUI(); // Render stat upgrade buttons
                    },
                    onError: error =>
                    {
                        SetLoading(false);
                        Debug.LogError("Failed to fetch level up options: " + error.Message);
                        if (error.ErrorCode == "NO_STAT_POINTS")
                        {
                            ClosePanel(); // Automatically dismiss dialog if no points remain
                        }
                    }
                );
            },
            onError: error =>
            {
                SetLoading(false);
                Debug.LogError("Failed to fetch profile in level up panel: " + error.Message);
                ClosePanel();
            }
        );
    }

    // Binds option labels, icons, and click handlers to the UI cards.
    private void UpdateUI()
    {
        if (currentOptions == null || currentOptions.Count == 0) return;

        AutoDetectStatIcons();

        for (int i = 0; i < statButtons.Length; i++)
        {
            if (i < currentOptions.Count)
            {
                string statKey = currentOptions[i];
                statButtons[i].gameObject.SetActive(true); // Make active card visible

                if (statTexts[i] != null)
                {
                    statTexts[i].text = GetStatDisplayName(statKey); // Format stat name with color-coded bonus value
                }

                if (statIcons != null && i < statIcons.Length && statIcons[i] != null)
                {
                    Sprite icon = GetStatIcon(statKey);
                    if (icon != null)
                    {
                        statIcons[i].sprite = icon; // Assign corresponding sprite icon
                        statIcons[i].gameObject.SetActive(true);
                    }
                }
            }
            else
            {
                statButtons[i].gameObject.SetActive(false); // Hide unused card slots
            }
        }
    }

    // Resolves mapped sprite icon asset for the specified stat name.
    private Sprite GetStatIcon(string statName)
    {
        if (string.IsNullOrEmpty(statName)) return defaultStatIcon;
        if (statIconMappings != null && statIconMappings.Length > 0)
        {
            string key = statName.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            if (key == "attack") key = "atk";
            if (key == "defense") key = "def";
            if (key == "hp") key = "maxhp";

            foreach (var mapping in statIconMappings)
            {
                if (!string.IsNullOrEmpty(mapping.statName))
                {
                    string mappingKey = mapping.statName.ToLowerInvariant().Replace(" ", "").Replace("_", "");
                    if (mappingKey == "attack") mappingKey = "atk";
                    if (mappingKey == "defense") mappingKey = "def";
                    if (mappingKey == "hp") mappingKey = "maxhp";

                    if (mappingKey == key)
                    {
                        if (mapping.icon != null) return mapping.icon; // Return matching icon asset
                    }
                }
            }
        }
        return defaultStatIcon; // Return fallback default icon
    }

    // Submits selected stat upgrade choice to backend API and updates local stats.
    private void OnStatButtonClicked(int index)
    {
        if (currentOptions == null || index >= currentOptions.Count) return;

        string selectedStat = currentOptions[index];
        SetLoading(true);

        var request = new AllocateStatRequestDto { StatName = selectedStat };

        CharacterApi.Instance.AllocateStat(
            request,
            onSuccess: response =>
            {
                SetLoading(false);
                if (PlayerHUDUIManager.Instance != null)
                {
                    PlayerHUDUIManager.Instance.RefreshHUD(); // Refresh player level, EXP bar, and current health
                }

                var inventory = FindFirstObjectByType<InventoryUIManager>();
                if (inventory != null)
                {
                    inventory.LoadInventory(force: true, refreshStats: true); // Force recalculation of effective attributes in UI
                }

                PlayerApi.Instance.GetMyProfile(
                    onSuccess: profile =>
                    {
                        if (profile.AvailableStatPoints > 0)
                        {
                            FetchOptions(); // If player leveled up multiple times, pull next set of 3 cards
                        }
                        else
                        {
                            ClosePanel(); // Dismiss level-up screen when all points allocated
                        }
                    },
                    onError: error =>
                    {
                        ClosePanel();
                    }
                );
            },
            onError: error =>
            {
                SetLoading(false);
                Debug.LogError("Failed to allocate stat: " + error.Message);
            }
        );
    }

    // Executes set loading operation.
    private void SetLoading(bool isLoading)
    {
        if (loadingOverlay != null) loadingOverlay.SetActive(isLoading);
        foreach (var btn in statButtons)
        {
            if (btn != null) btn.interactable = !isLoading;
        }
    }

    // Update visibility for panel; it updates active.
    private void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    // Executes get stat display name operation.
    private string GetStatDisplayName(string statName)
    {
        switch (statName.ToLowerInvariant())
        {
            case "maxhp": return "<align=center><b>Max HP</b>\n<color=#4CAF50>+20</color></align>";
            case "atk": return "<align=center><b>Attack</b>\n<color=#F44336>+3</color></align>";
            case "def": return "<align=center><b>Defense</b>\n<color=#FFEB3B>+2</color></align>";
            case "movespeed": return "<align=center><b>Move Speed</b>\n<color=#2196F3>+1</color></align>";
            case "attackspeed": return "<align=center><b>Attack Speed</b>\n<color=#FF9800>+1</color></align>";
            case "critrate": return "<align=center><b>Crit Rate</b>\n<color=#9C27B0>+1%</color></align>";
            case "critdamage": return "<align=center><b>Crit Damage</b>\n<color=#E91E63>+2%</color></align>";
            case "damagebonus": return "<align=center><b>Damage Bonus</b>\n<color=#00BCD4>+1%</color></align>";
            default: return $"<align=center><b>{statName}</b></align>";
        }
    }
}
