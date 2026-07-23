using UnityEngine;
using System.Linq;
using MysticJourney.API.Models.Response;
using MysticJourney.Features.Quest;
using MysticJourney.Core.Utilities;

public class QuestWaypointArrow : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private int currentQuestId = -1;
    private Transform currentTarget;
    private float nextSearchTime = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        var obj = new GameObject("QuestWaypointArrow");
        DontDestroyOnLoad(obj);
        obj.AddComponent<QuestWaypointArrow>();
    }

    private void Awake()
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GenerateArrowSprite();
        // A nice bright, slightly transparent gold/orange color
        spriteRenderer.color = new Color(1f, 0.8f, 0.2f, 0.85f);
        spriteRenderer.sortingOrder = 9999;
    }

    private void Update()
    {
        var player = PlayerMovement.Instance;
        if (player == null)
        {
            spriteRenderer.enabled = false;
            return;
        }

        if (!QuestWaypointManager.IsTrackingEnabled)
        {
            spriteRenderer.enabled = false;
            return;
        }

        var qm = QuestManager.Instance;
        if (qm == null)
        {
            spriteRenderer.enabled = false;
            return;
        }

        // Delegate target finding to QuestWaypointManager which has robust target lookup
        if (QuestWaypointManager.Instance != null)
        {
            currentTarget = QuestWaypointManager.Instance.GetTargetForActiveQuest();
        }
        else
        {
            var responses = qm.GetAllResponses();
            if (responses == null)
            {
                spriteRenderer.enabled = false;
                return;
            }

            var activeQuest = QuestUtils.PickPreferredQuest(responses.Values);
            if (activeQuest == null || string.Equals(activeQuest.ObjectiveType, "EquipSkill", System.StringComparison.OrdinalIgnoreCase))
            {
                spriteRenderer.enabled = false;
                return;
            }

            if (activeQuest.QuestId != currentQuestId || Time.time >= nextSearchTime || currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                currentQuestId = activeQuest.QuestId;
                currentTarget = FindTargetFallback(activeQuest, player.transform.position);
                nextSearchTime = Time.time + 1.0f;
            }
        }

        if (currentTarget == null)
        {
            spriteRenderer.enabled = false;
            return;
        }

        float dist = Vector2.Distance(player.transform.position, currentTarget.position);
        if (dist < 2.0f)
        {
            spriteRenderer.enabled = false; // Too close, hide arrow
            return;
        }

        spriteRenderer.enabled = true;

        Vector2 dir = (currentTarget.position - player.transform.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        
        // Offset around player (radius 1.5 units)
        transform.position = player.transform.position + (Vector3)dir * 1.5f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        
        // Add a slight bouncing animation back and forth along the direction
        float bounce = Mathf.Sin(Time.time * 6f) * 0.2f;
        transform.position += (Vector3)dir * bounce;
    }

    private Transform FindTargetFallback(PlayerQuestResponse quest, Vector3 playerPos)
    {
        string targetName = quest.ObjectiveTarget ?? "";
        string questGiver = quest.QuestGiverName ?? "";

        // 1. Match NPC by name or LinkedQuestIds
        var interactables = FindObjectsByType<WorldInteractable>(FindObjectsSortMode.None);
        foreach (var i in interactables)
        {
            if (!i.gameObject.activeInHierarchy) continue;

            if (i.Kind == WorldInteractableKind.Npc)
            {
                if ((!string.IsNullOrWhiteSpace(targetName) && i.DisplayName.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(questGiver) && i.DisplayName.IndexOf(questGiver, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (i.LinkedQuestIds != null && i.LinkedQuestIds.Contains(quest.QuestId)))
                {
                    return i.transform;
                }
            }
            else if (i.QuestId.HasValue && i.QuestId.Value == quest.QuestId)
            {
                return i.transform;
            }
        }

        // 2. Match specific Enemy by targetName for Defeat quest
        if (string.Equals(quest.ObjectiveType, "Defeat", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(targetName))
        {
            var enemies = FindObjectsByType<EnemyEntity>(FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy.gameObject.activeInHierarchy && enemy.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return enemy.transform;
                }
            }
        }

        return null;
    }

    // Generates a 32x32 pixel art arrow pointing right (0 degrees)
    private Sprite GenerateArrowSprite()
    {
        int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 fill = new Color32(255, 255, 255, 255);
        Color32 outline = new Color32(0, 0, 0, 150);

        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Tail
                if (x >= 4 && x <= 16 && y >= 12 && y <= 20)
                {
                    pixels[y * size + x] = fill;
                    if (x == 4 || y == 12 || y == 20) pixels[y * size + x] = outline;
                }
                
                // Head
                if (x >= 16 && x <= 28)
                {
                    int halfHeight = 28 - x; // at x=16, height=12. at x=28, height=0
                    if (y >= 16 - halfHeight && y <= 16 + halfHeight)
                    {
                        pixels[y * size + x] = fill;
                        if (y == 16 - halfHeight || y == 16 + halfHeight || (x == 16 && (y < 12 || y > 20)))
                            pixels[y * size + x] = outline;
                    }
                }
            }
        }
        
        pixels[16 * size + 29] = outline; // tip outline

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }
}
