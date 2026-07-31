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

            // Gỡ NavMeshAgent + AI quái TRƯỚC khi gán vị trí. Bắt buộc đúng thứ tự này —
            // xem StripEnemyAi: agent giữ quyền điều khiển transform nên gán localPosition
            // sau khi agent đã bật thì bị agent ghi đè, NPC trôi về chỗ khác.
            StripEnemyAi(npcObj);

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


    /// <summary>
    /// Gỡ NavMeshAgent + AI quái khỏi một NPC vừa spawn, TRƯỚC khi gán localPosition.
    ///
    /// Triệu chứng nó chữa: mọi NPC trên map dồn về gần như cùng một chỗ, không đứng đúng toạ độ
    /// trong DB (báo cáo ở FrozenMountain 2026-07-31).
    ///
    /// Nguyên nhân: 12/14 prefab trong NpcDatabase là prefab QUÁI của PixelWorld — có
    /// NavMeshAgent + EnemyEntity + EnemyBehaviour + EnemyAnimations (chỉ Witch/Zephyr và
    /// ElfGuardIdle/Elf Guard là sạch). Khi NavMeshAgent đang bật, NÓ nắm quyền điều khiển
    /// transform: gán transform.localPosition sau đó bị agent ghi đè ở lần update kế tiếp, và
    /// agent còn tự warp object về điểm gần nhất trên NavMesh đã bake. Kết quả là NPC nằm ở chỗ
    /// agent quyết định (quanh gốc container + offset sẵn trong prefab — nhiều prefab dùng chung
    /// đúng một offset), không phải chỗ ta gán.
    ///
    /// Lưu ý: KHÔNG phải do AI đuổi người chơi. Cả 3 prefab đã kiểm đều có isChasingEnemy = 0 và
    /// isAttackingEnemy = 0 nên không bao giờ vào State.Chasing. Nhưng EnemyBehaviour vẫn chạy
    /// Roaming (BlueGuard/Lieutenant có startingState = 1 = Roaming) nên vẫn tự đi lang thang —
    /// gỡ luôn cho chắc.
    ///
    /// Phải gỡ CẢ BỘ, không gỡ lẻ: EnemyAnimations.Update() deref enemyBehaviour/enemyEntity mà
    /// không kiểm null, nên gỡ EnemyBehaviour một mình sẽ đổi "NPC tự đi" thành
    /// NullReferenceException mỗi frame. Gỡ ở runtime, KHÔNG sửa prefab: các prefab này vẫn đang
    /// được dùng làm quái thật ở chỗ khác.
    /// </summary>
    private static void StripEnemyAi(GameObject npcObj)
    {
        // Thứ tự quan trọng: EnemyAnimations nằm trên GameObject con ("Graphics") và giữ tham chiếu
        // tới EnemyBehaviour/EnemyEntity ở root, nên phải gỡ nó TRƯỚC hai cái kia.
        RemoveAll<EnemyAnimations>(npcObj);
        RemoveAll<EnemyBehaviour>(npcObj);
        RemoveAll<EnemyEntity>(npcObj);

        // Cái này mới là thủ phạm chính của việc NPC sai vị trí.
        RemoveAll<UnityEngine.AI.NavMeshAgent>(npcObj);
    }

    // Tắt trước rồi mới Destroy: Destroy chỉ xoá component ở cuối frame, nên nếu không tắt thì
    // Start()/Update() của AI vẫn kịp chạy một nhịp và đẩy NPC đi một đoạn.
    // Không dùng DestroyImmediate — Unity khuyến cáo không gọi nó ở runtime.
    private static void RemoveAll<T>(GameObject root) where T : Behaviour
    {
        foreach (var component in root.GetComponentsInChildren<T>(true))
        {
            if (component == null) continue;
            component.enabled = false;
            Destroy(component);
        }
    }

    // Natalie chỉ biến mất SAU khi được an nghỉ — quest "[Chapter 4] Lay Natalie to Rest"
    // (AbandonedCastle, ObjectiveTarget = "Ivy Tree").
    //
    // Trước đây hằng này là 23, một quest ở FrozenMountain ("Dragons of Snow") hoàn toàn không liên
    // quan: Natalie bị ẩn ngay khi người chơi hạ rồng băng ở map khác, còn quest an nghỉ của cô thì
    // không ẩn được ai. Số cũ đã lệch từ trước lần chèn quest này.
    private const int NatalieRestQuestId = 33;

    private static bool ShouldHideNatalie()
    {
        var quests = QuestManager.Instance?.GetMainQuests();
        return quests != null && quests.Any(q =>
            q != null && q.QuestId == NatalieRestQuestId &&
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
