using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

// Executes mono behaviour operation.
public class SkillPopup : MonoBehaviour
{
    [Header("UI References - Main")]
    public Image popupIcon;
    public TextMeshProUGUI popupName;
    public TextMeshProUGUI popupDesc;
    public TextMeshProUGUI popupStats;
    public TextMeshProUGUI errorMessageText;
    public Button upgradeButton;

    [Header("UI References - Stone Count & Cost")]
    public TextMeshProUGUI ownedStoneText;
    public TextMeshProUGUI requiredStoneText;

    [Header("UI References - Top Section (Background1 & ClassBg)")]
    public TextMeshProUGUI topLevelBadgeText;
    public TextMeshProUGUI topDamageText;
    public TextMeshProUGUI topCooldownText;

    [Header("UI References - Review Section (Background2)")]
    public TextMeshProUGUI oldLevelText;
    public TextMeshProUGUI newLevelText;
    public TextMeshProUGUI oldDamageText;
    public TextMeshProUGUI newDamageText;
    public TextMeshProUGUI oldCooldownText;
    public TextMeshProUGUI newCooldownText;

    public GameObject skillListArea;

    private PlayerSkillResponse currentServerData;
    private int currentOwnedStones = 0;

    // Executes current player level operation.
    private static int CurrentPlayerLevel => Mathf.Max(
        WorldState.PlayerLevel,
        PlayerPrefs.GetInt(MysticJourney.API.Core.ApiConfig.PlayerLevelKey, 1));

    // Initializes internal component caches and dependencies for SkillPopup upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        AutoBindComponents();
    }

    // Executes auto bind components operation.
    public void AutoBindComponents()
    {
        if (popupIcon == null) popupIcon = transform.Find("Popup_Icon")?.GetComponent<Image>();
        if (popupName == null) popupName = transform.Find("Background1/SkillName")?.GetComponent<TextMeshProUGUI>();
        if (popupDesc == null) popupDesc = transform.Find("Desc")?.GetComponent<TextMeshProUGUI>() ?? transform.Find("Background1/Desc")?.GetComponent<TextMeshProUGUI>();
        if (popupStats == null)
        {
            popupStats = transform.Find("UpgradeReview")?.GetComponent<TextMeshProUGUI>()
                      ?? transform.Find("Background2/UpgradeReview")?.GetComponent<TextMeshProUGUI>();
        }
        if (upgradeButton == null) upgradeButton = transform.Find("Upgrade")?.GetComponent<Button>();

        if (topLevelBadgeText == null) topLevelBadgeText = transform.Find("ClassBg/SkillLevel")?.GetComponent<TextMeshProUGUI>();
        if (topDamageText == null) topDamageText = transform.Find("Background1/Dame/DameNumber")?.GetComponent<TextMeshProUGUI>();
        if (topCooldownText == null) topCooldownText = transform.Find("Background1/Cooldown/CooldownNumber")?.GetComponent<TextMeshProUGUI>();

        if (oldLevelText == null) oldLevelText = transform.Find("Background2/OldLevelToNewLevel/OldLevel")?.GetComponent<TextMeshProUGUI>();
        if (newLevelText == null) newLevelText = transform.Find("Background2/OldLevelToNewLevel/NewLevel")?.GetComponent<TextMeshProUGUI>();
        if (oldDamageText == null) oldDamageText = transform.Find("Background2/Dame/OldDameToNewDame/OldDame")?.GetComponent<TextMeshProUGUI>();
        if (newDamageText == null)
        {
            newDamageText = transform.Find("Background2/Dame/OldDameToNewDame/NewLevel")?.GetComponent<TextMeshProUGUI>()
                         ?? transform.Find("Background2/Dame/OldDameToNewDame/NewDame")?.GetComponent<TextMeshProUGUI>();
        }
        if (oldCooldownText == null) oldCooldownText = transform.Find("Background2/Cooldown/OldCooldownToNewCooldown/OldCooldown")?.GetComponent<TextMeshProUGUI>();
        if (newCooldownText == null) newCooldownText = transform.Find("Background2/Cooldown/OldCooldownToNewCooldown/NewCooldown")?.GetComponent<TextMeshProUGUI>();

        EnsureStoneUI();

        Transform bg2 = transform.Find("Background2");
        if (bg2 != null)
        {
            bg2.gameObject.SetActive(true);
        }
    }

    // Executes ensure stone ui operation.
    private void EnsureStoneUI()
    {
        if (ownedStoneText == null)
        {
            ownedStoneText = transform.parent?.Find("Header/Stone/NumberStone")?.GetComponent<TextMeshProUGUI>();
        }

        if (requiredStoneText == null)
        {
            requiredStoneText = transform.Find("Upgrade/NumberUp")?.GetComponent<TextMeshProUGUI>();
        }
    }

    // Executes clear error operation.
    private void ClearError()
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = "";
            errorMessageText.gameObject.SetActive(false);
        }
    }

    // Executes get required stones for level operation.
    private int GetRequiredStonesForLevel(int level)
    {
        return Mathf.Max(1, level);
    }

    // Executes private operation.
    private (double nextDamage, int nextCooldown) CalculateNextLevelStats(PlayerSkillResponse server)
    {
        if (server == null) return (0, 0);

        double nextDamage = server.EffectiveDamage;
        if (server.DamagePerLevel > 0)
        {
            nextDamage = server.EffectiveDamage + server.DamagePerLevel;
        }
        else if (server.DamageGrowthPercent > 0)
        {
            double growth = server.DamageGrowthPercent > 1.0 ? server.DamageGrowthPercent / 100.0 : server.DamageGrowthPercent;
            nextDamage = server.EffectiveDamage * (1.0 + growth);
        }
        else if (server.BaseDamage > 0)
        {
            nextDamage = server.EffectiveDamage + (server.BaseDamage * 0.15);
        }
        else
        {
            nextDamage = server.EffectiveDamage * 1.15;
        }

        int nextCooldown = server.CooldownSeconds;

        return (nextDamage, nextCooldown);
    }

    // Executes update review section operation.
    private void UpdateReviewSection(PlayerSkillResponse server)
    {
        if (server == null) return;

        int currentLevel = server.Level;
        int playerLevel = CurrentPlayerLevel;
        bool isMaxLevelForPlayer = (currentLevel >= playerLevel);

        var (nextDamage, nextCooldown) = CalculateNextLevelStats(server);

        if (oldLevelText != null) oldLevelText.text = $"Lv. {currentLevel}";
        if (newLevelText != null) newLevelText.text = isMaxLevelForPlayer ? "<color=#FF8C00>MAX</color>" : $"Lv. {currentLevel + 1}";

        if (oldDamageText != null) oldDamageText.text = $"{server.EffectiveDamage:0.#}";
        if (newDamageText != null) newDamageText.text = isMaxLevelForPlayer ? "<color=#FF8C00>MAX</color>" : $"{nextDamage:0.#}";

        if (oldCooldownText != null) oldCooldownText.text = $"{server.CooldownSeconds}s";
        if (newCooldownText != null) newCooldownText.text = isMaxLevelForPlayer ? "<color=#FF8C00>MAX</color>" : $"{nextCooldown}s";

        if (popupStats != null)
        {
            popupStats.text = "<b><color=#B22222>Skill Upgrade Preview</color></b>";
        }
    }

    // Executes update upgrade button state operation.
    private void UpdateUpgradeButtonState(int currentLevel, int playerLevel, int ownedStones)
    {
        int requiredStones = GetRequiredStonesForLevel(currentLevel);

        if (requiredStoneText != null)
        {
            requiredStoneText.text = $"x{requiredStones}";
        }

        if (currentLevel >= playerLevel)
        {
            if (upgradeButton != null) upgradeButton.interactable = false;
            if (requiredStoneText != null) requiredStoneText.color = Color.white;
        }
        else if (ownedStones < requiredStones)
        {
            if (upgradeButton != null) upgradeButton.interactable = false;
            if (requiredStoneText != null) requiredStoneText.color = new Color(1f, 0.4f, 0.4f, 1f);
        }
        else
        {
            if (upgradeButton != null) upgradeButton.interactable = true;
            if (requiredStoneText != null) requiredStoneText.color = Color.white;
        }
    }

    // Executes fetch stones and update ui operation.
    // Validates input parameters against null or empty values.
    private void FetchStonesAndUpdateUI(System.Action onDone = null)
    {
        InventoryApi.Instance.GetInventory(
            onSuccess: (summary) =>
            {
                int stones = 0;
                if (summary?.BagItems != null)
                {
                    foreach (var item in summary.BagItems)
                    {
                        if (item != null && !string.IsNullOrEmpty(item.ItemName) &&
                            (item.ItemId == 22 || item.ItemName.Equals("Skill Upgrade Stone", System.StringComparison.OrdinalIgnoreCase) || (item.ItemName.Contains("Skill Upgrade") && item.ItemName.Contains("Stone"))))
                        {
                            stones += item.Quantity;
                        }
                    }
                }
                currentOwnedStones = stones;

                if (ownedStoneText != null)
                {
                    ownedStoneText.text = stones.ToString();
                }

                if (currentServerData != null)
                {
                    UpdateUpgradeButtonState(currentServerData.Level, CurrentPlayerLevel, currentOwnedStones);
                }

                onDone?.Invoke();
            },
            onError: (err) =>
            {
                Debug.LogWarning("[SkillPopup] Failed to fetch inventory stones: " + err.Message);
                onDone?.Invoke();
            }
        );
    }

    // Executes show popup operation.
    public void ShowPopup(SkillData visual, PlayerSkillResponse server)
    {
        currentServerData = server;
        AutoBindComponents();
        ClearError();

        Transform bg2 = transform.Find("Background2");
        if (bg2 != null) bg2.gameObject.SetActive(true);

        if (visual != null && visual.skillIcon != null && popupIcon != null)
        {
            popupIcon.sprite = visual.skillIcon;
            popupIcon.preserveAspect = true;
        }

        var topSkillName = transform.Find("Background1/SkillName")?.GetComponent<TextMeshProUGUI>();
        var topDesc = transform.Find("Background1/Desc")?.GetComponent<TextMeshProUGUI>();

        if (server != null)
        {
            int currentLevel = server.Level;
            int playerLevel = CurrentPlayerLevel;

            if (popupName != null) popupName.text = $"<size=125%><color=#3D2314><b>{server.SkillName}</b></color></size>";
            if (topSkillName != null) topSkillName.text = server.SkillName;

            if (popupDesc != null) popupDesc.text = $"<color=#4A3B32><i>\"{server.SkillDescription}\"</i></color>";
            if (topDesc != null) topDesc.text = $"\"{server.SkillDescription}\"";

            if (topLevelBadgeText != null) topLevelBadgeText.text = $"Lv. {currentLevel}";
            if (topDamageText != null) topDamageText.text = $"Damage: {server.EffectiveDamage:0.#}";
            if (topCooldownText != null) topCooldownText.text = $"Cooldown: {server.CooldownSeconds}s";

            UpdateReviewSection(server);

            FetchStonesAndUpdateUI();
        }
        else
        {
            if (popupName != null) popupName.text = "<size=125%><color=#708090><b>Skill Locked</b></color></size>";
            if (topSkillName != null) topSkillName.text = "Skill Locked";

            if (popupDesc != null) popupDesc.text = "<color=#696969><i>You do not own this skill yet. Complete quests or reach the required level to unlock it.</i></color>";
            if (topDesc != null) topDesc.text = "You do not own this skill yet.";

            if (topLevelBadgeText != null) topLevelBadgeText.text = "Lv. 0";
            if (topDamageText != null) topDamageText.text = "Damage: 0";
            if (topCooldownText != null) topCooldownText.text = "Cooldown: 0s";

            if (oldLevelText != null) oldLevelText.text = "Lv. 0";
            if (newLevelText != null) newLevelText.text = "Lv. 1";
            if (oldDamageText != null) oldDamageText.text = "0";
            if (newDamageText != null) newDamageText.text = "0";
            if (oldCooldownText != null) oldCooldownText.text = "0s";
            if (newCooldownText != null) newCooldownText.text = "0s";

            if (popupStats != null) popupStats.text = "<b><color=#708090>Skill Locked</color></b>";

            if (upgradeButton != null) upgradeButton.interactable = false;
        }

        if (skillListArea != null)
        {
            skillListArea.SetActive(false);
        }

        gameObject.SetActive(true);
    }

    // Executes hide popup operation.
    public void HidePopup()
    {
        ClearError();
        if (skillListArea != null)
        {
            skillListArea.SetActive(true);
        }

        gameObject.SetActive(false);
    }

    // Executes on click upgrade operation.
    public void OnClickUpgrade()
    {
        if (currentServerData == null) return;
        ClearError();

        if (upgradeButton != null) upgradeButton.interactable = false;

        SkillApi.Instance.UpgradePlayerSkill(
            currentServerData.PlayerSkillId,
            onSuccess: (updatedSkill) =>
            {
                Debug.Log("Upgrade successful to Level " + updatedSkill.Level);

                currentServerData = updatedSkill;

                if (topLevelBadgeText != null) topLevelBadgeText.text = $"Lv. {updatedSkill.Level}";
                if (topDamageText != null) topDamageText.text = $"Damage: {updatedSkill.EffectiveDamage:0.#}";
                if (topCooldownText != null) topCooldownText.text = $"Cooldown: {updatedSkill.CooldownSeconds}s";

                UpdateReviewSection(updatedSkill);

                FetchStonesAndUpdateUI();

                var panelManager = FindFirstObjectByType<SkillUIManager>(FindObjectsInactive.Include);
                if (panelManager != null)
                {
                    panelManager.RefreshSkillList();
                }

                var invManager = FindFirstObjectByType<InventoryUIManager>(FindObjectsInactive.Include);
                if (invManager != null)
                {
                    invManager.LoadInventory(true);
                }
            },
            onError: (error) =>
            {
                Debug.LogError("Upgrade failed: " + error.Message);
                FetchStonesAndUpdateUI();
            }
        );
    }
}
