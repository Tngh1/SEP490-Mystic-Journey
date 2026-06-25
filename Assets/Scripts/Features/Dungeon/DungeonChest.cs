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
            if (Input.GetKeyDown(KeyCode.E))
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
            Debug.LogWarning($"[DungeonChest] Session ID is {sessionId} (testing/fallback). Opening fallback chest panel.");
            ShowFallbackReward();
            return;
        }

        Debug.Log($"[DungeonChest] Claiming reward for session: {sessionId}...");

        DungeonApi.Instance.ClaimReward(sessionId,
            onSuccess: response =>
            {
                if (response.Success && response.Data != null)
                {
                    Debug.Log($"[DungeonChest] Reward claimed: Gold={response.Data.GoldEarned}, XP={response.Data.ExperienceEarned}");
                    
                    // Show reward panel
                    if (UIChestRewardPanel.Instance != null)
                    {
                        UIChestRewardPanel.Instance.ShowRewards(
                            "Exploration Successful",
                            response.Data.GoldEarned,
                            response.Data.ExperienceEarned,
                            response.Data.Items,
                            onConfirm: () =>
                            {
                                // After closing the panel, return to world map
                                DungeonManager.Instance.ReturnToWorldMap();
                            }
                        );
                    }
                    else
                    {
                        Debug.LogWarning("[DungeonChest] UIChestRewardPanel.Instance not found. Returning to map directly.");
                        DungeonManager.Instance.ReturnToWorldMap();
                    }
                }
                else
                {
                    Debug.LogWarning("[DungeonChest] ClaimReward API succeeded but returned failure. Showing fallback reward panel.");
                    ShowFallbackReward();
                }
            },
            onError: error =>
            {
                Debug.LogWarning($"[DungeonChest] ClaimReward API failed: {error.Message}. Showing fallback reward panel.");
                ShowFallbackReward();
            }
        );
    }

    private void ShowFallbackReward()
    {
        if (UIChestRewardPanel.Instance != null)
        {
            UIChestRewardPanel.Instance.ShowRewards(
                "Exploration Successful",
                100, // mock gold
                50,  // mock xp
                null, // no items
                onConfirm: () =>
                {
                    // After closing the panel, return to world map
                    DungeonManager.Instance.ReturnToWorldMap();
                }
            );
        }
        else
        {
            Debug.LogWarning("[DungeonChest] UIChestRewardPanel.Instance not found. Returning to map directly.");
            DungeonManager.Instance.ReturnToWorldMap();
        }
    }
}
