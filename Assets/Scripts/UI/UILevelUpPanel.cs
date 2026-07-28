using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;

public class UILevelUpPanel : MonoBehaviour
{
    [SerializeField] private Button[] statButtons;
    [SerializeField] private TMP_Text[] statTexts;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject loadingOverlay;
    [SerializeField] private TMP_Text remainingPointsText;
    
    private List<string> currentOptions;
    
    private void Awake()
    {
        closeButton.onClick.AddListener(ClosePanel);
        for (int i = 0; i < statButtons.Length; i++)
        {
            int index = i;
            statButtons[i].onClick.AddListener(() => OnStatButtonClicked(index));
        }
    }
    
    private void OnEnable()
    {
        FetchOptions();
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
        
        for (int i = 0; i < statButtons.Length; i++)
        {
            if (i < currentOptions.Count)
            {
                statButtons[i].gameObject.SetActive(true);
                statTexts[i].text = GetStatDisplayName(currentOptions[i]);
            }
            else
            {
                statButtons[i].gameObject.SetActive(false);
            }
        }
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
            btn.interactable = !isLoading;
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
            case "maxhp": return "<align=center>Max HP\n<color=#4CAF50>+20</color></align>";
            case "atk": return "<align=center>Attack\n<color=#F44336>+3</color></align>";
            case "def": return "<align=center>Defense\n<color=#FFEB3B>+2</color></align>";
            case "movespeed": return "<align=center>Move Speed\n<color=#2196F3>+1</color></align>";
            case "attackspeed": return "<align=center>Attack Speed\n<color=#FF9800>+1</color></align>";
            case "critrate": return "<align=center>Crit Rate\n<color=#9C27B0>+1%</color></align>";
            case "critdamage": return "<align=center>Crit Damage\n<color=#E91E63>+2%</color></align>";
            case "damagebonus": return "<align=center>Damage Bonus\n<color=#00BCD4>+1%</color></align>";
            default: return $"<align=center>{statName}</align>";
        }
    }
}
