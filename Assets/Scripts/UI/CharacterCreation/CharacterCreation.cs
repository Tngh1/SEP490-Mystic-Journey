using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Request;
using MysticJourney.API.Models.Response;
using MysticJourney.Core.Utilities;

public class CharacterCreation : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField nameInput;

    [Header("Class Selection Lights (Optional drag & drop)")]
    [SerializeField] private GameObject knightLight;
    [SerializeField] private GameObject mageLight;
    [SerializeField] private GameObject archerLight;

    private string selectedClass;
    private bool _isCreating;

    private void Start()
    {
        FindLights();
        UpdateClassLights(); // Turn off all lights initially until a class is explicitly selected
    }

    private void FindLights()
    {
        // Try finding active/inactive Light objects by name and parent area name to avoid inspector dependency
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

    private void UpdateClassLights()
    {
        if (knightLight != null) knightLight.SetActive(selectedClass == "Knight");
        if (mageLight != null) mageLight.SetActive(selectedClass == "Mage");
        if (archerLight != null) archerLight.SetActive(selectedClass == "Archer");
    }

    public void SelectKnight()
    {
        selectedClass = "Knight";
        Debug.Log("Knight Selected");
        UpdateClassLights();
    }

    public void SelectMage()
    {
        selectedClass = "Mage";
        Debug.Log("Mage Selected");
        UpdateClassLights();
    }

    public void SelectArcher()
    {
        selectedClass = "Archer";
        Debug.Log("Archer Selected");
        UpdateClassLights();
    }

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

                // Save basic stats to WorldState
                WorldState.HasCharacter = true;
                WorldState.PlayerProfileId = response.PlayerProfileId;
                WorldState.PlayerName = response.CharacterName;
                WorldState.PlayerClass = response.PlayerClass;
                
                // Set starting map name and coordinate
                WorldState.CurrentMapName = GameConstants.Scenes.AbandonedCastle;
                WorldState.LastPosition = GameConstants.WorldDefaults.DefaultSpawnPosition;

                // Persist locally
                WorldState.SaveToPlayerPrefs();

                // Go to the first map using GameBootstrap
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