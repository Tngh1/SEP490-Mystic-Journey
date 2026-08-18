using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

// Executes mono behaviour operation.
[DisallowMultipleComponent]
public class GameplayInputProvider : MonoBehaviour
{
    // Executes local operation.
    public static GameplayInputProvider Local
    {
        get
        {
            var mv = PlayerMovement.Instance;
            if (mv != null)
            {
                var p = mv.GetComponent<GameplayInputProvider>();
                if (p != null) return p;
            }

            var pe = PlayerEntity.Instance;
            return pe != null ? pe.GetComponent<GameplayInputProvider>() : null;
        }
    }

    // Executes ui is capturing input operation.
    public static bool UiIsCapturingInput
    {
        get
        {
            var ui = UIManager.Instance;
            if (ui != null && ui.IsAnyPanelOpen) return true;
            return IsTextFieldFocused;
        }
    }

    // Executes is text field focused operation.
    // Evaluates conditions and returns a boolean result.
    private static bool IsTextFieldFocused
    {
        get
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return false;

            var tmp = sel.GetComponentInParent<TMPro.TMP_InputField>();
            if (tmp != null && tmp.isFocused) return true;

            var legacy = sel.GetComponentInParent<UnityEngine.UI.InputField>();
            return legacy != null && legacy.isFocused;
        }
    }

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

    private InputActionAsset _overridesAppliedTo;

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



    // Executes move operation.
    public Vector2 Move
    {
        get
        {
            if (UiIsCapturingInput) return Vector2.zero;

            EnsureResolved();
            if (_move != null)
            {
                Vector2 raw = _move.ReadValue<Vector2>();
                return raw.sqrMagnitude > 1f ? raw.normalized : raw;
            }
            return ReadMoveEmergencyFallback();
        }
    }


    // Executes attack held operation.
    public bool AttackHeld => !UiIsCapturingInput && IsHeldRaw(_attack, ref _attack, AttackName);
    // Executes attack pressed operation.
    public bool AttackPressed => !UiIsCapturingInput && WasPressedRaw(_attack, ref _attack, AttackName);

    // Executes skill1 held operation.
    public bool Skill1Held => !UiIsCapturingInput && IsHeldRaw(_skill1, ref _skill1, Skill1Name);
    // Executes skill1 pressed operation.
    public bool Skill1Pressed => !UiIsCapturingInput && WasPressedRaw(_skill1, ref _skill1, Skill1Name);

    // Executes skill2 held operation.
    public bool Skill2Held => !UiIsCapturingInput && IsHeldRaw(_skill2, ref _skill2, Skill2Name);
    // Executes skill2 pressed operation.
    public bool Skill2Pressed => !UiIsCapturingInput && WasPressedRaw(_skill2, ref _skill2, Skill2Name);

    // Executes skill3 held operation.
    public bool Skill3Held => !UiIsCapturingInput && IsHeldRaw(_skill3, ref _skill3, Skill3Name);
    // Executes skill3 pressed operation.
    public bool Skill3Pressed => !UiIsCapturingInput && WasPressedRaw(_skill3, ref _skill3, Skill3Name);

    // Executes interact held operation.
    public bool InteractHeld => !IsTextFieldFocused &&
                                (IsHeldRaw(_interact, ref _interact, InteractName) ||
                                 (Keyboard.current != null && Keyboard.current.eKey.isPressed));
    // Executes interact pressed operation.
    public bool InteractPressed => !IsTextFieldFocused &&
                                   (WasPressedRaw(_interact, ref _interact, InteractName) ||
                                    (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame));

    // Executes inventory pressed operation.
    public bool InventoryPressed => !IsTextFieldFocused && WasPressedRaw(_inventory, ref _inventory, InventoryName);
    // Executes map pressed operation.
    public bool MapPressed => !IsTextFieldFocused && WasPressedRaw(_map, ref _map, MapName);

    // Executes skill pressed operation.
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

    // Executes skill held operation.
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


    // Executes pointer screen position operation.
    public Vector2? PointerScreenPosition
    {
        get
        {
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            return Input.mousePosition;
        }
    }

    // Executes pointer world position operation.
    public Vector2? PointerWorldPosition
    {
        get
        {
            var screen = PointerScreenPosition;
            if (screen == null) return null;
            var cam = GameplayCamera;
            if (cam == null) return null;

            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.Value.x, screen.Value.y, depth));
            return new Vector2(world.x, world.y);
        }
    }

    // Executes gameplay camera operation.
    private static Camera GameplayCamera
    {
        get
        {
            var brains = Object.FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var b in brains)
            {
                if (b == null || !b.isActiveAndEnabled) continue;
                var cam = b.GetComponent<Camera>();
                if (cam != null && cam.isActiveAndEnabled) return cam;
            }
            return Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        }
    }

    // Executes pointer confirm pressed operation.
    public bool PointerConfirmPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

    // Executes pointer cancel pressed operation.
    public bool PointerCancelPressed => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;


    // Initializes internal component caches and dependencies for GameplayInputProvider upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        _playerInput = GetComponent<PlayerInput>();
        EnsureResolved();
    }

    // Refresh visible state and subscribe the event handlers required while this component is active.
    private void OnEnable()
    {
        _resolved = false;
        EnsureResolved();
        _playerMap?.Enable();
    }

    // Executes ensure resolved operation.
    private void EnsureResolved()
    {
        if (_playerInput == null)
            _playerInput = GetComponent<PlayerInput>();

        InputActionAsset current = _playerInput != null ? _playerInput.actions : null;

        if (_resolved && _playerMap != null && ReferenceEquals(current, _asset))
        {
            _playerMap.Enable();
            return;
        }

        _asset = current;

        if (_asset != null)
        {
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

                _playerMap.Enable();
                _resolved = true;
                return;
            }
        }

        _resolved = false;
    }

    // Executes apply saved overrides operation.
    // Validates input parameters against null or empty values.
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


    // Executes is held raw operation.
    private bool IsHeldRaw(InputAction cached, ref InputAction field, string name)
    {
        EnsureResolved();
        InputAction a = field ?? cached;
        return a != null && a.IsPressed();
    }

    // Executes was pressed raw operation.
    private bool WasPressedRaw(InputAction cached, ref InputAction field, string name)
    {
        EnsureResolved();
        InputAction a = field ?? cached;
        return a != null && a.WasPressedThisFrame();
    }

    // Executes read move emergency fallback operation.
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
