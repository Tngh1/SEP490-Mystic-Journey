using UnityEngine;

// Executes mono behaviour operation.
[RequireComponent(typeof(Camera))]
public class MinimapCameraController : MonoBehaviour
{
    // Executes instance operation.
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

    // Executes camera operation.
    public Camera Camera { get; private set; }

    // Executes target operation.
    public Transform Target { get; private set; }

    private bool _fullMap;
    private Vector3 _fullMapCenter;
    private float _fullMapZoom;

    private RenderTexture _minimapTexture;

    // Initializes internal component caches and dependencies for MinimapCameraController upon GameObject instantiation.
    // Executes during scene loading prior to Start to ensure critical references are wired up.
    private void Awake()
    {
        Instance = this;

        Camera = GetComponent<Camera>();
        Camera.orthographic = true;
        Camera.orthographicSize = zoom;
        Camera.cullingMask &= ~hiddenLayers.value;
        _minimapTexture = Camera.targetTexture;

        var listener = GetComponent<AudioListener>();
        if (listener != null) listener.enabled = false;
    }

    // Unsubscribe this component's event handlers and release its temporary runtime resources.
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Executes initialize minimap operation.
    public void InitializeMinimap(Transform targetTransform)
    {
        Target = targetTransform;
        if (Target == null) return;

        transform.position = FocusPosition();
    }

    // Executes show full map operation.
    public void ShowFullMap()
    {
        if (!TryComputeWorldBounds(out Bounds b)) return;

        if (fullMapTexture != null && Camera.targetTexture != fullMapTexture)
            Camera.targetTexture = fullMapTexture;

        _fullMapCenter = new Vector3(b.center.x, b.center.y, cameraZOffset);

        float aspect = CurrentAspect();
        _fullMapZoom = Mathf.Max(b.extents.y, b.extents.x / aspect) * Mathf.Max(1f, fullMapPadding);

        _fullMap = true;
        transform.position = _fullMapCenter;
        Camera.orthographicSize = _fullMapZoom;
    }

    // Executes show minimap operation.
    public void ShowMinimap()
    {
        _fullMap = false;

        if (_minimapTexture != null && Camera.targetTexture != _minimapTexture)
            Camera.targetTexture = _minimapTexture;

        Camera.orthographicSize = zoom;
        if (Target != null) transform.position = FocusPosition();
    }

    // Executes active texture operation.
    public RenderTexture ActiveTexture
    {
        get { return Camera != null ? Camera.targetTexture : null; }
    }

    // Executes current aspect operation.
    private float CurrentAspect()
    {
        var rt = Camera.targetTexture;
        if (rt != null && rt.height > 0)
            return (float)rt.width / rt.height;

        return Camera.aspect > 0.0001f ? Camera.aspect : 1f;
    }

    // Executes try compute world bounds operation.
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

    // Executes late update operation.
    private void LateUpdate()
    {
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

        if (!Mathf.Approximately(Camera.orthographicSize, zoom)) Camera.orthographicSize = zoom;
    }

    // Executes focus position operation.
    private Vector3 FocusPosition()
    {
        Vector3 p = Target.position;
        p.z = cameraZOffset;
        return p;
    }
}
