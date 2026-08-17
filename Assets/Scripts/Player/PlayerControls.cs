
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

// Executes i disposable operation.
public partial class @PlayerControls: IInputActionCollection2, IDisposable
{
    // Executes asset operation.
    public InputActionAsset asset { get; }

    // Executes player controls operation.
    public @PlayerControls()
    {
        asset = InputActionAsset.FromJson(@"{
    ""version"": 1,
    ""name"": ""PlayerControls"",
    ""maps"": [
        {
            ""name"": ""Player"",
            ""id"": ""b48670cb-6d72-4164-9712-991d48c5ca1b"",
            ""actions"": [
                {
                    ""name"": ""Move"",
                    ""type"": ""Value"",
                    ""id"": ""11169696-b376-464b-9ca1-367b0731ba57"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": true
                },
                {
                    ""name"": ""Attack"",
                    ""type"": ""Button"",
                    ""id"": ""f11c81b3-b4de-4257-8acb-d72d7678bf55"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Interact"",
                    ""type"": ""Button"",
                    ""id"": ""dbe0ac9e-bb49-4b78-bf4d-4c6d99627804"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Inventory"",
                    ""type"": ""Button"",
                    ""id"": ""02ed073d-7537-4f68-8f10-5891b9b10b6d"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Map"",
                    ""type"": ""Button"",
                    ""id"": ""6865aa57-4d98-4c8d-b60b-167620ee9c0f"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Skill1"",
                    ""type"": ""Button"",
                    ""id"": ""3e8228c7-93b3-49dc-8443-75a1fb2cdd65"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Skill2"",
                    ""type"": ""Button"",
                    ""id"": ""46170434-8912-4248-8ad2-7bd6b074e6a3"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                },
                {
                    ""name"": ""Skill3"",
                    ""type"": ""Button"",
                    ""id"": ""4322e8e7-2766-42bc-aad2-6f1e478c5f62"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """",
                    ""initialStateCheck"": false
                }
            ],
            ""bindings"": [
                {
                    ""name"": """",
                    ""id"": ""f40417a3-c1ff-4bed-ad86-82bcf195103e"",
                    ""path"": ""<Mouse>/leftButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Attack"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""58b138a1-c2c3-46f2-be91-91ea4b052dc9"",
                    ""path"": ""<Keyboard>/#(E)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Interact"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""fdb06fc6-c337-4293-88a0-fcb265fe4950"",
                    ""path"": ""<Keyboard>/#(B)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Inventory"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""f43cb14b-4b90-498a-9c65-fb54b63168d2"",
                    ""path"": ""<Keyboard>/#(M)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Map"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""0e47caf4-c408-4d76-937f-36efbf2bb3ab"",
                    ""path"": ""<Keyboard>/#(1)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Skill1"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""e6eca544-9925-4d7f-98ac-f3b0913f5a65"",
                    ""path"": ""<Keyboard>/#(2)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Skill2"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""420c0421-4661-47fa-87ba-17998b5636e7"",
                    ""path"": ""<Keyboard>/#(3)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Skill3"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""2D Vector"",
                    ""id"": ""dc425d2c-d400-42b0-b2d3-580640080bc1"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""5f9ed241-8ad5-40d2-b8fe-5bc8987d55fe"",
                    ""path"": ""<Keyboard>/#(W)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""7ac1c76b-84cb-4f7e-9381-22dd5a8e85dc"",
                    ""path"": ""<Keyboard>/#(S)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""a30582a9-6e6a-4e40-8ef7-4bf53217f2d4"",
                    ""path"": ""<Keyboard>/#(A)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""381fc5b4-0c24-4d4d-8a50-fe4fc9769d8b"",
                    ""path"": ""<Keyboard>/#(D)"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
        m_Player = asset.FindActionMap("Player", throwIfNotFound: true);
        m_Player_Move = m_Player.FindAction("Move", throwIfNotFound: true);
        m_Player_Attack = m_Player.FindAction("Attack", throwIfNotFound: true);
        m_Player_Interact = m_Player.FindAction("Interact", throwIfNotFound: true);
        m_Player_Inventory = m_Player.FindAction("Inventory", throwIfNotFound: true);
        m_Player_Map = m_Player.FindAction("Map", throwIfNotFound: true);
        m_Player_Skill1 = m_Player.FindAction("Skill1", throwIfNotFound: true);
        m_Player_Skill2 = m_Player.FindAction("Skill2", throwIfNotFound: true);
        m_Player_Skill3 = m_Player.FindAction("Skill3", throwIfNotFound: true);
    }

    ~@PlayerControls()
    {
        UnityEngine.Debug.Assert(!m_Player.enabled, "This will cause a leak and performance issues, PlayerControls.Player.Disable() has not been called.");
    }

    // Executes dispose operation.
    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    // Executes binding mask operation.
    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    // Executes devices operation.
    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    // Executes control schemes operation.
    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    // Executes contains operation.
    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    // Executes get enumerator operation.
    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // Executes enable operation.
    public void Enable()
    {
        asset.Enable();
    }

    // Executes disable operation.
    public void Disable()
    {
        asset.Disable();
    }

    // Executes bindings operation.
    public IEnumerable<InputBinding> bindings => asset.bindings;

    // Executes find action operation.
    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
    {
        return asset.FindAction(actionNameOrId, throwIfNotFound);
    }

    // Executes find binding operation.
    public int FindBinding(InputBinding bindingMask, out InputAction action)
    {
        return asset.FindBinding(bindingMask, out action);
    }

    private readonly InputActionMap m_Player;
    private List<IPlayerActions> m_PlayerActionsCallbackInterfaces = new List<IPlayerActions>();
    private readonly InputAction m_Player_Move;
    private readonly InputAction m_Player_Attack;
    private readonly InputAction m_Player_Interact;
    private readonly InputAction m_Player_Inventory;
    private readonly InputAction m_Player_Map;
    private readonly InputAction m_Player_Skill1;
    private readonly InputAction m_Player_Skill2;
    private readonly InputAction m_Player_Skill3;
    // Initializes a new default instance of the PlayerActions class.
    public struct PlayerActions
    {
        private @PlayerControls m_Wrapper;

        // Initializes a new instance of PlayerActions with dependencies: wrapper.
        // Assigns injected service and configuration instances to readonly fields for runtime operations.
        public PlayerActions(@PlayerControls wrapper) { m_Wrapper = wrapper; }
        // Executes move operation.
        public InputAction @Move => m_Wrapper.m_Player_Move;
        // Executes attack operation.
        public InputAction @Attack => m_Wrapper.m_Player_Attack;
        // Executes interact operation.
        public InputAction @Interact => m_Wrapper.m_Player_Interact;
        // Executes inventory operation.
        public InputAction @Inventory => m_Wrapper.m_Player_Inventory;
        // Executes map operation.
        public InputAction @Map => m_Wrapper.m_Player_Map;
        // Executes skill1 operation.
        public InputAction @Skill1 => m_Wrapper.m_Player_Skill1;
        // Executes skill2 operation.
        public InputAction @Skill2 => m_Wrapper.m_Player_Skill2;
        // Executes skill3 operation.
        public InputAction @Skill3 => m_Wrapper.m_Player_Skill3;
        // Send a GET request with optional query parameters, unwrap the API envelope, and return the typed response payload.
        public InputActionMap Get() { return m_Wrapper.m_Player; }
        // Fetch the endpoint and return the typed API result.
        public void Enable() { Get().Enable(); }
        // Fetch the endpoint and return the typed API result.
        public void Disable() { Get().Disable(); }
        // Executes enabled operation.
        public bool enabled => Get().enabled;
        // Executes input action map operation.
        public static implicit operator InputActionMap(PlayerActions set) { return set.Get(); }
        // Executes add callbacks operation.
        public void AddCallbacks(IPlayerActions instance)
        {
            if (instance == null || m_Wrapper.m_PlayerActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_PlayerActionsCallbackInterfaces.Add(instance);
            @Move.started += instance.OnMove;
            @Move.performed += instance.OnMove;
            @Move.canceled += instance.OnMove;
            @Attack.started += instance.OnAttack;
            @Attack.performed += instance.OnAttack;
            @Attack.canceled += instance.OnAttack;
            @Interact.started += instance.OnInteract;
            @Interact.performed += instance.OnInteract;
            @Interact.canceled += instance.OnInteract;
            @Inventory.started += instance.OnInventory;
            @Inventory.performed += instance.OnInventory;
            @Inventory.canceled += instance.OnInventory;
            @Map.started += instance.OnMap;
            @Map.performed += instance.OnMap;
            @Map.canceled += instance.OnMap;
            @Skill1.started += instance.OnSkill1;
            @Skill1.performed += instance.OnSkill1;
            @Skill1.canceled += instance.OnSkill1;
            @Skill2.started += instance.OnSkill2;
            @Skill2.performed += instance.OnSkill2;
            @Skill2.canceled += instance.OnSkill2;
            @Skill3.started += instance.OnSkill3;
            @Skill3.performed += instance.OnSkill3;
            @Skill3.canceled += instance.OnSkill3;
        }

        // Executes unregister callbacks operation.
        private void UnregisterCallbacks(IPlayerActions instance)
        {
            @Move.started -= instance.OnMove;
            @Move.performed -= instance.OnMove;
            @Move.canceled -= instance.OnMove;
            @Attack.started -= instance.OnAttack;
            @Attack.performed -= instance.OnAttack;
            @Attack.canceled -= instance.OnAttack;
            @Interact.started -= instance.OnInteract;
            @Interact.performed -= instance.OnInteract;
            @Interact.canceled -= instance.OnInteract;
            @Inventory.started -= instance.OnInventory;
            @Inventory.performed -= instance.OnInventory;
            @Inventory.canceled -= instance.OnInventory;
            @Map.started -= instance.OnMap;
            @Map.performed -= instance.OnMap;
            @Map.canceled -= instance.OnMap;
            @Skill1.started -= instance.OnSkill1;
            @Skill1.performed -= instance.OnSkill1;
            @Skill1.canceled -= instance.OnSkill1;
            @Skill2.started -= instance.OnSkill2;
            @Skill2.performed -= instance.OnSkill2;
            @Skill2.canceled -= instance.OnSkill2;
            @Skill3.started -= instance.OnSkill3;
            @Skill3.performed -= instance.OnSkill3;
            @Skill3.canceled -= instance.OnSkill3;
        }

        // Executes remove callbacks operation.
        public void RemoveCallbacks(IPlayerActions instance)
        {
            if (m_Wrapper.m_PlayerActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        // Executes set callbacks operation.
        public void SetCallbacks(IPlayerActions instance)
        {
            foreach (var item in m_Wrapper.m_PlayerActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_PlayerActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }
    // Executes player operation.
    public PlayerActions @Player => new PlayerActions(this);
    // Executes i player actions operation.
    public interface IPlayerActions
    {
        void OnMove(InputAction.CallbackContext context);
        void OnAttack(InputAction.CallbackContext context);
        void OnInteract(InputAction.CallbackContext context);
        void OnInventory(InputAction.CallbackContext context);
        void OnMap(InputAction.CallbackContext context);
        void OnSkill1(InputAction.CallbackContext context);
        void OnSkill2(InputAction.CallbackContext context);
        void OnSkill3(InputAction.CallbackContext context);
    }
}
