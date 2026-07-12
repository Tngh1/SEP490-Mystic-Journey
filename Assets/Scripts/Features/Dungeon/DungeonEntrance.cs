using UnityEngine;

public class DungeonEntrance : MonoBehaviour
{
    [SerializeField] private int dungeonConfigId = 1;
    [SerializeField] private int energyCost = 20;
    [SerializeField] private string dungeonName = "Abandoned Mines";
    [SerializeField] private float interactionRadius = 2.5f;

    private void Start()
    {
        // Add and configure WorldInteractable component dynamically to utilize the prompt system
        var interactable = gameObject.GetComponent<WorldInteractable>() ?? gameObject.AddComponent<WorldInteractable>();
        interactable.ConfigureDungeon(dungeonConfigId, interactionRadius);
        Debug.Log($"[DungeonEntrance] Configured {gameObject.name} as Dungeon Entrance Interactable.");
    }

    public void Interact()
    {
        if (WorldState.PlayerLevel < 5)
        {
            WorldRuntimeEvents.RaiseMessage("Yêu cầu Cấp 5 để vào Dungeon!");
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
            // Fallback to searching the scene
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
            // Activate the panel first so that Awake() runs and UIPartyPanel.Instance is initialized!
            if (UIManager.Instance != null && UIManager.Instance.dungeonPanel == targetPanel)
            {
                UIManager.Instance.ShowPanel(targetPanel);
            }
            else
            {
                targetPanel.SetActive(true);
            }

            var lobbyScript = targetPanel.GetComponent<UIPartyPanel>();
            if (lobbyScript == null)
            {
                lobbyScript = targetPanel.AddComponent<UIPartyPanel>();
            }
            // The dungeon scene name is now hardcoded since the game uses one common scene
            lobbyScript.OpenForDungeon(dungeonConfigId, "HollowCryptDungeon", energyCost, dungeonName);
        }
        else
        {
            Debug.LogError("[DungeonEntrance] TeamPanel UI GameObject not found!");
        }
    }
}
