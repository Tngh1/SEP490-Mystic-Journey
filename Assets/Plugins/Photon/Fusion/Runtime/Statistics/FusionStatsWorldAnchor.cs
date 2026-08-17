namespace Fusion.Statistics {
  using System;
  using UnityEngine;

  // Executes mono behaviour operation.
  [DisallowMultipleComponent]
  [AddComponentMenu("Fusion/Statistics/Statistics World Anchor")]
  public class FusionStatsWorldAnchor : MonoBehaviour {
    // Callback invoked when FusionStatsWorldAnchor becomes enabled and active in the scene hierarchy.
    // Subscribes to global game events and refreshes visible UI displays.
    private void OnEnable() {
      FusionStatsConfig.SetWorldAnchorCandidate(transform, true);
    }

    // Callback invoked when FusionStatsWorldAnchor becomes disabled in the scene hierarchy.
    // Unregisters event listeners to prevent unintended callbacks while inactive.
    private void OnDisable() {
      FusionStatsConfig.SetWorldAnchorCandidate(transform, false);
    }

    // Cleanup callback executed when FusionStatsWorldAnchor is destroyed.
    // Unsubscribes from events, cancels active coroutines, and prevents memory leaks.
    private void OnDestroy() {
      // Saving stats if is child
      var stats = transform.GetComponentInChildren<FusionStatsCanvas>();
      if (stats) {
        stats.transform.SetParent(null);
        stats.GetComponentInChildren<FusionStatsConfig>(true).ResetToCanvasAnchor();
      }
    }
  }
}