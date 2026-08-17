using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using UnityEngine;

// Executes mono behaviour operation.
public class WorldNpcSpawnerRuntime : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Database chứa mapping giữa Type (trong DB) và Prefab")]
    [SerializeField] private NpcDatabaseSO npcDatabase;

    [Tooltip("Nơi chứa các GameObject NPC sau khi đẻ ra (để Hierarchy gọn gàng).")]
    [SerializeField] private Transform npcContainer;

    private readonly List<GameObject> spawnedNpcs = new List<GameObject>();

    // Performs startup initialization for WorldNpcSpawnerRuntime on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        WorldRuntimeEvents.MapChanged += OnMapChanged;

        SpawnNpcsForCurrentMap();
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        WorldRuntimeEvents.MapChanged -= OnMapChanged;
    }

    // Executes on map changed operation.
    private void OnMapChanged(string mapName)
    {
        SpawnNpcsForCurrentMap();
    }

    // Executes spawn npcs for current map operation.
    public void SpawnNpcsForCurrentMap()
    {
        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[WorldNpcSpawner] Không có token, không thể tải danh sách NPC.");
            return;
        }

        WorldApi.Instance.GetState(
            state =>
            {
                if (this == null) return;
                if (state != null && state.Npcs != null)
                {
                    ClearCurrentNpcs();
                    SpawnNpcList(state.Npcs);
                }
            },
            error =>
            {
                if (this == null) return;
                Debug.LogError($"[WorldNpcSpawner] Lỗi tải NPC: {error.Message}");
            }
        );
    }

    // Executes spawn npc list operation.
    private void SpawnNpcList(List<NPCResponse> npcList)
    {
        if (this == null) return;
        Transform parentTransform = (npcContainer != null) ? npcContainer : this.transform;
        var hideNatalie = ShouldHideNatalie();

        foreach (var npc in npcList)
        {
            if (hideNatalie && string.Equals(npc.Name, "Natalie", System.StringComparison.OrdinalIgnoreCase))
                continue;

            GameObject prefab = npcDatabase != null ? npcDatabase.GetPrefab(npc.Name) : null;

            if (prefab != null && prefab.name == "VikingRobber" && npc.Name == "Valiant Warrior")
            {
                var overridePrefab = Resources.Load<GameObject>("NPCs/QuestGiver");
                if (overridePrefab == null)
                    overridePrefab = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "Lieutenant" && g.scene.name == null);

                if (overridePrefab != null)
                {
                    prefab = overridePrefab;
                    Debug.Log("[WorldNpcSpawner] Đã ép Valiant Warrior dùng prefab NPC chuẩn thay vì VikingRobber (quái).");
                }
            }

            if (prefab == null)
            {
                Debug.LogWarning($"[WorldNpcSpawner] Không tìm thấy Prefab cho NPC Name: '{npc.Name}'. Hãy kiểm tra file NpcDatabaseSO!");
                continue;
            }

            Vector3 localPos = new Vector3((float)npc.PositionX, (float)npc.PositionY, 0f);

            GameObject npcObj = Instantiate(prefab, parentTransform);
            npcObj.name = npc.Name;

            StripEnemyAi(npcObj);

            npcObj.transform.localPosition = localPos;
            npcObj.transform.localRotation = Quaternion.identity;
            npcObj.transform.localScale = Vector3.one;

            spawnedNpcs.Add(npcObj);

            WorldInteractable interactable = npcObj.GetComponent<WorldInteractable>();
            if (interactable == null)
            {
                interactable = npcObj.AddComponent<WorldInteractable>();
            }

            var linkedQuests = npc.Dialogues != null
                ? System.Linq.Enumerable.Select(
                    System.Linq.Enumerable.Where(npc.Dialogues, d => d != null && d.LinkedQuestId.HasValue),
                    d => d.LinkedQuestId.Value)
                : null;

            interactable.ConfigureNpc(
                npc.NPCId,
                npc.Name,
                npc.Description,
                "Xin chào lữ khách!",
                npc.InteractionRadius > 0 ? npc.InteractionRadius : 2.5f,
                linkedQuests
            );

            Debug.Log($"[WorldNpcSpawner] Đã spawn NPC {npc.Name} (ID: {npc.NPCId}) tại localPosition {localPos}");
        }

    }


    // Executes strip enemy ai operation.
    private static void StripEnemyAi(GameObject npcObj)
    {
        RemoveAll<EnemyAnimations>(npcObj);
        RemoveAll<EnemyBehaviour>(npcObj);
        RemoveAll<EnemyEntity>(npcObj);

        RemoveAll<UnityEngine.AI.NavMeshAgent>(npcObj);
    }

    // Executes behaviour operation.
    private static void RemoveAll<T>(GameObject root) where T : Behaviour
    {
        foreach (var component in root.GetComponentsInChildren<T>(true))
        {
            if (component == null) continue;
            component.enabled = false;
            Destroy(component);
        }
    }

    private const int NatalieRestQuestId = 33;

    // Executes should hide natalie operation.
    private static bool ShouldHideNatalie()
    {
        var quests = QuestUIManager.Instance?.GetMainQuests();
        return quests != null && quests.Any(q =>
            q != null && q.QuestId == NatalieRestQuestId &&
            string.Equals(q.Status, "Claimed", System.StringComparison.OrdinalIgnoreCase));
    }

    // Executes clear current npcs operation.
    private void ClearCurrentNpcs()
    {
        foreach (var npc in spawnedNpcs)
        {
            if (npc != null)
            {
                Destroy(npc);
            }
        }
        spawnedNpcs.Clear();
    }
}
