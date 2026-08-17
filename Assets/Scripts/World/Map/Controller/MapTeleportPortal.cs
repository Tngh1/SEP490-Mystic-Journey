using UnityEngine;

// Executes mono behaviour operation.
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

    // Performs startup initialization for MapTeleportPortal on the first active frame.
    // Binds event handlers, initializes UI view elements, and synchronizes initial state values.
    private void Start()
    {
        if (mapSceneController == null)
        {
            mapSceneController = FindFirstObjectByType<MapSceneController>();
        }
    }

    // Executes on trigger enter operation.
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isTeleporting)
        {
            HandleTeleport();
        }
    }

    // Executes on trigger enter2 d operation.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isTeleporting)
        {
            HandleTeleport();
        }
    }

    // Executes handle teleport operation.
    private void HandleTeleport()
    {
        if (isTeleporting) return;

        if (requiredQuestId > 0)
        {
            var requiredQuest = QuestUIManager.Instance?.GetQuestState(requiredQuestId);
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
        if (QuestUIManager.Instance != null)
        {
            var quests = QuestUIManager.Instance.GetMainQuests();
            if (quests != null)
            {
                foreach (var q in quests)
                {
                    if (string.Equals(q.Status, "InProgress", System.StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(q.ObjectiveType, "Explore", System.StringComparison.OrdinalIgnoreCase) &&
                        (q.ObjectiveTarget != null && q.ObjectiveTarget.Contains("Portal", System.StringComparison.OrdinalIgnoreCase)))
                    {
                        QuestUIManager.Instance.AddProgress(q.QuestId, 1);
                        justExplored = true;
                    }
                }
            }
        }

        if (QuestUIManager.Instance != null && !QuestUIManager.Instance.CanEnterMap(targetMapData) && !justExplored)
        {
            string mapTitle = targetMapData.mapName;
            if (mapTitle.Contains(",")) mapTitle = mapTitle.Split(',')[0];

            string msg = $"You have not completed the required quest to enter {mapTitle}.";
            Debug.Log($"MapTeleportPortal: {msg}");

            if (MainQuestPanelRuntime.Instance != null)
                MainQuestPanelRuntime.Instance.ShowPaperPopup(msg, UIPaperPopupView.PaperPopupKind.None);

            isTeleporting = false;
            return;
        }

        Debug.Log($"MapTeleportPortal: Đang dịch chuyển người chơi tới map {targetMapData.mapName}...");

        if (justExplored && QuestUIManager.Instance != null)
            QuestUIManager.Instance.FlushPendingProgressNow();

        if (useSpecificSpawn)
            mapSceneController.EnterMap(targetMapData, false, specificSpawnPosition);
        else
            mapSceneController.EnterMap(targetMapData, false);

    }
}
