using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;

[System.Serializable]
public struct StatIconMapping
{
    [Tooltip("Tên chỉ số từ Backend: MaxHp, Atk, Def, MoveSpeed, AttackSpeed, CritRate, CritDamage, DamageBonus")]
    public string statName;
    [Tooltip("File hình Sprite icon mà bạn muốn dùng cho chỉ số này (tên file sprite là gì cũng được)")]
    public Sprite icon;
}

public class UILevelUpPanel : MonoBehaviour
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
    
    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        for (int i = 0; i < statButtons.Length; i++)
        {
            int index = i;
            if (statButtons[i] != null)
            {
                statButtons[i].onClick.AddListener(() => OnStatButtonClicked(index));
            }
        }
        AutoDetectStatIcons();
    }
    
    private void OnEnable()
    {
        FetchOptions();
    }

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

            // 1. Check for child named "Icon", "StatIcon", or "Image"
            var iconT = statButtons[i].transform.Find("Icon") 
                     ?? statButtons[i].transform.Find("StatIcon")
                     ?? statButtons[i].transform.Find("Image");

            if (iconT != null && iconT.TryGetComponent<Image>(out var img))
            {
                statIcons[i] = img;
            }
            else
            {
                // 2. Check for child Image separate from the button's background Image
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
    
    public void FetchOptions()
    {
        SetLoading(true);
        PlayerApi.Instance.GetMyProfile(
            onSuccess: profile =>
            {
                if (remainingPointsText != null)
                {
                    remainingPointsText.text = $"Remaining: {profile.AvailableStatPoints}";
                }
                
                CharacterApi.Instance.GetLevelUpOptions(
                    onSuccess: options =>
                    {
                        SetLoading(false);
                        currentOptions = options;
                        UpdateUI();
                    },
                    onError: error =>
                    {
                        SetLoading(false);
                        Debug.LogError("Failed to fetch level up options: " + error.Message);
                        if (error.ErrorCode == "NO_STAT_POINTS")
                        {
                            ClosePanel();
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
    
    private void UpdateUI()
    {
        if (currentOptions == null || currentOptions.Count == 0) return;
        
        AutoDetectStatIcons();
        
        for (int i = 0; i < statButtons.Length; i++)
        {
            if (i < currentOptions.Count)
            {
                string statKey = currentOptions[i];
                statButtons[i].gameObject.SetActive(true);

                if (statTexts[i] != null)
                {
                    statTexts[i].text = GetStatDisplayName(statKey);
                }

                if (statIcons != null && i < statIcons.Length && statIcons[i] != null)
                {
                    Sprite icon = GetStatIcon(statKey);
                    if (icon != null)
                    {
                        statIcons[i].sprite = icon;
                        statIcons[i].gameObject.SetActive(true);
                    }
                }
            }
            else
            {
                statButtons[i].gameObject.SetActive(false);
            }
        }
    }

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
                        if (mapping.icon != null) return mapping.icon;
                    }
                }
            }
        }
        return defaultStatIcon;
    }
    
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
                // Call GetMyProfile or update stats directly if needed
                if (PlayerHUDController.Instance != null)
                {
                    PlayerHUDController.Instance.RefreshHUD();
                }
                
                var inventory = FindObjectOfType<InventoryManager>();
                if (inventory != null)
                {
                    inventory.LoadInventory(force: true, refreshStats: true);
                }

                // If the player still has points, fetch new options. Otherwise close.
                PlayerApi.Instance.GetMyProfile(
                    onSuccess: profile =>
                    {
                        if (profile.AvailableStatPoints > 0)
                        {
                            FetchOptions();
                        }
                        else
                        {
                            ClosePanel();
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
    
    private void SetLoading(bool isLoading)
    {
        if (loadingOverlay != null) loadingOverlay.SetActive(isLoading);
        foreach (var btn in statButtons)
        {
            if (btn != null) btn.interactable = !isLoading;
        }
    }
    
    private void ClosePanel()
    {
        gameObject.SetActive(false);
    }
    
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
