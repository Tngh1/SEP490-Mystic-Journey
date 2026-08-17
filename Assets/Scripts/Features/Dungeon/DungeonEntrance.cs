using MysticJourney.API.Endpoints;
using UnityEngine;

// Executes mono behaviour operation.
public class DungeonEntrance : MonoBehaviour
{
    // Executes required level operation.
    public int? RequiredLevel { get; private set; }

    private string dungeonName;

    [SerializeField] private int dungeonConfigId = 1;
    [SerializeField] private int energyCost = 20;
    [SerializeField] private float interactionRadius = 2.5f;

    private bool fetchInFlight;

    // Performs startup initialization for DungeonEntrance on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        var interactable = gameObject.GetComponent<WorldInteractable>() ?? gameObject.AddComponent<WorldInteractable>();
        interactable.ConfigureDungeon(dungeonConfigId, interactionRadius);
        Debug.Log($"[DungeonEntrance] Configured {gameObject.name} as Dungeon Entrance Interactable.");

        FetchConfig();
    }

    // Executes fetch config operation.
    private void FetchConfig()
    {
        if (fetchInFlight || RequiredLevel.HasValue) return;
        fetchInFlight = true;

        DungeonApi.Instance.GetById(dungeonConfigId,
            config =>
            {
                fetchInFlight = false;
                if (config == null) return;
                RequiredLevel = Mathf.Max(1, config.LevelRequirement);
                dungeonName = config.Name;
            },
            error =>
            {
                fetchInFlight = false;
                Debug.LogWarning($"[DungeonEntrance] GetById({dungeonConfigId}) failed: {error.Message}");
            });
    }

    // Executes interact operation.
    public void Interact()
    {
        if (!RequiredLevel.HasValue)
        {
            FetchConfig();
            return;
        }

        if (WorldState.PlayerLevel < RequiredLevel.Value)
        {
            return;
        }

        WorldInteractionPromptRuntime.Hide();

        GameObject targetPanel = null;
        if (UIManager.Instance != null && UIManager.Instance.dungeonPanel != null)
        {
            targetPanel = UIManager.Instance.dungeonPanel;
        }
        else
        {
            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allGos)
            {
                if (obj != null && obj.name == "TeamPanel" && obj.scene.IsValid() && !string.IsNullOrEmpty(obj.scene.name))
                {
                    targetPanel = obj;
                    break;
                }
            }
        }

        if (targetPanel != null)
        {
            if (UIManager.Instance != null && UIManager.Instance.dungeonPanel == targetPanel)
            {
                UIManager.Instance.ShowPanel(targetPanel);
            }
            else
            {
                targetPanel.SetActive(true);
            }

            var lobbyScript = targetPanel.GetComponent<PartyPanel>();
            if (lobbyScript == null)
            {
                lobbyScript = targetPanel.AddComponent<PartyPanel>();
            }
            lobbyScript.OpenForDungeon(dungeonConfigId, "HollowCryptDungeon", energyCost, dungeonName);
        }
        else
        {
            Debug.LogError("[DungeonEntrance] TeamPanel UI GameObject not found!");
        }
    }
}
