using UnityEngine;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;

// Executes mono behaviour operation.
public class DungeonChest : MonoBehaviour
{
    [SerializeField] private float interactionRadius = 2.5f;

    private bool hasOpened = false;

    // Performs startup initialization for DungeonChest on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
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

        var interactable = gameObject.GetComponent<WorldInteractable>() ?? gameObject.AddComponent<WorldInteractable>();
        interactable.ConfigureObject("dungeon_chest", "Reward Chest", "Open", 0, 1, interactionRadius);
        Debug.Log("[DungeonChest] Chest interaction initialized.");
    }

    // Per-frame update loop for DungeonChest.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (hasOpened) return;

        GameObject player = null;
        if (NetworkPlayer.Local != null && NetworkPlayer.Local.gameObject != null)
        {
            player = NetworkPlayer.Local.gameObject;
        }
        else
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null) player = pm.gameObject;
        }

        if (player == null) return;

        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (dist <= interactionRadius)
        {
            var input = GameplayInputProvider.Local;
            if (input != null && input.InteractPressed)
            {
                OpenChest();
            }
        }
    }

    // Executes open chest operation.
    private void OpenChest()
    {
        if (hasOpened) return;
        hasOpened = true;

        var interactable = GetComponent<WorldInteractable>();
        if (interactable != null) Destroy(interactable);

        WorldInteractionPromptRuntime.Hide();

        int sessionId = DungeonManager.Instance.CurrentSessionId;
        if (sessionId <= 0)
        {
            Debug.LogWarning($"[DungeonChest] Session ID is {sessionId} (testing/fallback). Will show complete panel without claiming rewards.");
        }

        Debug.Log($"[DungeonChest] Opening chest for session: {sessionId}...");

        var panel = MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel.Instance;
        if (panel == null)
        {
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
