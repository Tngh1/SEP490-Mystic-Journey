using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RPG-style glowing rarity frame effect for inventory item slots, shop slots, and equipment slots.
/// Creates a 3-layer intense radiating aura matching the exact slot background shape.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIRarityFrameEffect : MonoBehaviour
{
    private Image ambientAura;  // Layer 1: Wide soft radiating aura (+20px)
    private Image outerGlow;    // Layer 2: Medium vivid aura (+12px)
    private Image innerGlow;    // Layer 3: Super crisp bright frame glow (+4px)

    private Color baseColor = Color.white;
    private float pulseStrength = 0.35f;
    private bool isVisible;

    public void Configure(string rarity, Image targetBorderImage = null)
    {
        EnsureGlowObjects(targetBorderImage);

        baseColor = GetRarityColor(rarity);
        pulseStrength = GetPulseStrength(rarity);
        isVisible = true;

        ApplyColors(1f);
        SetGlowActive(true);
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        SetGlowActive(visible);
    }

    private void Update()
    {
        if (!isVisible || pulseStrength <= 0f)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        ApplyColors(pulse);
    }

    private void EnsureGlowObjects(Image targetBorderImage)
    {
        Image slotBg = targetBorderImage;

        if (slotBg == null || slotBg.name == "Icon" || slotBg.name == "ItemIcon")
        {
            slotBg = transform.Find("Background")?.GetComponent<Image>()
                  ?? transform.Find("RarityBorder")?.GetComponent<Image>()
                  ?? GetComponent<Image>()
                  ?? transform.parent?.Find("Background")?.GetComponent<Image>()
                  ?? transform.parent?.GetComponent<Image>();
        }

        Sprite frameSprite = (slotBg != null && slotBg.name != "Icon") ? slotBg.sprite : null;
        Image.Type frameType = slotBg != null ? slotBg.type : Image.Type.Sliced;

        // Layer 1: Ambient Wide Aura (+20px)
        if (ambientAura == null)
            ambientAura = CreateGlowLayer("AmbientAura", new Vector2(20f, 20f), frameSprite, frameType);

        // Layer 2: Outer Glow Aura (+12px)
        if (outerGlow == null)
            outerGlow = CreateGlowLayer("OuterGlowAura", new Vector2(12f, 12f), frameSprite, frameType);

        // Layer 3: Inner Frame Glow (+4px)
        if (innerGlow == null)
            innerGlow = CreateGlowLayer("InnerGlowFrame", new Vector2(4f, 4f), frameSprite, frameType);

        ReorderGlowLayers();
    }

    private Image CreateGlowLayer(string layerName, Vector2 extraSize, Sprite sprite, Image.Type type)
    {
        Transform existing = transform.Find(layerName);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = gameObject.layer;
        obj.transform.SetParent(transform, false);

        var layoutElem = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
        layoutElem.ignoreLayout = true;

        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = extraSize;

        var img = obj.GetComponent<Image>();
        img.raycastTarget = false;

        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = type;
            if (sprite.border != Vector4.zero)
            {
                img.fillCenter = false;
            }
        }

        return img;
    }

    private void ReorderGlowLayers()
    {
        Transform bg = transform.Find("Background");
        int baseIndex = bg != null ? bg.GetSiblingIndex() + 1 : 0;

        if (ambientAura != null) ambientAura.transform.SetSiblingIndex(baseIndex);
        if (outerGlow != null) outerGlow.transform.SetSiblingIndex(baseIndex + 1);
        if (innerGlow != null) innerGlow.transform.SetSiblingIndex(baseIndex + 2);

        Transform icon = transform.Find("Icon") ?? transform.Find("Image");
        if (icon != null) icon.SetAsLastSibling();

        Transform qty = transform.Find("Quantity") ?? transform.Find("Quanlity") ?? transform.Find("QuantityText");
        if (qty != null) qty.SetAsLastSibling();
    }

    private void ApplyColors(float pulse)
    {
        float p = 1f - pulseStrength + pulseStrength * pulse;

        if (ambientAura != null)
        {
            Color c = baseColor;
            c.a = Mathf.Clamp01(0.40f * p);
            ambientAura.color = c;
        }

        if (outerGlow != null)
        {
            Color c = baseColor;
            c.a = Mathf.Clamp01(0.75f * p);
            outerGlow.color = c;
        }

        if (innerGlow != null)
        {
            Color c = baseColor;
            c.a = Mathf.Clamp01(1.00f * p);
            innerGlow.color = c;
        }
    }

    private void SetGlowActive(bool active)
    {
        if (ambientAura != null) ambientAura.gameObject.SetActive(active);
        if (outerGlow != null) outerGlow.gameObject.SetActive(active);
        if (innerGlow != null) innerGlow.gameObject.SetActive(active);
    }

    private static float GetPulseStrength(string rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity))
            return 0.30f;

        switch (rarity.Trim().ToLowerInvariant())
        {
            case "common":    return 0.30f;
            case "uncommon":  return 0.35f;
            case "rare":      return 0.40f;
            case "epic":      return 0.45f;
            case "legendary": return 0.55f;
            case "mythic":    return 0.65f;
            default:          return 0.30f;
        }
    }

    public static Color GetRarityColor(string rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity))
            return new Color(0.00f, 0.95f, 1.00f);

        switch (rarity.Trim().ToLowerInvariant())
        {
            case "common":    return new Color(0.00f, 0.95f, 1.00f); // Electric Ice Cyan
            case "uncommon":  return new Color(0.15f, 1.00f, 0.30f); // Bright Neon Lime Green
            case "rare":      return new Color(0.00f, 0.55f, 1.00f); // Vivid Hyper Royal Blue
            case "epic":      return new Color(0.90f, 0.15f, 1.00f); // Radiant Ultra Violet Purple
            case "legendary": return new Color(1.00f, 0.60f, 0.00f); // Blazing Sun Gold
            case "mythic":    return new Color(1.00f, 0.10f, 0.10f); // Fiery Plasma Red
            default:          return new Color(0.00f, 0.95f, 1.00f);
        }
    }
}
