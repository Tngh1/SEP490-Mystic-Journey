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

        // Find Player
        var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Knight") ?? GameObject.Find("Mage") ?? GameObject.Find("Archer");
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

        int sessionId = DungeonManager.Instance.CurrentSessionId;
        if (sessionId <= 0)
        {
            Debug.LogWarning($"[DungeonChest] Session ID is {sessionId} (testing/fallback). Cannot claim reward on backend.");
            DungeonManager.Instance.ReturnToWorldMap();
            return;
        }

        Debug.Log($"[DungeonChest] Opening chest for session: {sessionId}...");

        if (MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel.Instance != null)
        {
            MysticJourney.Features.Dungeon.UI.UIDungeonCompletePanel.Instance.ShowPanel(sessionId);
        }
        else
        {
            Debug.LogWarning("[DungeonChest] UIDungeonCompletePanel.Instance not found. Returning to map directly.");
            DungeonManager.Instance.ReturnToWorldMap();
        }
    }
}
