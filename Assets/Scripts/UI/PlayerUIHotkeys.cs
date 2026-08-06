using UnityEngine;

/// <summary>
/// Routes the client-local UI hotkeys — Inventory and Map — from the single
/// input source (<see cref="GameplayInputProvider"/>) to the panels they open.
///
/// These actions are intentionally NOT part of the Fusion network input: they
/// only open local UI panels, so they are polled directly on the local player
/// (via <see cref="GameplayInputProvider.Local"/>) and work identically offline
/// and after connecting. This is the sole place the Inventory / Map keys are
/// read, so they always honour the player's rebindings.
///
/// Added automatically by <see cref="UIManager"/> (EnsureRuntimeComponents), so
/// it needs no scene wiring.
/// </summary>
public class PlayerUIHotkeys : MonoBehaviour
{
    private void Update()
    {
        var input = GameplayInputProvider.Local;
        if (input == null) return;

        if (input.InventoryPressed)
            ToggleInventory();

        if (input.MapPressed)
            ToggleMap();

        // Skill 1/2/3 CHỈ đọc ở đây khi OFFLINE. Online, phím skill đi qua Fusion
        // (LocalInputCollector -> NetworkPlayer.RequestSkill) nên đọc thêm ở đây sẽ
        // gây double-fire (cast 2 lần). RequestCastSkillBySlot là đúng đường HUD click.
        if (!IsNetworked)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                if (input.SkillPressed(slot))
                    CastSkill(slot);
            }
        }
    }

    /// <summary>
    /// A toggle key may act when nothing is open, or when the panel it owns is the
    /// one that's open (so the same key closes it). If a DIFFERENT panel is open the
    /// key is ignored — otherwise pressing M over the party roster would route
    /// through UIManager.ShowPanel → CloseAll and silently drop the player out of
    /// the party panel.
    /// </summary>
    private static bool CanToggle(UIManager ui, GameObject owned)
    {
        if (ui == null) return false;
        return !ui.IsAnyPanelOpen || ui.IsPanelOpen(owned);
    }

    private static bool IsNetworked
    {
        get
        {
            var runner = Fusion.NetworkRunner.Instances != null && Fusion.NetworkRunner.Instances.Count > 0
                ? Fusion.NetworkRunner.Instances[0] : null;
            return runner != null && runner.IsRunning;
        }
    }

    private void CastSkill(int slotIndex)
    {
        var combat = PlayerEntity.Instance != null ? PlayerEntity.Instance.GetComponent<PlayerCombat>() : null;
        if (combat != null)
            combat.RequestCastSkillBySlot(slotIndex);
    }

    private void ToggleInventory()
    {
        // Match the InventoryButton unlock gate so the key can't open a panel the
        // player hasn't unlocked yet.
        if (WorldState.PlayerLevel < MainFeatureUnlockRuntime.InventoryButtonLevel) return;

        var ui = UIManager.Instance;
        if (ui != null && ui.inventoryPanel != null && CanToggle(ui, ui.inventoryPanel))
            ui.OpenPanel(ui.inventoryPanel); // OpenPanel toggles if already open
    }

    /// <summary>
    /// False khi người chơi tự ẩn minimap bằng phím Map (bước 3 của vòng lặp).
    /// <see cref="MainFeatureUnlockRuntime.Apply"/> đọc cờ này: nó chạy lại mỗi lần
    /// LevelChanged/QuestsChanged và SetActive(true) lại MiniMapButton, nên nếu không
    /// tôn trọng lựa chọn của người chơi thì minimap sẽ tự hiện lại giữa chừng.
    /// </summary>
    public static bool MinimapVisible { get; private set; } = true;

    // Bước hiện tại của vòng lặp phím Map: 0 = chưa mở gì, 1 = Map Panel đang mở,
    // 2 = Map Panel đã đóng, 3 = minimap đang ẩn. Nhấn tiếp từ 3 quay lại 0.
    private int _mapCycleStep;

    /// <summary>
    /// Phím Map chạy vòng 4 bước: mở Map Panel → đóng Map Panel → ẩn minimap trên HUD →
    /// hiện lại minimap → lặp lại. Phím nào chạy vòng này là do binding "Map" quyết định,
    /// nên người chơi đổi phím trong GameSettingPanel (tab Controller) là ăn ngay.
    /// </summary>
    private void ToggleMap()
    {
        if (WorldState.PlayerLevel < MainFeatureUnlockRuntime.MiniMapButtonLevel) return;

        var ui = UIManager.Instance;
        if (ui == null || ui.mapPanel == null || !CanToggle(ui, ui.mapPanel)) return;

        // Đồng bộ vòng lặp với thực tế trước khi bước tiếp: panel còn được mở bằng nút MiniMap
        // và đóng bằng nút Continue / khi mở panel khác / khi vào dungeon. Không sync thì lần
        // nhấn kế tiếp nhảy sai bước — ví dụ ẩn minimap trong khi Map Panel vẫn đang mở.
        if (ui.IsPanelOpen(ui.mapPanel)) _mapCycleStep = 1;
        else if (_mapCycleStep == 1) _mapCycleStep = 0;

        _mapCycleStep = _mapCycleStep >= 3 ? 0 : _mapCycleStep + 1;

        switch (_mapCycleStep)
        {
            case 1:
                // Trong dungeon không cho mở: panel chỉ dùng để dịch chuyển map, mà dịch chuyển
                // đang bị chặn. Đứng yên ở bước 0 để lần nhấn sau thử mở lại từ đầu.
                if (!MainMapPanelRuntime.CanOpen) { _mapCycleStep = 0; return; }
                ui.ShowPanel(ui.mapPanel);
                break;

            case 2:
                ui.ClosePanel(ui.mapPanel);
                break;

            case 3:
                SetMinimapVisible(false);
                break;

            default: // 0 — hết vòng, trả HUD về trạng thái ban đầu
                SetMinimapVisible(true);
                break;
        }
    }

    private static GameObject _miniMapButton;

    private static void SetMinimapVisible(bool visible)
    {
        MinimapVisible = visible;

        if (_miniMapButton == null)
            _miniMapButton = FindSceneObject("MiniMapButton");

        if (_miniMapButton != null)
            _miniMapButton.SetActive(visible);
    }

    private static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in objects)
        {
            if (obj != null && obj.name == objectName && obj.scene.IsValid() && !string.IsNullOrEmpty(obj.scene.name))
                return obj;
        }

        return null;
    }
}
