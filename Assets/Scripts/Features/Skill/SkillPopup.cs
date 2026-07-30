using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

public class SkillPopup : MonoBehaviour
{
    [Header("UI References")]
    public Image popupIcon;
    public TextMeshProUGUI popupName;
    public TextMeshProUGUI popupDesc;
    public TextMeshProUGUI popupStats;
    public TextMeshProUGUI errorMessageText; // Dedicated error text at bottom of panel
    public Button upgradeButton;
    public Button dismantleButton;
    public TMP_Dropdown targetDropdown;

    // maps dropdown index -> PlayerSkillId (0 = None)
    private List<int> _dropdownPlayerSkillIds = new List<int>();

    public GameObject skillListArea;

    private PlayerSkillResponse currentServerData;

    private void ClearError()
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = "";
            errorMessageText.gameObject.SetActive(false);
        }
    }

    private void ShowError(string msg)
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = $"<color=#FF4444><b>Error:</b> {msg}</color>";
            errorMessageText.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[SkillPopup Error] {msg}");
        }
    }

    public void ShowPopup(SkillData visual, PlayerSkillResponse server)
    {
        currentServerData = server;
        ClearError();

        if (visual != null && visual.skillIcon != null)
        {
            popupIcon.sprite = visual.skillIcon;
            popupIcon.preserveAspect = true;
        }

        if (server != null)
        {
            popupName.text = $"<size=125%><color=#3D2314><b>{server.SkillName}</b></color></size>";
            popupDesc.text = $"<color=#4A3B32><i>\"{server.SkillDescription}\"</i></color>";
            popupStats.text = $"<b><color=#8B4513>✦ Level:</color></b>  <color=#000000><b>{server.Level}</b></color>\n\n" +
                              $"<b><color=#B22222>⚔️ Damage:</color></b>  <color=#8B0000><b>{server.EffectiveDamage}</b></color>\n\n" +
                              $"<b><color=#1E90FF>⏱️ Cooldown:</color></b>  <color=#006400><b>{server.CooldownSeconds}s</b></color>";

            if (upgradeButton != null)
            {
                upgradeButton.gameObject.SetActive(true);
                if (server.Level >= WorldState.PlayerLevel)
                {
                    upgradeButton.interactable = false;
                    ShowError("Skill level cannot exceed player level.");
                }
                else
                {
                    upgradeButton.interactable = true;
                }
            }

            if (dismantleButton != null)
            {
                dismantleButton.gameObject.SetActive(true);
                dismantleButton.interactable = !server.IsEquipped;
            }

            if (targetDropdown != null)
            {
                targetDropdown.ClearOptions();
                _dropdownPlayerSkillIds.Clear();

                var options = new List<string> { "None" };
                _dropdownPlayerSkillIds.Add(0);

                SkillApi.Instance.GetMySkills(
                    response =>
                    {
                        var mySkills = response.Skills ?? new List<PlayerSkillResponse>();
                        foreach (var s in mySkills)
                        {
                            if (s == null) continue;
                            if (s.PlayerSkillId == server.PlayerSkillId) continue;
                            options.Add($"{s.SkillName} (Lv.{s.Level})");
                            _dropdownPlayerSkillIds.Add(s.PlayerSkillId);
                        }
                        targetDropdown.AddOptions(options);
                        targetDropdown.value = 0;
                    },
                    error =>
                    {
                        Debug.LogWarning("Could not load player skills for dismantle dropdown: " + error.Message);
                        targetDropdown.AddOptions(new List<string> { "None" });
                        targetDropdown.value = 0;
                    }
                );
            }
        }
        else
        {
            popupName.text = "<size=125%><color=#708090><b>🔒 Skill Locked</b></color></size>";
            popupDesc.text = "<color=#696969><i>You do not own this skill yet. Complete quests or reach the required level to unlock it.</i></color>";
            popupStats.text = "<b><color=#708090>✦ Level:</color></b>  <color=#555555>0</color>\n\n<b><color=#708090>⚔️ Damage:</color></b>  <color=#555555>0</color>\n\n<b><color=#708090>⏱️ Cooldown:</color></b>  <color=#555555>0s</color>";

            if (upgradeButton != null) upgradeButton.gameObject.SetActive(false);
            if (dismantleButton != null) dismantleButton.gameObject.SetActive(false);
            if (targetDropdown != null)
            {
                targetDropdown.ClearOptions();
            }
        }

        if (skillListArea != null)
        {
            skillListArea.SetActive(false);
        }

        gameObject.SetActive(true);
    }

    public void HidePopup()
    {
        ClearError();
        if (skillListArea != null)
        {
            skillListArea.SetActive(true);
        }

        gameObject.SetActive(false);
    }

    public void OnClickUpgrade()
    {
        if (currentServerData == null) return;
        ClearError();

        upgradeButton.interactable = false;

        SkillApi.Instance.UpgradePlayerSkill(
            currentServerData.PlayerSkillId,
            onSuccess: (updatedSkill) =>
            {
                Debug.Log("Upgrade successful to Level " + updatedSkill.Level);

                currentServerData = updatedSkill;
                popupStats.text = $"<b><color=#8B4513>✦ Level:</color></b>  <color=#000000><b>{updatedSkill.Level}</b></color>\n\n" +
                                  $"<b><color=#B22222>⚔️ Damage:</color></b>  <color=#8B0000><b>{updatedSkill.EffectiveDamage}</b></color>\n\n" +
                                  $"<b><color=#1E90FF>⏱️ Cooldown:</color></b>  <color=#006400><b>{updatedSkill.CooldownSeconds}s</b></color>";

                upgradeButton.interactable = true;

                var panelManager = FindFirstObjectByType<SkillPanelManager>(FindObjectsInactive.Include);
                if (panelManager != null)
                {
                    panelManager.RefreshSkillList();
                }

                var invManager = FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
                if (invManager != null)
                {
                    invManager.LoadInventory(true);
                }
            },
            onError: (error) =>
            {
                Debug.LogError("Upgrade failed: " + error.Message);
                ShowError(error.Message);
                upgradeButton.interactable = true;
            }
        );
    }

    public void OnClickDismantle()
    {
        if (currentServerData == null) return;
        ClearError();

        dismantleButton.interactable = false;

        int? targetId = null;
        if (targetDropdown != null && _dropdownPlayerSkillIds.Count > 0)
        {
            int idx = targetDropdown.value;
            if (idx >= 0 && idx < _dropdownPlayerSkillIds.Count)
            {
                int mapped = _dropdownPlayerSkillIds[idx];
                if (mapped != 0) targetId = mapped;
            }
        }

        SkillApi.Instance.DismantlePlayerSkill(
            currentServerData.PlayerSkillId,
            targetId,
            onSuccess: (updated) =>
            {
                Debug.Log("Dismantle success");
                dismantleButton.interactable = true;

                var panelManager = FindFirstObjectByType<SkillPanelManager>(FindObjectsInactive.Include);
                if (panelManager != null)
                {
                    panelManager.RefreshSkillList();
                }
                HidePopup();
            },
            onError: (error) =>
            {
                Debug.LogError("Dismantle failed: " + error.Message);
                ShowError(error.Message);
                dismantleButton.interactable = true;
            }
        );
    }
}