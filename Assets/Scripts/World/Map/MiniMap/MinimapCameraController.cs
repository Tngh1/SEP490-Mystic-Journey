using UnityEngine;

/// <summary>
/// Drives the minimap camera. It follows the local player from far above with a
/// zoomed-out orthographic view (a map, not a second gameplay view) and renders
/// into RT_Minimap. While the map panel is open it stops following and frames the
/// whole level into the wider full-map texture instead, so the level is not
/// letterboxed inside the panel frame. Player icons are drawn as UI on top of that
/// texture by <see cref="MinimapMarkerLayer"/> — this component only owns the camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MinimapCameraController : MonoBehaviour
{
    /// <summary>Active minimap camera, used by the marker layer to project world positions.</summary>
    public static MinimapCameraController Instance { get; private set; }

    [Header("View")]
    [Tooltip("Orthographic size of the minimap view. Larger = more world visible.")]
    [SerializeField] private float zoom = 20f;

    [Tooltip("Layers the minimap must not render (UI, VFX, ...).")]
    [SerializeField] private LayerMask hiddenLayers;

    [Header("Full map (map panel)")]
    [Tooltip("Render texture used while the map panel is open. Its aspect must match " +
             "the panel's RawImage, otherwise the level is letterboxed inside the frame. " +
             "Leave empty to keep rendering into the minimap texture.")]
    [SerializeField] private RenderTexture fullMapTexture;

    [Tooltip("Margin left around the level in full-map view. 1 = level edges touch the frame.")]
    [Range(1f, 1.5f)]
    [SerializeField] private float fullMapPadding = 1.02f;

    [Header("Follow")]
    [Tooltip("Follow smoothing. 0 = snap instantly to the target.")]
    [SerializeField] private float followSmoothing = 12f;

    [SerializeField] private float cameraZOffset = -20f;

    public Camera Camera { get; private set; }

    /// <summary>The transform the minimap is centered on (the local player).</summary>
    public Transform Target { get; private set; }

    // Full-map mode: the camera stops following and frames the whole level so the
    // map panel can show every player at once.
    private bool _fullMap;
    private Vector3 _fullMapCenter;
    private float _fullMapZoom;

    // Texture the camera renders into while following the player. Restored when the
    // map panel closes, so the HUD minimap keeps its own square texture.
    private RenderTexture _minimapTexture;

    private void Awake()
    {
        Instance = this;

        Camera = GetComponent<Camera>();
        Camera.orthographic = true;
        Camera.orthographicSize = zoom;
        Camera.cullingMask &= ~hiddenLayers.value;
        _minimapTexture = Camera.targetTexture;

        // The gameplay camera owns audio; a second listener spams warnings and
        // breaks 2D spatial panning.
        var listener = GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Bind the minimap to a player transform. Called by the spawn paths
    /// (PlayerSpawner / NetworkPlayer / MapSceneController / DungeonManager)
    /// whenever the local avatar changes.
    /// </summary>
    public void InitializeMinimap(Transform targetTransform)
    {
        Target = targetTransform;
        if (Target == null) return;

        // Snap immediately so the map does not slide in from the previous scene.
        transform.position = FocusPosition();
    }

    /// <summary>
    /// Frame the whole level instead of following the player, so the map panel can
    /// show every player at once. Falls back to normal follow if nothing renderable
    /// is loaded.
    /// </summary>
    public void ShowFullMap()
    {
        if (!TryComputeWorldBounds(out Bounds b)) return;

        // Render into the wide panel texture first: the fit below must use the
        // aspect the player will actually see, not the square minimap one.
        if (fullMapTexture != null && Camera.targetTexture != fullMapTexture)
            Camera.targetTexture = fullMapTexture;

        _fullMapCenter = new Vector3(b.center.x, b.center.y, cameraZOffset);

        // Orthographic size is half the vertical extent; also fit the width through
        // the aspect ratio, then pad so the level edges are not flush with the frame.
        float aspect = CurrentAspect();
        _fullMapZoom = Mathf.Max(b.extents.y, b.extents.x / aspect) * Mathf.Max(1f, fullMapPadding);

        _fullMap = true;
        transform.position = _fullMapCenter;
        Camera.orthographicSize = _fullMapZoom;
    }

    /// <summary>Go back to following the local player at minimap zoom.</summary>
    public void ShowMinimap()
    {
        _fullMap = false;

        if (_minimapTexture != null && Camera.targetTexture != _minimapTexture)
            Camera.targetTexture = _minimapTexture;

        Camera.orthographicSize = zoom;
        if (Target != null) transform.position = FocusPosition();
    }

    /// <summary>
    /// Texture the camera is rendering into right now. The map panel binds its
    /// RawImage to this so it follows the full-map/minimap swap.
    /// </summary>
    public RenderTexture ActiveTexture
    {
        get { return Camera != null ? Camera.targetTexture : null; }
    }

    // Camera.aspect only picks up a new target texture on the next render, so read
    // the texture directly — ShowFullMap needs the value in the same frame.
    private float CurrentAspect()
    {
        var rt = Camera.targetTexture;
        if (rt != null && rt.height > 0)
            return (float)rt.width / rt.height;

        return Camera.aspect > 0.0001f ? Camera.aspect : 1f;
    }

    // ponytail: bounds are derived from whatever renderers are loaded when the panel
    // opens — good enough for one-scene-per-map. Author explicit bounds on MapData
    // if maps ever stream in pieces or contain far-away decor that skews the frame.
    private bool TryComputeWorldBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        int mask = Camera.cullingMask;

        var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if ((mask & (1 << r.gameObject.layer)) == 0) continue;
            if (r is ParticleSystemRenderer) continue;

            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return any;
    }

    private void LateUpdate()
    {
        // Full-map view is static: nothing to follow, and the zoom must not snap back.
        if (_fullMap)
        {
            transform.position = _fullMapCenter;
            if (!Mathf.Approximately(Camera.orthographicSize, _fullMapZoom))
                Camera.orthographicSize = _fullMapZoom;
            return;
        }

        if (Target == null) return;

        Vector3 wanted = FocusPosition();
        transform.position = followSmoothing <= 0f
            ? wanted
            : Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-followSmoothing * Time.deltaTime));

        // Keep zoom live-editable while playing.
        if (!Mathf.Approximately(Camera.orthographicSize, zoom)) Camera.orthographicSize = zoom;
    }

    private Vector3 FocusPosition()
    {
        Vector3 p = Target.position;
        p.z = cameraZOffset;
        return p;
    }
}
