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

    private bool isTeleporting = false;

    private void Start()
    {
        if (mapSceneController == null)
        {
            mapSceneController = FindObjectOfType<MapSceneController>();
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
        isTeleporting = true;
        
        if (targetMapData == null)
        {
            Debug.LogWarning("MapTeleportPortal: Chưa gán targetMapData!");
            isTeleporting = false;
            return;
        }

        if (mapSceneController == null)
        {
            Debug.LogError("MapTeleportPortal: Không tìm thấy MapSceneController trong scene!");
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
                        if (MainQuestPanelRuntime.Instance != null)
                            MainQuestPanelRuntime.Instance.ShowQuestPopup($"Explored: {q.ObjectiveTarget}");
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
            
            if (MainQuestPanelRuntime.Instance != null)
                MainQuestPanelRuntime.Instance.ShowQuestPopup(msg);
                
            isTeleporting = false;
            return;
        }

        Debug.Log($"MapTeleportPortal: Đang dịch chuyển người chơi tới map {targetMapData.mapName}...");
        
        // Gọi hàm EnterMap để tiến hành load map (không dùng cache vì qua cổng phải ra đúng cổng)
        if (useSpecificSpawn)
            mapSceneController.EnterMap(targetMapData, false, specificSpawnPosition);
        else
            mapSceneController.EnterMap(targetMapData, false);
            
        // Do not reset isTeleporting to false here because the scene is about to unload.
        // If we reset it, another collision could trigger it again before the unload finishes.
    }
}
