using MysticJourney.API.Core;
using UnityEngine;

public class DungeonEntrance : MonoBehaviour
{
    /// <summary>
    /// Level tối thiểu để vào Dungeon. Nguồn duy nhất — WorldInteractable.GetPromptText()
    /// đọc chính hằng số này. Trước đây số 5 bị hardcode ở CẢ HAI chỗ, nên sửa một bên
    /// là prompt và cửa gate lệch nhau (prompt báo một mức, gate chặn ở mức khác).
    /// </summary>
    public const int RequiredLevel = 3;

    [SerializeField] private int dungeonConfigId = 1;
    [SerializeField] private int energyCost = 20;
    [SerializeField] private string dungeonName = "Abandoned Mines";
    [SerializeField] private float interactionRadius = 2.5f;

    private void Start()
    {
        // Add and configure WorldInteractable component dynamically to utilize the prompt system
        var interactable = gameObject.GetComponent<WorldInteractable>() ?? gameObject.AddComponent<WorldInteractable>();
        interactable.ConfigureDungeon(dungeonConfigId, interactionRadius);
        Debug.Log($"[DungeonEntrance] Configured {gameObject.name} as Dungeon Entrance Interactable.");
    }

    public void Interact()
    {
        // Đọc level từ CẢ WorldState và PlayerPrefs rồi lấy giá trị lớn hơn.
        // WorldState.PlayerLevel là in-memory và có thể còn là 1 (giá trị khởi tạo của
        // GameStateService) nếu GetMyProfile chưa trả về, hoặc nếu người chơi lên level
        // trong session này mà chưa có ai đồng bộ lại — WorldRuntimeEvents.LevelChanged
        // KHÔNG có nơi nào gọi Raise, nên không có tín hiệu nào cập nhật nó. Đó là lý do
        // gate chặn cả người chơi đã đủ level.
        var level = Mathf.Max(
            WorldState.PlayerLevel,
            PlayerPrefs.GetInt(ApiConfig.PlayerLevelKey, 1));

        if (level < RequiredLevel)
        {
            // Không cần báo gì thêm ở đây: PlayerWorldInteractor.Update() gọi
            // WorldInteractionPromptRuntime.Show(GetPromptText()) MỖI FRAME khi người chơi
            // đứng trong bán kính, và GetPromptText() đã hiện "Requires Level 3..." rồi.
            // (RaiseMessage cũ vô dụng: WorldRuntimeEvents.Message không có subscriber nào.)
            return;
        }

        WorldInteractionPromptRuntime.Hide();
        
        GameObject targetPanel = null;
        if (UIManager.Instance != null && UIManager.Instance.dungeonPanel != null)
        {
            targetPanel = UIManager.Instance.dungeonPanel;
        }
        else
        {
            // Fallback to searching the scene
            var allGos = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allGos)
            {
                if (obj != null && obj.name == "TeamPanel" && obj.scene.IsValid() && !string.IsNullOrEmpty(obj.scene.name))
                {
                    targetPanel = obj;
                    break;
                }
            }
        }

        if (targetPanel != null)
        {
            // Activate the panel first so that Awake() runs and UIPartyPanel.Instance is initialized!
            if (UIManager.Instance != null && UIManager.Instance.dungeonPanel == targetPanel)
            {
                UIManager.Instance.ShowPanel(targetPanel);
            }
            else
            {
                targetPanel.SetActive(true);
            }

            var lobbyScript = targetPanel.GetComponent<UIPartyPanel>();
            if (lobbyScript == null)
            {
                lobbyScript = targetPanel.AddComponent<UIPartyPanel>();
            }
            // The dungeon scene name is now hardcoded since the game uses one common scene
            lobbyScript.OpenForDungeon(dungeonConfigId, "HollowCryptDungeon", energyCost, dungeonName);
        }
        else
        {
            Debug.LogError("[DungeonEntrance] TeamPanel UI GameObject not found!");
        }
    }
}
