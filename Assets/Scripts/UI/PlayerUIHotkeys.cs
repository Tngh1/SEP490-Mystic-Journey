using UnityEngine;

// Executes mono behaviour operation.
public class PlayerUIHotkeys : MonoBehaviour
{
    // Per-frame update loop for PlayerUIHotkeys.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        var input = GameplayInputProvider.Local;
        if (input == null) return;

        if (input.InventoryPressed)
            ToggleInventory();

        if (input.MapPressed)
            ToggleMap();

        if (!IsNetworked)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                if (input.SkillPressed(slot))
                    CastSkill(slot);
            }
        }
    }

    // Executes can toggle operation.
    private static bool CanToggle(UIManager ui, GameObject owned)
    {
        if (ui == null) return false;
        return !ui.IsAnyPanelOpen || ui.IsPanelOpen(owned);
    }

    // Executes is networked operation.
    private static bool IsNetworked
    {
        get
        {
            var runner = Fusion.NetworkRunner.Instances != null && Fusion.NetworkRunner.Instances.Count > 0
                ? Fusion.NetworkRunner.Instances[0] : null;
            return runner != null && runner.IsRunning;
        }
    }

    // Executes cast skill operation.
    private void CastSkill(int slotIndex)
    {
        var combat = PlayerEntity.Instance != null ? PlayerEntity.Instance.GetComponent<PlayerCombat>() : null;
        if (combat != null)
            combat.RequestCastSkillBySlot(slotIndex);
    }

    // Executes toggle inventory operation.
    private void ToggleInventory()
    {
        if (WorldState.PlayerLevel < MainFeatureUnlockRuntime.InventoryButtonLevel) return;

        var ui = UIManager.Instance;
        if (ui != null && ui.inventoryPanel != null && CanToggle(ui, ui.inventoryPanel))
            ui.OpenPanel(ui.inventoryPanel);
    }

    // Executes minimap visible operation.
    public static bool MinimapVisible { get; private set; } = true;

    private int _mapCycleStep;

    // Executes toggle map operation.
    private void ToggleMap()
    {
        if (WorldState.PlayerLevel < MainFeatureUnlockRuntime.MiniMapButtonLevel) return;

        var ui = UIManager.Instance;
        if (ui == null || ui.mapPanel == null || !CanToggle(ui, ui.mapPanel)) return;

        if (ui.IsPanelOpen(ui.mapPanel)) _mapCycleStep = 1;
        else if (_mapCycleStep == 1) _mapCycleStep = 0;

        _mapCycleStep = _mapCycleStep >= 3 ? 0 : _mapCycleStep + 1;

        switch (_mapCycleStep)
        {
            case 1:
                if (!MapUIManager.CanOpen) { _mapCycleStep = 0; return; }
                ui.ShowPanel(ui.mapPanel);
                break;

            case 2:
                ui.ClosePanel(ui.mapPanel);
                break;

            case 3:
                SetMinimapVisible(false);
                break;

            default:
                SetMinimapVisible(true);
                break;
        }
    }

    private static GameObject _miniMapButton;

    // Executes set minimap visible operation.
    // Validates input parameters against null or empty values.
    private static void SetMinimapVisible(bool visible)
    {
        MinimapVisible = visible;

        if (_miniMapButton == null)
            _miniMapButton = FindSceneObject("MiniMapButton");

        if (_miniMapButton != null)
            _miniMapButton.SetActive(visible);
    }

    // Executes find scene object operation.
    // Validates input parameters against null or empty values.
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
