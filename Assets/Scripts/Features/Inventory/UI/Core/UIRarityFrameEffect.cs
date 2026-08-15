using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sprite-independent rarity frame for inventory item slots.
/// Builds four core edges and four soft glow edges under the existing RarityBorder object.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIRarityFrameEffect : MonoBehaviour
{
    private const float CoreThickness = 3f;
    private const float GlowThickness = 7f;

    private readonly Image[] coreEdges = new Image[4];
    private readonly Image[] glowEdges = new Image[4];

    private Color baseColor = Color.white;
    private float pulseStrength;
    private bool isVisible;

    public void Configure(string rarity)
    {
        EnsureEdges();

        baseColor = GetRarityColor(rarity);
        pulseStrength = GetPulseStrength(rarity);
        isVisible = true;
        ApplyColors(1f);
        SetEdgesActive(true);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        SetEdgesActive(visible);
    }

    private void Update()
    {
        if (!isVisible || pulseStrength <= 0f)
            return;

        float pulse = 1f - pulseStrength + pulseStrength * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f));
        ApplyColors(pulse);
    }

    private void EnsureEdges()
    {
        if (coreEdges[0] != null)
            return;

        for (int i = 0; i < 4; i++)
        {
            glowEdges[i] = CreateEdge($"Glow_{i}", i, GlowThickness);
            coreEdges[i] = CreateEdge($"Core_{i}", i, CoreThickness);
        }
    }

    private Image CreateEdge(string edgeName, int edgeIndex, float thickness)
    {
        var edge = new GameObject(edgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        edge.layer = gameObject.layer;
        edge.transform.SetParent(transform, false);

        var rect = edge.GetComponent<RectTransform>();
        switch (edgeIndex)
        {
            case 0: // top
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, thickness);
                break;
            case 1: // bottom
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(0f, thickness);
                break;
            case 2: // left
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(thickness, 0f);
                break;
            default: // right
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(thickness, 0f);
                break;
        }

        rect.anchoredPosition = Vector2.zero;
        var image = edge.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void ApplyColors(float pulse)
    {
        Color core = baseColor;
        core.a = Mathf.Clamp01(0.72f + 0.28f * pulse);

        Color glow = baseColor;
        glow.a = Mathf.Clamp01(0.12f + 0.20f * pulse);

        for (int i = 0; i < 4; i++)
        {
            if (coreEdges[i] != null) coreEdges[i].color = core;
            if (glowEdges[i] != null) glowEdges[i].color = glow;
        }
    }

    private void SetEdgesActive(bool active)
    {
        for (int i = 0; i < 4; i++)
        {
            if (coreEdges[i] != null) coreEdges[i].gameObject.SetActive(active);
            if (glowEdges[i] != null) glowEdges[i].gameObject.SetActive(active);
        }
    }

    private static float GetPulseStrength(string rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity))
            return 0f;

        switch (rarity.Trim().ToLowerInvariant())
        {
            case "epic": return 0.18f;
            case "legendary": return 0.32f;
            case "mythic": return 0.42f;
            default: return 0f;
        }
    }

    public static Color GetRarityColor(string rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity))
            return new Color(0.72f, 0.75f, 0.8f);

        switch (rarity.Trim().ToLowerInvariant())
        {
            case "common": return new Color(0.72f, 0.75f, 0.8f);
            case "uncommon": return new Color(0.30f, 0.88f, 0.40f);
            case "rare": return new Color(0.22f, 0.55f, 1f);
            case "epic": return new Color(0.72f, 0.32f, 1f);
            case "legendary": return new Color(1f, 0.62f, 0.08f);
            case "mythic": return new Color(1f, 0.3f, 0.3f);
            default: return new Color(0.72f, 0.75f, 0.8f);
        }
    }
}
