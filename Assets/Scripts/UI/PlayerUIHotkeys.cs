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
        if (ui != null && ui.inventoryPanel != null)
            ui.OpenPanel(ui.inventoryPanel); // OpenPanel toggles if already open
    }

    private void ToggleMap()
    {
        if (WorldState.PlayerLevel < MainFeatureUnlockRuntime.MiniMapButtonLevel) return;

        var ui = UIManager.Instance;
        if (ui != null && ui.mapPanel != null)
            ui.OpenPanel(ui.mapPanel);
    }
}
