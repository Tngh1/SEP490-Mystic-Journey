using UnityEngine;

public class MapTeleportPortal : MonoBehaviour
{
    [Header("Portal Settings")]
    [Tooltip("Dữ liệu của Map muốn dịch chuyển tới khi chạm vào cổng này")]
    public MapData targetMapData;
    
    [Tooltip("Reference tới MapSceneController (nếu để trống sẽ tự tìm trong scene)")]
    public MapSceneController mapSceneController;

    private void Start()
    {
        if (mapSceneController == null)
        {
            mapSceneController = FindObjectOfType<MapSceneController>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem đối tượng va chạm có phải là Player không
        if (other.CompareTag("Player"))
        {
            if (targetMapData == null)
            {
                Debug.LogWarning("MapTeleportPortal: Chưa gán targetMapData!");
                return;
            }

            if (mapSceneController == null)
            {
                Debug.LogError("MapTeleportPortal: Không tìm thấy MapSceneController trong scene!");
                return;
            }

            // Kiểm tra xem người chơi đã đủ điều kiện (hoàn thành quest) để vào map này chưa
            if (QuestManager.Instance != null && !QuestManager.Instance.CanEnterMap(targetMapData))
            {
                Debug.Log($"MapTeleportPortal: Bạn chưa hoàn thành nhiệm vụ để mở khóa map {targetMapData.mapName}.");
                // TODO: Hiển thị thông báo UI cho người chơi nếu cần
                return;
            }

            Debug.Log($"MapTeleportPortal: Đang dịch chuyển người chơi tới map {targetMapData.mapName}...");
            
            // Gọi hàm EnterMap để tiến hành load map
            mapSceneController.EnterMap(targetMapData);
        }
    }
}
