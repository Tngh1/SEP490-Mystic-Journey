using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterCreation : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField nameInput;

    private string selectedClass;

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
        if (string.IsNullOrWhiteSpace(nameInput.text))
        {
            Debug.Log("Enter Name");
            return;
        }

        if (string.IsNullOrEmpty(selectedClass))
        {
            Debug.Log("Select Class");
            return;
        }

        WorldState.HasCharacter = true;

        WorldState.PlayerName =
            nameInput.text;

        WorldState.PlayerClass =
            selectedClass;

        WorldState.CurrentMapName =
            "Abandoned  Castle";

        WorldState.LastPosition =
            new Vector3(0, 0, 0);

        SceneManager.LoadScene("Loading");
        Debug.Log("Selected Class: " + WorldState.PlayerClass);
    }
}