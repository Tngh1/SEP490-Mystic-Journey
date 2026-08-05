using MysticJourney.API.Endpoints;
using UnityEngine;

public class DungeonEntrance : MonoBehaviour
{
    /// <summary>
    /// Level tối thiểu của CHÍNH dungeon này, đọc từ DungeonConfig.LevelRequirement trên
    /// server. Nguồn duy nhất — WorldInteractable.GetPromptText() đọc lại property này.
    ///
    /// null = server CHƯA trả lời. Client không có số mặc định nào: trước đây là const 3
    /// dùng chung cho MỌI cửa, nên mỗi cửa có LevelRequirement riêng trong DB mà client
    /// vẫn chặn tất cả ở 3, và sửa DB thì client không theo. Thà hiện "đang kiểm tra" một
    /// nhịp còn hơn hiện một con số client tự bịa ra.
    /// </summary>
    public int? RequiredLevel { get; private set; }

    /// <summary>Tên dungeon từ DungeonConfig.Name trên server, lấy từ CÙNG response với
    /// RequiredLevel nên không tốn thêm request. Không [SerializeField]: giá trị cứng trong
    /// scene đã sai thật (cả ba cửa đều ghi "Abandoned Mines", kể cả cửa configId 2 là
    /// Dragon's Lair) và chỉ vô hình vì panel tự ghi đè khi GetById của nó thành công.</summary>
    private string dungeonName;

    [SerializeField] private int dungeonConfigId = 1;
    [SerializeField] private int energyCost = 20;
    [SerializeField] private float interactionRadius = 2.5f;

    private bool fetchInFlight;

    private void Start()
    {
        // Add and configure WorldInteractable component dynamically to utilize the prompt system
        var interactable = gameObject.GetComponent<WorldInteractable>() ?? gameObject.AddComponent<WorldInteractable>();
        interactable.ConfigureDungeon(dungeonConfigId, interactionRadius);
        Debug.Log($"[DungeonEntrance] Configured {gameObject.name} as Dungeon Entrance Interactable.");

        // Prompt hiện MỖI FRAME khi đứng trong bán kính nên phải fetch sẵn ở Start,
        // không đợi tới Interact.
        FetchConfig();
    }

    /// <summary>Idempotent: bỏ qua nếu đã có số thật hoặc đang có request bay. Gọi lại được
    /// từ Interact() để thử lại sau khi mạng lỗi — nếu không, một lần GetById fail là cửa
    /// khoá vĩnh viễn tới lúc load lại scene. Guard theo RequiredLevel là đủ cho cả tên:
    /// hai giá trị đến từ cùng một response.</summary>
    private void FetchConfig()
    {
        if (fetchInFlight || RequiredLevel.HasValue) return;
        fetchInFlight = true;

        DungeonApi.Instance.GetById(dungeonConfigId,
            config =>
            {
                fetchInFlight = false;
                if (config == null) return;
                RequiredLevel = Mathf.Max(1, config.LevelRequirement);
                dungeonName = config.Name;
            },
            error =>
            {
                fetchInFlight = false;
                Debug.LogWarning($"[DungeonEntrance] GetById({dungeonConfigId}) failed: {error.Message}");
            });
    }

    public void Interact()
    {
        // Chưa biết ngưỡng thì KHÔNG cho qua (fail closed) và thử fetch lại luôn.
        // Mở cửa khi chưa biết sẽ đẩy người chơi vào loading rồi để server chặn ở
        // /dungeon/{id}/enter — tệ hơn nhiều so với đứng chờ một nhịp.
        if (!RequiredLevel.HasValue)
        {
            FetchConfig();
            return;
        }

        // WorldState.PlayerLevel do PlayerHUDController.RefreshHUD() ghi từ GetMyProfile
        // mỗi 15s. HUD sống ở scene Main và world scene load ADDITIVE lên nó
        // (GameBootstrap.EnsureSceneLoaded), nên vòng lặp đó vẫn chạy ở đây — đây là
        // giá trị server, không phải cache offline.
        if (WorldState.PlayerLevel < RequiredLevel.Value)
        {
            // Không cần báo gì thêm ở đây: PlayerWorldInteractor.Update() gọi
            // WorldInteractionPromptRuntime.Show(GetPromptText()) MỖI FRAME khi người chơi
            // đứng trong bán kính, và GetPromptText() đã hiện "Requires Level ..." rồi
            // — cùng ngưỡng, cùng nguồn.
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
