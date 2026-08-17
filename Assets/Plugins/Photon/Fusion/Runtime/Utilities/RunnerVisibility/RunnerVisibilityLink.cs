
namespace Fusion {

  using System;
  using System.Collections.Generic;
  using Fusion.Analyzer;
  using UnityEngine;

  /// <summary>
  /// Flags a MonoBehaviour class as a RunnerVisibilityControl recognized type. 
  /// Will be included in runner visibility handling, and will be found by <see cref="EnableOnSingleRunner"/> component finds.
  /// </summary>
  public interface IRunnerVisibilityRecognizedType {
    
  }

  /// <summary>
  /// Identifies visible/audible components (such as renderers, canvases, lights) that should be enabled/disabled by runner visibility handling.
  /// Automatically added to scene objects and spawned objects during play if running in <see cref="NetworkProjectConfig.PeerModes.Multiple"/>. 
  /// Additionally this component can be added manually at development time to identify specific Behaviours or Renderers you would like to restrict to one enabled copy at a time.
  /// </summary>
  [AddComponentMenu("")]
  public sealed class RunnerVisibilityLink : MonoBehaviour {

    /// <summary>
    /// The peer runner that will be used if more than one runner is visible, and this node was manually added by developer (indicating only one instance should be visible at a time).
    /// </summary>
    public enum PreferredRunners {
      /// <summary>
      /// The first visible runner will be used.
      /// </summary>
      Auto,
      /// <summary>
      /// The server peer/runner will be used if visible.
      /// </summary>
      Server,
      /// <summary>
      /// The first client peer/runner will be used if visible.
      /// </summary>
      Client,
      /// <summary>
      /// The components will only be enabled on the instance that has input authority over the NetworkObject. Unlike the other options, this expects a NetworkObject to work and it will search its children and parents for it. 
      /// </summary>
      InputAuthority,
    }

    // Executes component type operation.
    private enum ComponentType {
      None,
      Renderer,
      Behaviour
    }

    /// <summary>
    /// If more than one runner instance is visible, this indicates which peer's clone of this entity should be visible.
    /// </summary>
    [SerializeField]
#pragma warning disable IDE0044 // Add readonly modifier
    public PreferredRunners PreferredRunner;
#pragma warning restore IDE0044 // Add readonly modifier

    /// <summary>
    /// The associated component with this node. This Behaviour or Renderer will be enabled/disabled when its NetworkRunner.IsVisible value is changed.
    /// </summary>
    public Component Component;
    
    // Executes is on single runner operation.
    public bool IsOnSingleRunner { get; private set; }

    /// <summary>
    /// Guid is used for common objects (user flagged components that should only run in one instance), to identify matching clones.
    /// </summary>
    [SerializeField]
    [ReadOnly]
    internal string Guid;

    // TODO: This can be removed later. Here for backwards compat for the short term as users may still be using this component.
    // Ultimately this component will always be invisible.
    [SerializeField]
    [HideInInspector]
    internal bool _showAtRuntime;

    // cached runtime
    internal NetworkRunner _runner;
    private ComponentType _componentType;
    private NetworkObject _networkObject;
    private bool _originalState;

    /// <summary>
    /// Set to false to indicate that this object should remain disabled even when <see cref="NetworkRunner.IsVisible"/> is set to true.
    /// </summary>
    public bool DefaultState {
      get {
        return _originalState;
      }
      set {
        _originalState = value;
      }
    }

     // internal LinkedListNode<RunnerVisibilityNode> _node;

    // Executes enabled operation.
    internal bool Enabled {
      get { return _componentType == ComponentType.Renderer ? (Component as Renderer).enabled : (Component as UnityEngine.Behaviour).enabled; }
      set {
        if (Component == null) {  // Entity not found — short-circuit with appropriate error result
          return;
        }
        if (_componentType == ComponentType.Renderer)
          (Component as Renderer).enabled = value;
        else {
          (Component as UnityEngine.Behaviour).enabled = value;
        }
      }
    }


    // TODO: Can be removed most likely now that Node is not user accessible.
    // Reset finds the first viable component and automatically adds it
    private void Reset() {
      _showAtRuntime = true;
      Guid = System.Guid.NewGuid().ToString();
    }

    // Executes associate component operation.
    private bool AssociateComponent(Component component) {
      Component = component;
      var type = component.GetType();
      if (component as Renderer != null) {
        _componentType = ComponentType.Renderer;
        return true;
      } else if (component as UnityEngine.Behaviour != null) {
        _componentType = ComponentType.Behaviour;
        return true;
      }
      return false;
    }

    // Executes on validate operation.
    private void OnValidate() {

      if (Component != null) {  // Entity exists — proceed with conditional branch
        if (Component.transform != transform) {
          Debug.LogWarning($"{nameof(RunnerVisibilityLink)} can only be associated with components on the same GameObject.");
          Component = null;
          return;
        }

        if (AssociateComponent(Component))
          return;

        Debug.LogWarning($"{nameof(RunnerVisibilityLink)} can only be associated with Components that can be enabled/disabled.");
        Component = null;
      }
    }

    // Initializes internal component caches and dependencies for as upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake() {
      // TODO: once deprecated, make this flag always the case and remove the bool check.
      if (!_showAtRuntime)
        this.hideFlags = HideFlags.HideInInspector;
    }

    // Cleanup callback executed when as is destroyed.
    // Unsubscribes from events, cancels active coroutines, and prevents memory leaks.
    private void OnDestroy() {
      this.UnregisterNode();
    }

    // Executes initialize operation.
    internal void Initialize(UnityEngine.Component comp, NetworkRunner runner) {
      _runner = runner;
      
      // First look into children
      _networkObject = GetComponentInChildren<NetworkObject>();
      if (!_networkObject)
        _networkObject = GetComponentInParent<NetworkObject>();
      
      if (!_networkObject && PreferredRunner == PreferredRunners.InputAuthority)
        Log.Warn($"No NetworkObject found for RunnerVisibilityLink on {gameObject.name} with preferred runner as Input Authority. EnableOnSingleRunner will always disable it.");
      
      if (comp is Renderer renderer) {
        _componentType = ComponentType.Renderer;
        _originalState = renderer.enabled;
        renderer.enabled = runner.GetVisible() && _originalState;
        //_node = node;
        Component = comp;
      } else if (comp is UnityEngine.Behaviour behaviour) {
        _componentType = ComponentType.Behaviour;
        _originalState = behaviour.enabled;
        behaviour.enabled = runner.GetVisible() && _originalState;
       // _node = node;
        Component = comp;
      }
    }

    /// <summary>
    /// Sets the visibility state of this node.
    /// </summary>
    /// <param name="enabled"></param>
    public void SetEnabled(bool enabled) {
      if (enabled) {

        // If this object was originally disabled, we will want to keep it that way, unless it looks like the user enabled the object directly since the last time this was called.
        if (_originalState == false) {

          // TODO: These only partially work
          // User has directly enabled this object - assume it is meant to be enabled
          if (Enabled) {
            _originalState = true;
          } else {
            // original state was disabled, so leave it that way.
            return;
          }
        }
        Enabled = true;

      } else {

        // TODO: These only partially work
        // Detect/store if user has manually disabled the component
        //if (_originalState == true && Enabled == false) {
        //  _originalState = false;
        //}
        
        Enabled = false;
      }
    }

    // Executes is input auth operation.
    internal bool IsInputAuth() {
      if (_networkObject && _networkObject.IsValid) {
        return _networkObject.HasInputAuthority;
      } 

      return false;
    }

    // Executes setup on single runner link operation.
    internal void SetupOnSingleRunnerLink(PreferredRunners preferredRunner) {
      PreferredRunner = preferredRunner;
      IsOnSingleRunner = true;
    }

    // Executes invoke refresh common object visibilities operation.
    internal void InvokeRefreshCommonObjectVisibilities(float time) {
      StopAllCoroutines();
      Invoke(nameof(RetryRefreshCommonLinks), time);
    }

    // Executes retry refresh common links operation.
    private void RetryRefreshCommonLinks() {
      NetworkRunnerVisibilityExtensions.RetryRefreshCommonLinks();
    }
  }
}

