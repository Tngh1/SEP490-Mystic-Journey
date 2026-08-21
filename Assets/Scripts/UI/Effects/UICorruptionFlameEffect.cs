using UnityEngine;
using UnityEngine.UI;

// Procedural purple flame overlay for the corruption HUD. The mesh is generated at
// runtime so the effect remains sharp at every Canvas scale and needs no texture asset.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UICorruptionFlameEffect : MaskableGraphic
{
    [SerializeField, Range(0f, 1f)] private float intensity;
    [SerializeField] private Color outerColor = new Color(0.28f, 0.01f, 0.62f, 0.38f);
    [SerializeField] private Color middleColor = new Color(0.62f, 0.06f, 1f, 0.68f);
    [SerializeField] private Color coreColor = new Color(0.96f, 0.34f, 1f, 0.9f);

    public float Intensity => intensity;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    // Updates flame density, height, brightness, and animation speed from a normalized value.
    public void SetIntensity(float normalized)
    {
        float next = Mathf.Clamp01(normalized);
        if (Mathf.Approximately(intensity, next) && enabled == (next > 0.002f))
            return;

        intensity = next;
        if (intensity <= 0.002f)
        {
            canvasRenderer.Clear();
            enabled = false;
            return;
        }

        enabled = true;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (intensity > 0.002f)
            SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (intensity <= 0.002f)
            return;

        Rect rect = rectTransform.rect;
        float power = Mathf.SmoothStep(0f, 1f, intensity);
        float time = Time.unscaledTime * Mathf.Lerp(1.4f, 4.8f, power);

        AddAura(vh, rect, power, time);

        int bottomFlames = Mathf.RoundToInt(Mathf.Lerp(5f, 17f, power));
        float xPadding = Mathf.Min(18f, rect.width * 0.08f);
        for (int i = 0; i < bottomFlames; i++)
        {
            float lane = (i + 0.5f) / bottomFlames;
            float noise = Mathf.PerlinNoise(i * 0.731f + 3.1f, time * 0.21f);
            float sway = Mathf.Sin(time + i * 1.91f) * Mathf.Lerp(1.2f, 5f, power);
            float x = Mathf.Lerp(rect.xMin + xPadding, rect.xMax - xPadding, lane) + sway;
            float width = Mathf.Lerp(4f, 12f, power) * Mathf.Lerp(0.75f, 1.2f, noise);
            float height = Mathf.Lerp(7f, 39f, power) * Mathf.Lerp(0.62f, 1.18f, noise);
            AddFlame(vh, new Vector2(x, rect.yMin + 3f), width, height, time + i, power);
        }

        int sideFlames = Mathf.RoundToInt(Mathf.Lerp(1f, 5f, power));
        for (int i = 0; i < sideFlames; i++)
        {
            float lane = (i + 1f) / (sideFlames + 1f);
            float y = Mathf.Lerp(rect.yMin + 10f, rect.yMax - 12f, lane);
            float noise = Mathf.PerlinNoise(11.7f + i, time * 0.18f);
            float width = Mathf.Lerp(3f, 8f, power);
            float height = Mathf.Lerp(5f, 20f, power) * Mathf.Lerp(0.7f, 1.2f, noise);
            AddFlame(vh, new Vector2(rect.xMin + 4f, y), width, height, time + i * 2.4f, power * 0.82f);
            AddFlame(vh, new Vector2(rect.xMax - 4f, y), width, height, time + i * 2.4f + 1.2f, power * 0.82f);
        }
    }

    private void AddAura(VertexHelper vh, Rect rect, float power, float time)
    {
        float pulse = 0.82f + Mathf.Sin(time * 1.7f) * 0.18f;
        Color glow = middleColor;
        glow.a *= Mathf.Lerp(0.10f, 0.42f, power) * pulse;

        float thickness = Mathf.Lerp(3f, 11f, power);
        AddQuad(vh,
            new Vector2(rect.xMin + 8f, rect.yMin + 1f),
            new Vector2(rect.xMax - 8f, rect.yMin + thickness),
            glow);

        Color sideGlow = outerColor;
        sideGlow.a *= Mathf.Lerp(0.08f, 0.32f, power) * pulse;
        AddQuad(vh,
            new Vector2(rect.xMin + 1f, rect.yMin + 5f),
            new Vector2(rect.xMin + thickness * 0.7f, rect.yMax - 5f),
            sideGlow);
        AddQuad(vh,
            new Vector2(rect.xMax - thickness * 0.7f, rect.yMin + 5f),
            new Vector2(rect.xMax - 1f, rect.yMax - 5f),
            sideGlow);
    }

    private void AddFlame(VertexHelper vh, Vector2 origin, float width, float height, float phase, float power)
    {
        float flicker = 0.88f + 0.12f * Mathf.Sin(phase * 2.3f);
        height *= flicker;
        float tipOffset = Mathf.Sin(phase * 1.37f) * width * 0.42f;

        Color outer = outerColor;
        outer.a *= Mathf.Lerp(0.38f, 1f, power);
        AddFlameShape(vh, origin, width, height, tipOffset, outer);

        Color middle = middleColor;
        middle.a *= Mathf.Lerp(0.42f, 1f, power);
        AddFlameShape(vh,
            origin + new Vector2(0f, 1f),
            width * 0.58f,
            height * 0.72f,
            tipOffset * 0.65f,
            middle);

        if (power > 0.28f)
        {
            Color core = coreColor;
            core.a *= Mathf.InverseLerp(0.28f, 1f, power);
            AddFlameShape(vh,
                origin + new Vector2(0f, 1.5f),
                width * 0.27f,
                height * 0.42f,
                tipOffset * 0.35f,
                core);
        }
    }

    private static void AddFlameShape(
        VertexHelper vh,
        Vector2 origin,
        float width,
        float height,
        float tipOffset,
        Color color)
    {
        int start = vh.currentVertCount;
        Vector2 uv = new Vector2(0.5f, 0.5f);

        vh.AddVert(origin + new Vector2(-width * 0.5f, 0f), color, uv);
        vh.AddVert(origin + new Vector2(-width * 0.58f, height * 0.34f), color, uv);
        vh.AddVert(origin + new Vector2(tipOffset, height), color, uv);
        vh.AddVert(origin + new Vector2(width * 0.58f, height * 0.34f), color, uv);
        vh.AddVert(origin + new Vector2(width * 0.5f, 0f), color, uv);

        vh.AddTriangle(start, start + 1, start + 4);
        vh.AddTriangle(start + 1, start + 3, start + 4);
        vh.AddTriangle(start + 1, start + 2, start + 3);
    }

    private static void AddQuad(VertexHelper vh, Vector2 min, Vector2 max, Color color)
    {
        int start = vh.currentVertCount;
        Vector2 uv = new Vector2(0.5f, 0.5f);

        vh.AddVert(new Vector2(min.x, min.y), color, uv);
        vh.AddVert(new Vector2(min.x, max.y), color, uv);
        vh.AddVert(new Vector2(max.x, max.y), color, uv);
        vh.AddVert(new Vector2(max.x, min.y), color, uv);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
