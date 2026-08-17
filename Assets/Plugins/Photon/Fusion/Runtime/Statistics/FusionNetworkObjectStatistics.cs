namespace Fusion.Statistics {
using UnityEngine;

  // Executes mono behaviour operation.
  [RequireComponent(typeof(NetworkObject)), DisallowMultipleComponent]
  [AddComponentMenu("Fusion/Statistics/Network Object Statistics")]
  public class FusionNetworkObjectStatistics : MonoBehaviour {
    [HideInInspector]
    public NetworkObject NetworkObject;

    // Executes toggle monitoring operation.
    private void ToggleMonitoring(bool value) {
      NetworkObject = GetComponent<NetworkObject>();
      if (NetworkObject.Runner && NetworkObject.Runner.IsRunning) {
        if (NetworkObject.Runner.TryGetComponent<FusionStatistics>(out var statistics)) {
          if (statistics.MonitorNetworkObject(NetworkObject, this, value))
            return;
        }
      }

      // If not running or don't have the statistics manager or NO is already added on the graph, destroy for now.
      Destroy(this);
    }

    // Callback invoked when FusionNetworkObjectStatistics becomes enabled and active in the scene hierarchy.
    // Subscribes to global game events and refreshes visible UI displays.
    private void OnEnable() {
      ToggleMonitoring(true);
    }

    // Callback invoked when FusionNetworkObjectStatistics becomes disabled in the scene hierarchy.
    // Unregisters event listeners to prevent unintended callbacks while inactive.
    private void OnDisable() {
      ToggleMonitoring(false);
    }
  }
}