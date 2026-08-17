
namespace Fusion {

  using UnityEngine;

  /// <summary>
  /// Flag component which indicates a NetworkObject has already been factored into a Runner's VisibilityNode list.
  /// </summary>
  [AddComponentMenu("")]
  internal class RunnerVisibilityLinksRoot : MonoBehaviour {
    // Initializes internal component caches and dependencies for RunnerVisibilityLinksRoot upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake() {
      this.hideFlags = HideFlags.HideInInspector;
    }
  }
}
