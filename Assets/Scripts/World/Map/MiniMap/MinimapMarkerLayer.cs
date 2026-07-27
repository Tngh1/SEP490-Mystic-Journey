using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws player markers on top of the minimap render texture: the local player
/// plus every other player in the current Photon Fusion session, with their name.
/// Markers outside the minimap view are clamped to the edge so you always know
/// which direction a teammate is.
///
/// Attach to the masked minimap viewport (the RectTransform that holds the
/// minimap RawImage). Markers are generated at runtime — no prefab wiring.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MinimapMarkerLayer : MonoBehaviour
{
    [Header("Marker look")]
    [SerializeField] private float markerSize = 14f;
    [SerializeField] private Color localColor = new Color(0.35f, 0.85f, 1f);
    [SerializeField] private Color remoteColor = new Color(1f, 0.85f, 0.30f);

    [Tooltip("Show the player name under remote markers.")]
    [SerializeField] private bool showRemoteNames = true;

    [Tooltip("Clamp off-screen players to the minimap border instead of hiding them.")]
    [SerializeField] private bool clampToEdge = true;

    [Tooltip("Alpha applied to markers that are clamped to the border.")]
    [SerializeField, Range(0f, 1f)] private float clampedAlpha = 0.5f;

    private RectTransform _rect;
    private Transform _markerRoot;

    private readonly Dictionary<Object, Marker> _markers = new();
    private readonly List<Object> _stale = new();

    private static Sprite _dotSprite;

    private sealed class Marker
    {
        public RectTransform Rect;
        public Image Icon;
        public TMP_Text Label;
    }

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();

        var root = new GameObject("MinimapMarkers", typeof(RectTransform)) { layer = gameObject.layer };
        _markerRoot = root.transform;
        var rootRect = (RectTransform)_markerRoot;
        rootRect.SetParent(_rect, worldPositionStays: false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        var minimap = MinimapCameraController.Instance;
        if (minimap == null || minimap.Camera == null)
        {
            HideAll();
            return;
        }

        foreach (var pair in _markers) pair.Value.Rect.gameObject.SetActive(false);

        // Multiplayer avatars (Fusion). NetworkPlayer.All contains every player in
        // the session, including the local one.
        var players = NetworkPlayer.All;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;

            bool isLocal = p == NetworkPlayer.Local;
            DrawMarker(minimap.Camera, p, p.transform.position, isLocal,
                       isLocal ? null : p.PlayerName.ToString());
        }

        // Offline / single-player path: no networked avatar, so fall back to the
        // camera target (the local player PlayerSpawner handed us).
        if (players.Count == 0 && minimap.Target != null)
            DrawMarker(minimap.Camera, minimap.Target, minimap.Target.position, true, null);

        PruneDestroyed();
    }

    private void DrawMarker(Camera cam, Object owner, Vector3 worldPos, bool isLocal, string label)
    {
        Vector3 viewport = cam.WorldToViewportPoint(worldPos);
        Vector2 centered = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f);

        bool offView = Mathf.Abs(centered.x) > 0.5f || Mathf.Abs(centered.y) > 0.5f;
        if (offView)
        {
            if (!clampToEdge) return;
            centered.x = Mathf.Clamp(centered.x, -0.5f, 0.5f);
            centered.y = Mathf.Clamp(centered.y, -0.5f, 0.5f);
        }

        var marker = GetOrCreate(owner, isLocal, label);
        marker.Rect.gameObject.SetActive(true);

        Rect area = _rect.rect;
        marker.Rect.anchoredPosition = new Vector2(centered.x * area.width, centered.y * area.height);

        Color c = isLocal ? localColor : remoteColor;
        c.a = offView ? clampedAlpha : 1f;
        marker.Icon.color = c;

        if (marker.Label != null)
        {
            marker.Label.gameObject.SetActive(!offView && !string.IsNullOrEmpty(label));
            if (!string.IsNullOrEmpty(label) && marker.Label.text != label) marker.Label.text = label;
        }
    }

    private Marker GetOrCreate(Object owner, bool isLocal, string label)
    {
        if (_markers.TryGetValue(owner, out var existing)) return existing;

        var go = new GameObject(isLocal ? "Marker_Local" : "Marker_Remote", typeof(RectTransform)) { layer = gameObject.layer };
        var rect = (RectTransform)go.transform;
        rect.SetParent(_markerRoot, worldPositionStays: false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * (isLocal ? markerSize * 1.3f : markerSize);

        var icon = go.AddComponent<Image>();
        icon.sprite = DotSprite();
        icon.raycastTarget = false;

        Marker marker = new Marker { Rect = rect, Icon = icon };

        if (!isLocal && showRemoteNames)
        {
            var labelGo = new GameObject("Name", typeof(RectTransform)) { layer = gameObject.layer };
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rect, worldPositionStays: false);
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -2f);
            labelRect.sizeDelta = new Vector2(90f, 14f);

            var text = labelGo.AddComponent<TextMeshProUGUI>();
            text.text = label ?? string.Empty;
            text.fontSize = 10f;
            text.alignment = TextAlignmentOptions.Top;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            marker.Label = text;
        }

        _markers[owner] = marker;
        return marker;
    }

    private void PruneDestroyed()
    {
        _stale.Clear();
        foreach (var pair in _markers)
        {
            if (pair.Key == null) _stale.Add(pair.Key);
        }

        foreach (var key in _stale)
        {
            if (_markers.TryGetValue(key, out var marker) && marker.Rect != null)
                Destroy(marker.Rect.gameObject);
            _markers.Remove(key);
        }
    }

    private void HideAll()
    {
        foreach (var pair in _markers)
        {
            if (pair.Value.Rect != null) pair.Value.Rect.gameObject.SetActive(false);
        }
    }

    // ponytail: procedural 1px dot scaled by the Image instead of an art asset —
    // swap in a proper marker sprite (arrow for local, class icon for remotes)
    // when art is available.
    private static Sprite DotSprite()
    {
        if (_dotSprite != null) return _dotSprite;

        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var center = new Vector2(7.5f, 7.5f);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                // Soft-edged circle with a darker rim so markers read on any terrain.
                float a = Mathf.Clamp01(7.5f - d);
                float rim = d > 5.5f ? 0.45f : 1f;
                tex.SetPixel(x, y, new Color(rim, rim, rim, a));
            }
        }
        tex.Apply();

        _dotSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        _dotSprite.name = "MinimapDot";
        return _dotSprite;
    }
}
