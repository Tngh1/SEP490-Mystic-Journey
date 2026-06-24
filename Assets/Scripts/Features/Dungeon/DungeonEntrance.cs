using UnityEngine;

public class DungeonEntrance : MonoBehaviour
{
    [SerializeField] private int dungeonConfigId = 1;
    [SerializeField] private string dungeonSceneName = "AbandonedMines";
    [SerializeField] private int energyCost = 20;
    [SerializeField] private string dungeonName = "Abandoned Mines";

    private bool isTriggered = false;

    private void Start()
    {
        // Ensure this GameObject has a Collider2D configured as a trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
        {
            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(2.5f, 2.5f); // Generous trigger area for easy testing
            Debug.Log($"[DungeonEntrance] Automatically added BoxCollider2D (IsTrigger=true) to {gameObject.name}");
        }
        else
        {
            col.isTrigger = true;
            Debug.Log($"[DungeonEntrance] Set existing Collider2D on {gameObject.name} to Trigger");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isTriggered) return;

        // Robust check for player including child colliders and names
        bool isPlayer = collision.CompareTag("Player") || 
                       collision.GetComponentInParent<PlayerMovement>() != null || 
                       collision.gameObject.name.Contains("Knight") || 
                       collision.gameObject.name.Contains("Mage") || 
                       collision.gameObject.name.Contains("Archer") ||
                       collision.transform.root.CompareTag("Player") ||
                       collision.transform.root.name.Contains("Knight") ||
                       collision.transform.root.name.Contains("Mage") ||
                       collision.transform.root.name.Contains("Archer");

        if (isPlayer)
        {
            isTriggered = true;
            Debug.Log($"[DungeonEntrance] Player ({collision.gameObject.name}) entered trigger. Teleporting directly to: {dungeonSceneName}");
            
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.StartDungeon(dungeonConfigId, dungeonSceneName, energyCost, dungeonName);
            }
            else
            {
                Debug.LogError("[DungeonEntrance] DungeonManager.Instance is null!");
                isTriggered = false; // Reset to allow retry
            }
        }
    }

    private void OnEnable()
    {
        isTriggered = false;
    }
}
