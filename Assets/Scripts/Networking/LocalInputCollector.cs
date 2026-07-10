using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalInputCollector : MonoBehaviour
{
    [Header("Input Actions")]
    [Tooltip("Optional. If null, Mouse.current / Keyboard.current will be queried directly.")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("Aim Settings")]
    [Tooltip("Z depth used when projecting the pointer into world space.")]
    [SerializeField] private float aimWorldDepth = 0f;

    private InputAction _moveAction;
    private InputAction _attackAction;
    private InputAction _skill1Action;
    private InputAction _skill2Action;
    private InputAction _skill3Action;
    private InputAction _interactAction;

    private void Awake()
    {
        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Player", throwIfNotFound: false);
            if (map != null)
            {
                _moveAction     = map.FindAction("Move");
                _attackAction   = map.FindAction("Attack");
                _skill1Action   = map.FindAction("Skill1");
                _skill2Action   = map.FindAction("Skill2");
                _skill3Action   = map.FindAction("Skill3");
                _interactAction = map.FindAction("Interact");
            }
        }
    }

    private void OnEnable()
    {
        _moveAction?.Enable();
        _attackAction?.Enable();
        _skill1Action?.Enable();
        _skill2Action?.Enable();
        _skill3Action?.Enable();
        _interactAction?.Enable();
    }

    private void OnDisable()
    {
        _moveAction?.Disable();
        _attackAction?.Disable();
        _skill1Action?.Disable();
        _skill2Action?.Disable();
        _skill3Action?.Disable();
        _interactAction?.Disable();
    }

    /// <summary>
    /// Build a fresh <see cref="NetworkInputData"/> snapshot from current input state.
    /// Called once per Fusion tick from PhotonManager.OnInput.
    /// </summary>
    public NetworkInputData Collect()
    {
        var data = new NetworkInputData
        {
            Move = ReadMove(),
            AimWorldPosition = ReadAimWorld(),
            Buttons = BuildButtons(),
        };

        Debug.Log($"[InputCollector] Move = {data.Move}");
        return data;
    }

    private Vector2 ReadMove()
    {
        // Prefer Keyboard.current directly — InputActionAsset wiring is fragile during
        // Phase 1 and falls back here keeps movement working regardless of map state.
        Vector2 raw = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) raw.y += 1f;
            if (Keyboard.current.sKey.isPressed) raw.y -= 1f;
            if (Keyboard.current.dKey.isPressed) raw.x += 1f;
            if (Keyboard.current.aKey.isPressed) raw.x -= 1f;
        }

        // If keyboard gave nothing, try the InputAction asset as a secondary source.
        if (raw == Vector2.zero && _moveAction != null)
        {
            raw = _moveAction.ReadValue<Vector2>();
        }

        return raw.sqrMagnitude > 1f ? raw.normalized : raw;
    }

    private Vector2 ReadAimWorld()
    {
        if (Mouse.current == null)
            return Vector2.zero;

        var cam = Camera.main;
        if (cam == null)
            return Vector2.zero;

        var screen = Mouse.current.position.ReadValue();
        var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, aimWorldDepth));
        return new Vector2(world.x, world.y);
    }

    private NetworkButtons BuildButtons()
    {
        var buttons = default(NetworkButtons);

        bool attack    = IsPressed(_attackAction,   () => Mouse.current != null && Mouse.current.leftButton.isPressed);
        bool skill1    = IsPressed(_skill1Action,   () => Keyboard.current != null && Keyboard.current.digit1Key.isPressed);
        bool skill2    = IsPressed(_skill2Action,   () => Keyboard.current != null && Keyboard.current.digit2Key.isPressed);
        bool skill3    = IsPressed(_skill3Action,   () => Keyboard.current != null && Keyboard.current.digit3Key.isPressed);
        bool interact  = IsPressed(_interactAction, () => Keyboard.current != null && Keyboard.current.eKey.isPressed);
        bool aimConfirm = attack;

        buttons.Set(InputButtons.Attack,      attack);
        buttons.Set(InputButtons.Skill1,      skill1);
        buttons.Set(InputButtons.Skill2,      skill2);
        buttons.Set(InputButtons.Skill3,      skill3);
        buttons.Set(InputButtons.Interact,    interact);
        buttons.Set(InputButtons.AimConfirm,  aimConfirm);

        return buttons;
    }

    private static bool IsPressed(InputAction action, Func<bool> fallback)
    {
        if (action != null)
            return action.IsPressed();
        return fallback();
    }
}