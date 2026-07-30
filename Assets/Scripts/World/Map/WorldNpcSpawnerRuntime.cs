using System.Collections.Generic;
using System.Linq;
using MysticJourney.API.Core;
using MysticJourney.API.Endpoints;
using MysticJourney.API.Models.Response;
using UnityEngine;

public class WorldNpcSpawnerRuntime : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Database chứa mapping giữa Type (trong DB) và Prefab")]
    [SerializeField] private NpcDatabaseSO npcDatabase;
    
    [Tooltip("Nơi chứa các GameObject NPC sau khi đẻ ra (để Hierarchy gọn gàng).")]
    [SerializeField] private Transform npcContainer;
    
    // Lưu danh sách NPC đã sinh ra để dọn dẹp khi đổi Map
    private readonly List<GameObject> spawnedNpcs = new List<GameObject>();

    private void Start()
    {
        // Nếu có sự kiện đổi map trong cùng 1 scene thì gọi lại hàm này
        WorldRuntimeEvents.MapChanged += OnMapChanged;
        
        // Sinh NPC ngay khi object này được load
        SpawnNpcsForCurrentMap();
    }

    private void OnDestroy()
    {
        WorldRuntimeEvents.MapChanged -= OnMapChanged;
    }

    private void OnMapChanged(string mapName)
    {
        SpawnNpcsForCurrentMap();
    }

    public void SpawnNpcsForCurrentMap()
    {
        if (!ApiClient.Instance.HasToken())
        {
            Debug.LogWarning("[WorldNpcSpawner] Không có token, không thể tải danh sách NPC.");
            return;
        }

        // Gọi API lấy trạng thái World hiện tại (có chứa danh sách Npcs của Map đó)
        WorldApi.Instance.GetState(
            state => 
            {
                if (state != null && state.Npcs != null)
                {
                    ClearCurrentNpcs();
                    SpawnNpcList(state.Npcs);
                }
            },
            error => 
            {
                Debug.LogError($"[WorldNpcSpawner] Lỗi tải NPC: {error.Message}");
            }
        );
    }

    private void SpawnNpcList(List<NPCResponse> npcList)
    {
        Transform parentTransform = npcContainer != null ? npcContainer : this.transform;
        var hideNatalie = ShouldHideNatalie();

        foreach (var npc in npcList)
        {
            if (hideNatalie && string.Equals(npc.Name, "Natalie", System.StringComparison.OrdinalIgnoreCase))
                continue;

            // 1. Tìm Prefab từ NpcDatabaseSO
            GameObject prefab = npcDatabase != null ? npcDatabase.GetPrefab(npc.Name) : null;
            
            // Xử lý cứng cho Valiant Warrior để tránh việc nó bị nhầm thành Quái (Enemy)
            if (prefab != null && prefab.name == "VikingRobber" && npc.Name == "Valiant Warrior")
            {
                var overridePrefab = Resources.Load<GameObject>("NPCs/QuestGiver");
                if (overridePrefab == null)
                    overridePrefab = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "Lieutenant" && g.scene.name == null); // search prefab
                
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

            // 2. Tọa độ (PositionX, PositionY) trong DB là localPosition tương đối so với NPC Container/Map
            Vector3 localPos = new Vector3((float)npc.PositionX, (float)npc.PositionY, 0f);

            // 3. Đẻ ra NPC và gán localPosition chuẩn xác
            GameObject npcObj = Instantiate(prefab, parentTransform);
            npcObj.name = npc.Name;
            npcObj.transform.localPosition = localPos;
            npcObj.transform.localRotation = Quaternion.identity;
            npcObj.transform.localScale = Vector3.one;

            spawnedNpcs.Add(npcObj);

            // 4. Ghi đè cấu hình cho NPC bằng dữ liệu từ Backend
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
        
        // NPC Spawner completed
    }


    private static bool ShouldHideNatalie()
    {
        var quests = QuestManager.Instance?.GetMainQuests();
        return quests != null && quests.Any(q =>
            q != null && q.QuestId == 23 &&
            (string.Equals(q.Status, "Completed", System.StringComparison.OrdinalIgnoreCase) ||
             string.Equals(q.Status, "Claimed", System.StringComparison.OrdinalIgnoreCase)));
    }

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
