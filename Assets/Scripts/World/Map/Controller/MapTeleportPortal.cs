using UnityEngine;

public class MapTeleportPortal : MonoBehaviour
{
    [Header("Portal Settings")]
    [Tooltip("Dữ liệu của Map muốn dịch chuyển tới khi chạm vào cổng này")]
    public MapData targetMapData;
    
    [Tooltip("Sử dụng vị trí spawn cụ thể (tránh bị lỗi 50 50 hoặc dùng điểm spawn mặc định)")]
    public bool useSpecificSpawn = false;
    public Vector3 specificSpawnPosition;
    
    [Tooltip("Reference tới MapSceneController (nếu để trống sẽ tự tìm trong scene)")]
    public MapSceneController mapSceneController;

    [Tooltip("Quest phải hoàn thành trước khi portal hoạt động. 0 = không khóa theo quest.")]
    [SerializeField] private int requiredQuestId;

    private bool isTeleporting = false;

    private void Start()
    {
        if (mapSceneController == null)
        {
            mapSceneController = FindFirstObjectByType<MapSceneController>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem đối tượng va chạm có phải là Player không (3D)
        if (other.CompareTag("Player") && !isTeleporting)
        {
            HandleTeleport();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Kiểm tra xem đối tượng va chạm có phải là Player không (2D)
        if (other.CompareTag("Player") && !isTeleporting)
        {
            HandleTeleport();
        }
    }

    private void HandleTeleport()
    {
        if (isTeleporting) return;

        if (requiredQuestId > 0)
        {
            // Portal gating belongs to gameplay state, not the normalized UI response list.
            var requiredQuest = QuestManager.Instance?.GetQuestState(requiredQuestId);
            if (requiredQuest == null ||
                !string.Equals(requiredQuest.status, "Claimed", System.StringComparison.OrdinalIgnoreCase))
            {
                WorldRuntimeEvents.RaiseMessage("Speak with the Elf Guard and finish your business on the island first.");
                return;
            }
        }


        isTeleporting = true;
        
        if (targetMapData == null)
        {
            Debug.LogWarning("MapTeleportPortal: Chưa gán targetMapData!");
            isTeleporting = false;
            return;
        }

        // Additive scenes can start before the persistent controller is ready. Resolve again
        // when the player enters the portal instead of relying only on Start().
        if (mapSceneController == null)
            mapSceneController = FindFirstObjectByType<MapSceneController>();

        if (mapSceneController == null)
        {
            Debug.LogError("MapTeleportPortal: MapSceneController is unavailable; teleport cancelled.");
            WorldRuntimeEvents.RaiseMessage("The portal is not ready. Please try again.");
            isTeleporting = false;
            return;
        }


        bool justExplored = false;
        // Try to update any "Explore" objective related to portals before checking entry condition
        if (QuestManager.Instance != null)
        {
            var quests = QuestManager.Instance.GetMainQuests();
            if (quests != null)
            {
                foreach (var q in quests)
                {
                    if (string.Equals(q.Status, "InProgress", System.StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(q.ObjectiveType, "Explore", System.StringComparison.OrdinalIgnoreCase) &&
                        (q.ObjectiveTarget != null && q.ObjectiveTarget.Contains("Portal", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        QuestManager.Instance.AddProgress(q.QuestId, 1);
                        justExplored = true;
                        // KHÔNG popup ở đây: "Explored: X" không chứa từ khoá nào nên InferKind trả None
                        // -> PaperPopup sẽ hiện một thông báo không có loại cụ thể dù vừa hoàn thành mục tiêu.
                        // BatchSyncLoop sẽ Complete + Claim và bắn popup "Reward Claimed!" duy nhất.
                    }
                }
            }
        }

        // Kiểm tra xem người chơi đã đủ điều kiện (hoàn thành quest) để vào map này chưa
        if (QuestManager.Instance != null && !QuestManager.Instance.CanEnterMap(targetMapData) && !justExplored)
        {
            string mapTitle = targetMapData.mapName;
            if (mapTitle.Contains(",")) mapTitle = mapTitle.Split(',')[0]; // Cleanup weird map names
            
            string msg = $"You have not completed the required quest to enter {mapTitle}.";
            Debug.Log($"MapTeleportPortal: {msg}");
            
            // Kind None tường minh: msg chứa chữ "completed" nên InferKind sẽ đoán sai thành
            // "Quest Completed!" + stamp xanh, dù đây là thông báo CHẶN không cho vào map.
            if (MainQuestPanelRuntime.Instance != null)
                MainQuestPanelRuntime.Instance.ShowPaperPopup(msg, UIPaperPopupView.PaperPopupKind.None);
                
            isTeleporting = false;
            return;
        }

        Debug.Log($"MapTeleportPortal: Đang dịch chuyển người chơi tới map {targetMapData.mapName}...");

        // Đẩy progress Explore lên server NGAY, trước khi scene unload. BatchSyncLoop chỉ
        // tick mỗi 1s; nếu chưa kịp tick thì LoadMyQuests của map mới sẽ xoá _pendingBatch
        // và quest "đi qua cổng" mắc kẹt ở InProgress mãi.
        if (justExplored && QuestManager.Instance != null)
            QuestManager.Instance.FlushPendingProgressNow();

        // Gọi hàm EnterMap để tiến hành load map (không dùng cache vì qua cổng phải ra đúng cổng)
        if (useSpecificSpawn)
            mapSceneController.EnterMap(targetMapData, false, specificSpawnPosition);
        else
            mapSceneController.EnterMap(targetMapData, false);
            
        // Do not reset isTeleporting to false here because the scene is about to unload.
        // If we reset it, another collision could trigger it again before the unload finishes.
    }
}
