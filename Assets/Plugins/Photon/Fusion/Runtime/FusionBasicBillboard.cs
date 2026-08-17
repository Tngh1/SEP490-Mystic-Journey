namespace Fusion {
  using UnityEngine;


  /// <summary>
  /// Component which automatically faces this GameObject toward the supplied Camera. If Camera == null, will face towards Camera.main.
  /// </summary>
  [Fusion.ScriptHelp(BackColor = ScriptHeaderBackColor.Olive)]
  [ExecuteAlways]
  public class FusionBasicBillboard : Fusion.Behaviour {

    /// <summary>
    /// Force a particular camera to billboard this object toward. Leave null to use Camera.main.
    /// </summary>
    [InlineHelp]
    public Camera Camera;

    // Camera find is expensive, so do it once per update for ALL implementations
    static float _lastCameraFindTime;
    static Camera _currentCam;

    // Callback invoked when FusionBasicBillboard becomes enabled and active in the scene hierarchy.
    // Subscribes to global game events and refreshes visible UI displays.
    private void OnEnable() {
      UpdateLookAt();
    }

    // Callback invoked when FusionBasicBillboard becomes disabled in the scene hierarchy.
    // Unregisters event listeners to prevent unintended callbacks while inactive.
    private void OnDisable() {
      transform.localRotation = default;
    }

    Camera MainCamera {
      set {
        _currentCam = value;
      }
      get {

        var time = Time.time;
        // Only look for the camera once per Update.
        if (time == _lastCameraFindTime)
          return _currentCam;

        _lastCameraFindTime = time;
        var cam = Camera.main;
        _currentCam = cam;
        return cam;
      }
    }

#if UNITY_EDITOR
    // Executes on draw gizmos operation.
    private void OnDrawGizmos() {
      LateUpdate();
    }
#endif

    // Executes late update operation.
    private void LateUpdate() {
      UpdateLookAt();
    }

    // Executes update look at operation.
    public void UpdateLookAt() {

      var cam = Camera ? Camera : MainCamera;

      if (cam) {
        if (enabled) {
          transform.rotation = cam.transform.rotation;
        }
      }
    }

    // Executes reset statics operation.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
      _currentCam = default;
      _lastCameraFindTime = default;
    }
  }
}