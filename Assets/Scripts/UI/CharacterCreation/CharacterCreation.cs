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

    private string selectedClass;
    private bool _isCreating;

    public void SelectKnight()
    {
        selectedClass = "Knight";
        Debug.Log("Knight Selected");
    }

    public void SelectMage()
    {
        selectedClass = "Mage";
        Debug.Log("Mage Selected");
    }

    public void SelectArcher()
    {
        selectedClass = "Archer";
        Debug.Log("Archer Selected");
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
                Debug.Log($"[CharacterCreation] Character created successfully: {response.Data.CharacterName}");

                // Save basic stats to WorldState
                WorldState.HasCharacter = true;
                WorldState.PlayerProfileId = response.Data.PlayerProfileId;
                WorldState.PlayerName = response.Data.CharacterName;
                WorldState.PlayerClass = response.Data.PlayerClass;
                
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