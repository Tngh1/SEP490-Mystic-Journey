using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;
using System.Collections.Generic;

// Executes mono behaviour operation.
public class CharacterCreation : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField nameInput;

    [Header("Class Selection Lights (Optional drag & drop)")]
    [SerializeField] private GameObject knightLight;
    [SerializeField] private GameObject mageLight;
    [SerializeField] private GameObject archerLight;

    [Header("Class Stats UI")]
    [SerializeField] private GameObject statPanel;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text defText;
    [SerializeField] private TMP_Text moveSpeedText;
    [SerializeField] private TMP_Text attackSpeedText;
    [SerializeField] private TMP_Text critRateText;
    [SerializeField] private TMP_Text critDamageText;
    [SerializeField] private TMP_Text damageBonusText;

    private string selectedClass;
    private bool _isCreating;
    private List<ClassConfigDTO> classConfigs = new List<ClassConfigDTO>();

    // Performs startup initialization for CharacterCreation on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        FindLights();
        UpdateClassLights();

        if (statPanel != null)
            statPanel.SetActive(false);

        FetchClassStats();
    }

    // Executes fetch class stats operation.
    // Validates input parameters against null or empty values.
    private void FetchClassStats()
    {
        WikiApi.Instance.GetClasses(
            configs =>
            {
                classConfigs = configs;
                if (!string.IsNullOrEmpty(selectedClass))
                {
                    UpdateStatsUI();
                }
            },
            error =>
            {
                Debug.LogError($"Failed to fetch class configs: {error.Message}");
            }
        );
    }

    // Executes update stats ui operation.
    private void UpdateStatsUI()
    {
        if (classConfigs == null || classConfigs.Count == 0) return;

        var config = classConfigs.Find(c => c.ClassName == selectedClass);
        if (config != null)
        {
            if (statPanel != null) statPanel.SetActive(true);

            if (hpText != null) hpText.text = config.MaxHp.ToString("N0");
            if (atkText != null) atkText.text = config.Atk.ToString("N0");
            if (defText != null) defText.text = config.Def.ToString("N0");
            if (moveSpeedText != null) moveSpeedText.text = config.MoveSpeed.ToString("N0");
            if (attackSpeedText != null) attackSpeedText.text = config.AttackSpeed.ToString("N0");
            if (critRateText != null) critRateText.text = config.CritRate.ToString() + "%";
            if (critDamageText != null) critDamageText.text = config.CritDamage.ToString() + "%";
            if (damageBonusText != null) damageBonusText.text = config.DamageBonus.ToString() + "%";
        }
    }

    // Executes find lights operation.
    private void FindLights()
    {
        var allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (var t in allTransforms)
        {
            if (t == null) continue;
            if (t.name == "Light" && t.parent != null)
            {
                if (t.parent.name == "KnightArea" && knightLight == null)
                    knightLight = t.gameObject;
                else if (t.parent.name == "MageArea" && mageLight == null)
                    mageLight = t.gameObject;
                else if (t.parent.name == "ArcherArea" && archerLight == null)
                    archerLight = t.gameObject;
            }
        }
    }

    // Executes update class lights operation.
    private void UpdateClassLights()
    {
        if (knightLight != null) knightLight.SetActive(selectedClass == "Knight");
        if (mageLight != null) mageLight.SetActive(selectedClass == "Mage");
        if (archerLight != null) archerLight.SetActive(selectedClass == "Archer");
    }

    // Executes select knight operation.
    public void SelectKnight()
    {
        selectedClass = "Knight";
        Debug.Log("Knight Selected");
        UpdateClassLights();
        UpdateStatsUI();
    }

    // Executes select mage operation.
    public void SelectMage()
    {
        selectedClass = "Mage";
        Debug.Log("Mage Selected");
        UpdateClassLights();
        UpdateStatsUI();
    }

    // Executes select archer operation.
    // Validates input parameters against null or empty values.
    public void SelectArcher()
    {
        selectedClass = "Archer";
        Debug.Log("Archer Selected");
        UpdateClassLights();
        UpdateStatsUI();
    }

    // Execute character creation inside one transaction so profile, stats, starter skill, and default skin are committed together or rolled back together.
    public void CreateCharacter()
    {
        if (_isCreating) return;

        if (string.IsNullOrWhiteSpace(nameInput.text))
        {
            Debug.LogWarning("[CharacterCreation] Enter Name");
            return;
        }

        if (string.IsNullOrEmpty(selectedClass))
        {
            Debug.LogWarning("[CharacterCreation] Select Class");
            return;
        }

        _isCreating = true;

        var request = new CreateCharacterRequest
        {
            CharacterName = nameInput.text.Trim(),
            SelectedClass = selectedClass
        };

        CharacterApi.Instance.CreateCharacter(
            request,
            response =>
            {
                _isCreating = false;
                Debug.Log($"[CharacterCreation] Character created successfully: {response.CharacterName}");

                WorldState.HasCharacter = true;
                WorldState.PlayerProfileId = response.PlayerProfileId;
                WorldState.PlayerName = response.CharacterName;
                WorldState.PlayerClass = response.PlayerClass;

                WorldState.CurrentMapName = GameConstants.Scenes.ElfForest;
                WorldState.LastPosition = GameConstants.WorldDefaults.DefaultSpawnPosition;

                WorldState.SaveToPlayerPrefs();

                SceneManager.LoadScene(GameConstants.Scenes.Bootstrap);
            },
            error =>
            {
                _isCreating = false;
                Debug.LogError($"[CharacterCreation] Create character failed: {error.Message}");
            }
        );
    }
}
