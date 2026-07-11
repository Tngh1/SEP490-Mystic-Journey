using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single source of truth for reading gameplay input.
///
/// Every gameplay system (offline movement, Fusion input collection, combat,
/// world interaction, …) reads from THIS component instead of touching the
/// Input System, <see cref="Keyboard"/>, <see cref="Mouse"/>, or the legacy
/// <see cref="UnityEngine.Input"/> Manager directly. That keeps all keybinding
/// knowledge in exactly one place and guarantees every action honours the
/// player's rebindings (saved via <c>ControlRebindManager</c> /
/// <c>SaveBindingOverridesAsJson</c>).
///
/// Design decisions (see the refactor spec):
///   • Reads the SAME <see cref="InputActionAsset"/> that PlayerInput uses and
///     that ControlRebindManager writes overrides onto. It resolves that asset
///     WITHOUT going through <see cref="PlayerInput.actions"/> when possible,
///     because PlayerInput clones its asset the moment two PlayerInput
///     components share one asset (Instantiate on conflict). Reading the clone
///     would miss binding overrides applied after the clone was taken. We prefer
///     the literal project asset so rebinds always apply.
///   • It keeps the "Player" action map ENABLED. Multiple PlayerInput components
///     briefly coexisting (offline player being destroyed as the network avatar
///     spawns) can leave the shared map DISABLED via PlayerInput.OnDisable →
///     DeactivateInput(). A disabled map returns Vector2.zero for Move, which is
///     exactly the "can't move after connecting" bug. This provider re-enables
///     the map every frame it is used, so it is self-healing.
///   • Adding a new gameplay action (Sprint / Dodge / Dash / …) means adding one
///     accessor here — no gameplay script changes.
///
/// Placement: sits on the same GameObject as <see cref="PlayerInput"/> (the
/// player prefab root). It has no ordering requirement — it lazily resolves
/// actions on first use.
/// </summary>
[DisallowMultipleComponent]
public class GameplayInputProvider : MonoBehaviour
{
    /// <summary>
    /// The local player's provider, if one exists. Convenience accessor for
    /// scene-level scripts (chests, minimap toggle) that need to read the local
    /// player's input without holding a direct reference. Tracks the same local
    /// input-authority player as <see cref="PlayerMovement.Instance"/>.
    /// </summary>
    public static GameplayInputProvider Local
    {
        get
        {
            var localPlayer = PlayerMovement.Instance;
            return localPlayer != null ? localPlayer.GetComponent<GameplayInputProvider>() : null;
        }
    }

    // Action-map / action names. Kept in one place so the string literals used
    // to look up InputActions live here and nowhere else.
    private const string PlayerMap   = "Player";
    private const string MoveName    = "Move";
    private const string AttackName  = "Attack";
    private const string Skill1Name  = "Skill1";
    private const string Skill2Name  = "Skill2";
    private const string Skill3Name  = "Skill3";
    private const string InteractName = "Interact";
    private const string InventoryName = "Inventory";
    private const string MapName     = "Map";

    private PlayerInput _playerInput;
    private InputActionAsset _asset;
    private InputActionMap _playerMap;

    // The asset instance we last applied saved binding overrides to. Tracked so
    // we re-apply if PlayerInput hands us a DIFFERENT instance (e.g. it cloned
    // its asset on a multi-PlayerInput conflict when Fusion connects). Without
    // this, the clone carries default bindings and the saved settings are lost.
    private InputActionAsset _overridesAppliedTo;

    // MUST match ControlRebindManager.BindingKey — the PlayerPrefs key the
    // settings panel saves rebindings under.
    private const string BindingKey = "MJ_KEY_BINDINGS";

    private InputAction _move;
    private InputAction _attack;
    private InputAction _skill1;
    private InputAction _skill2;
    private InputAction _skill3;
    private InputAction _interact;
    private InputAction _inventory;
    private InputAction _map;

    private bool _resolved;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Move
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Normalized 2D movement vector from the Move action (clamped to magnitude 1).</summary>
    public Vector2 Move
    {
        get
        {
            EnsureResolved();
            if (_move != null)
            {
                Vector2 raw = _move.ReadValue<Vector2>();
                return raw.sqrMagnitude > 1f ? raw.normalized : raw;
            }
            return ReadMoveEmergencyFallback();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — buttons (held / pressed-this-frame)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True while the Attack control is held down.</summary>
    public bool AttackHeld => IsHeld(_attack, ref _attack, AttackName);
    /// <summary>True on the frame the Attack control was pressed.</summary>
    public bool AttackPressed => WasPressed(_attack, ref _attack, AttackName);

    public bool Skill1Held => IsHeld(_skill1, ref _skill1, Skill1Name);
    public bool Skill1Pressed => WasPressed(_skill1, ref _skill1, Skill1Name);

    public bool Skill2Held => IsHeld(_skill2, ref _skill2, Skill2Name);
    public bool Skill2Pressed => WasPressed(_skill2, ref _skill2, Skill2Name);

    public bool Skill3Held => IsHeld(_skill3, ref _skill3, Skill3Name);
    public bool Skill3Pressed => WasPressed(_skill3, ref _skill3, Skill3Name);

    /// <summary>True while the Interact control is held down.</summary>
    public bool InteractHeld => IsHeld(_interact, ref _interact, InteractName);
    /// <summary>True on the frame the Interact control was pressed. Respects rebinding.</summary>
    public bool InteractPressed => WasPressed(_interact, ref _interact, InteractName);

    public bool InventoryPressed => WasPressed(_inventory, ref _inventory, InventoryName);
    public bool MapPressed => WasPressed(_map, ref _map, MapName);

    /// <summary>
    /// Skill slot helper (0-based) so callers can index without knowing the
    /// action names. Slot 0/1/2 → Skill1/2/3.
    /// </summary>
    public bool SkillPressed(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return Skill1Pressed;
            case 1: return Skill2Pressed;
            case 2: return Skill3Pressed;
            default: return false;
        }
    }

    public bool SkillHeld(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0: return Skill1Held;
            case 1: return Skill2Held;
            case 2: return Skill3Held;
            default: return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — pointer / aim
    //
    // Aim position is intentionally read from Mouse.current here (not a bindable
    // InputAction): a world-space cursor position is not a "key" the player would
    // rebind. Centralising it here still keeps gameplay scripts (PlayerCombat,
    // LocalInputCollector) free of direct Mouse.current reads.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Current pointer position in screen pixels, or null if no pointer device.</summary>
    public Vector2? PointerScreenPosition
    {
        get
        {
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            return null;
        }
    }

    /// <summary>
    /// Pointer position projected into world space using the main camera, or
    /// null if there is no pointer / camera. Z is flattened to 0.
    /// </summary>
    public Vector2? PointerWorldPosition
    {
        get
        {
            var screen = PointerScreenPosition;
            if (screen == null) return null;
            var cam = Camera.main;
            if (cam == null) return null;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.Value.x, screen.Value.y, 0f));
            return new Vector2(world.x, world.y);
        }
    }

    /// <summary>True on the frame the primary pointer button (confirm) was pressed.</summary>
    public bool PointerConfirmPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

    /// <summary>True on the frame the secondary pointer button (cancel) was pressed.</summary>
    public bool PointerCancelPressed => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

    // ─────────────────────────────────────────────────────────────────────────
    // Resolution
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        EnsureResolved();
    }

    private void OnEnable()
    {
        // Re-resolve (in case actions were uninitialised while disabled) and
        // make sure the map is live.
        _resolved = false;
        EnsureResolved();
        _playerMap?.Enable();
    }

    /// <summary>
    /// Resolve the action map + actions from the literal InputActionAsset.
    ///
    /// Priority:
    ///   1. PlayerInput.actions — this is the asset PlayerInput drives and (for
    ///      the local player) the one whose map is enabled for the SendMessage
    ///      callbacks. It already carries binding overrides applied through the
    ///      shared literal asset because ControlRebindManager writes overrides on
    ///      the asset PlayerInput references.
    ///   2. Fallback: nothing — Move() then uses the emergency keyboard path.
    ///
    /// We keep a reference to the map and RE-ENABLE it each resolution so a
    /// stray PlayerInput.OnDisable (from a sibling player being destroyed on
    /// connect) cannot leave Move returning zero.
    /// </summary>
    private void EnsureResolved()
    {
        if (_playerInput == null)
            _playerInput = GetComponent<PlayerInput>();

        InputActionAsset current = _playerInput != null ? _playerInput.actions : null;

        // Fast path: already resolved AND PlayerInput still points at the same
        // asset instance. If PlayerInput swapped to a clone (multi-PlayerInput
        // conflict on Fusion connect), current != _asset and we fall through to
        // re-resolve + re-apply saved overrides onto the new instance.
        if (_resolved && _playerMap != null && ReferenceEquals(current, _asset))
        {
            _playerMap.Enable();
            return;
        }

        _asset = current;

        if (_asset != null)
        {
            // Apply the player's saved rebindings onto THIS asset instance. Needed
            // because the settings panel (ControlRebindManager) may never have run
            // this session, and because PlayerInput can hand us a fresh CLONE of
            // the asset on a multi-PlayerInput conflict (Fusion connect) — the
            // clone starts with default bindings until we re-apply the overrides.
            ApplySavedOverrides(_asset);

            _playerMap = _asset.FindActionMap(PlayerMap, throwIfNotFound: false);
            if (_playerMap != null)
            {
                _move     = _playerMap.FindAction(MoveName);
                _attack   = _playerMap.FindAction(AttackName);
                _skill1   = _playerMap.FindAction(Skill1Name);
                _skill2   = _playerMap.FindAction(Skill2Name);
                _skill3   = _playerMap.FindAction(Skill3Name);
                _interact = _playerMap.FindAction(InteractName);
                _inventory = _playerMap.FindAction(InventoryName);
                _map      = _playerMap.FindAction(MapName);

                // Self-heal: guarantee the map is enabled so reads never return
                // zero merely because a sibling PlayerInput disabled it.
                _playerMap.Enable();
                _resolved = true;
                return;
            }
        }

        _resolved = false; // couldn't resolve — will retry and use fallback meanwhile
    }

    /// <summary>
    /// Load the player's saved binding overrides (from the settings panel) onto
    /// <paramref name="asset"/>. Idempotent per asset instance: only re-applies
    /// when handed a different instance than last time, so it is cheap to call
    /// every resolve. This is what makes rebindings survive a Fusion connect even
    /// if PlayerInput cloned its asset and the settings panel never opened.
    /// </summary>
    private void ApplySavedOverrides(InputActionAsset asset)
    {
        if (asset == null) return;
        if (ReferenceEquals(asset, _overridesAppliedTo)) return;

        _overridesAppliedTo = asset;

        if (!PlayerPrefs.HasKey(BindingKey)) return;

        string json = PlayerPrefs.GetString(BindingKey);
        if (string.IsNullOrEmpty(json)) return;

        asset.LoadBindingOverridesFromJson(json);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsHeld(InputAction cached, ref InputAction field, string name)
    {
        EnsureResolved();
        InputAction a = field ?? cached;
        return a != null && a.IsPressed();
    }

    private bool WasPressed(InputAction cached, ref InputAction field, string name)
    {
        EnsureResolved();
        InputAction a = field ?? cached;
        return a != null && a.WasPressedThisFrame();
    }

    /// <summary>
    /// Absolute last-resort movement read used ONLY when no InputActionAsset is
    /// available at all (e.g. a PlayerInput-less test rig). Never runs when the
    /// action resolved, so a rebind is never overridden by hardcoded WASD.
    /// </summary>
    private static Vector2 ReadMoveEmergencyFallback()
    {
        if (Keyboard.current == null) return Vector2.zero;
        Vector2 v = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) v.y += 1f;
        if (Keyboard.current.sKey.isPressed) v.y -= 1f;
        if (Keyboard.current.dKey.isPressed) v.x += 1f;
        if (Keyboard.current.aKey.isPressed) v.x -= 1f;
        return v.sqrMagnitude > 1f ? v.normalized : v;
    }
}
