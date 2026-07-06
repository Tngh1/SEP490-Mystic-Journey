using UnityEngine;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

public class DungeonChest : MonoBehaviour
{
    [SerializeField] private float interactionRadius = 2.5f;

    private bool hasOpened = false;

    private void Start()
    {
        // Add a trigger collider if not present
        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            var boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector2(1.5f, 1.5f);
        }
        else
        {
            col.isTrigger = true;
        }

        // Add and configure WorldInteractable component dynamically to utilize the prompt system
        var interactable = gameObject.GetComponent<WorldInteractable>() ?? gameObject.AddComponent<WorldInteractable>();
        interactable.ConfigureObject("dungeon_chest", "Reward Chest", "Open", 0, 1, interactionRadius);
        Debug.Log("[DungeonChest] Chest interaction initialized.");
    }

    private void Update()
    {
        if (hasOpened) return;

        // Find Player properly (including Clones)
        var pm = FindFirstObjectByType<PlayerMovement>();
        GameObject player = pm != null ? pm.gameObject : (GameObject.FindWithTag("Player") ?? GameObject.Find("Knight(Clone)") ?? GameObject.Find("Knight"));
        if (player == null) return;

        // Check distance to player
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= interactionRadius)
        {
            // Listen to standard interaction key
            bool interactPressed = false;
            
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                interactPressed = true;
            }

            if (interactPressed)
            {
                OpenChest();
            }
        }
    }

    private void OpenChest()
    {
        if (hasOpened) return;
        hasOpened = true;

        var interactable = GetComponent<WorldInteractable>();
        if (interactable != null) Destroy(interactable);
        
        // Ensure prompt is hidden immediately
        WorldInteractionPromptRuntime.Hide();

        int sessionId = DungeonManager.Instance.CurrentSessionId;
        if (sessionId <= 0)
        {
            Debug.LogWarning($"[DungeonChest] Session ID is {sessionId} (testing/fallback). Cannot claim reward on backend.");
            DungeonManager.Instance.ReturnToWorldMap();
            return;
        }

        Debug.Log($"[DungeonChest] Opening chest for session: {sessionId}...");

        var panel = MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel.Instance;
        if (panel == null)
        {
            // If the panel is in the scene but disabled, Awake hasn't run to set Instance. Find it manually!
            panel = FindFirstObjectByType<MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel>(FindObjectsInactive.Include);
        }

        if (panel != null)
        {
            panel.ShowPanel(sessionId);
        }
        else
        {
            Debug.LogWarning("[DungeonChest] UIDungeonCompletePanel not found anywhere. Returning to map directly.");
            DungeonManager.Instance.ReturnToWorldMap();
        }
    }
}
