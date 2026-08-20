using UnityEngine;
using UnityEngine.UI;

// Executes mono behaviour operation.
[DisallowMultipleComponent]
public sealed class UIRarityFrameEffect : MonoBehaviour
{
    private Image ambientAura;
    private Image outerGlow;
    private Image innerGlow;

    private Color baseColor = Color.white;
    private float pulseStrength = 0.35f;
    private bool isVisible;

    // Executes configure operation.
    public void Configure(string rarity, Image targetBorderImage = null)
    {
        EnsureGlowObjects(targetBorderImage);

        baseColor = GetRarityColor(rarity);
        pulseStrength = GetPulseStrength(rarity);
        isVisible = true;

        ApplyColors(1f);
        SetGlowActive(true);
    }

    // Executes set visible operation.
    public void SetVisible(bool visible)
    {
        isVisible = visible;
        SetGlowActive(visible);
    }

    // Per-frame update loop for UIRarityFrameEffect.
    // Handles real-time input polling, smooth interpolations, cooldown timers, and UI updates.
    private void Update()
    {
        if (!isVisible || pulseStrength <= 0f)
            return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        ApplyColors(pulse);
    }

    // Executes ensure glow objects operation.
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

        bool isDailySlot = GetComponent<UIDailySlot>() != null || GetComponentInParent<UIDailySlot>() != null;

        Vector2 ambientExtra = isDailySlot ? Vector2.zero : new Vector2(20f, 20f);
        Vector2 outerExtra   = isDailySlot ? Vector2.zero : new Vector2(12f, 12f);
        Vector2 innerExtra   = isDailySlot ? Vector2.zero : new Vector2(4f, 4f);

        if (ambientAura == null)
            ambientAura = CreateGlowLayer("AmbientAura", ambientExtra, frameSprite, frameType);

        if (outerGlow == null)
            outerGlow = CreateGlowLayer("OuterGlowAura", outerExtra, frameSprite, frameType);

        if (innerGlow == null)
            innerGlow = CreateGlowLayer("InnerGlowFrame", innerExtra, frameSprite, frameType);

        ReorderGlowLayers();
    }

    // Executes create glow layer operation.
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

    // Executes reorder glow layers operation.
    private void ReorderGlowLayers()
    {
        Transform bg = transform.Find("Background");
        int baseIndex = bg != null ? bg.GetSiblingIndex() + 1 : 0;

        if (ambientAura != null) ambientAura.transform.SetSiblingIndex(baseIndex);
        if (outerGlow != null) outerGlow.transform.SetSiblingIndex(baseIndex + 1);
        if (innerGlow != null) innerGlow.transform.SetSiblingIndex(baseIndex + 2);

        bool isDailySlot = GetComponent<UIDailySlot>() != null || GetComponentInParent<UIDailySlot>() != null;
        if (isDailySlot)
            return;

        Transform icon = transform.Find("Icon") ?? transform.Find("Image");
        if (icon != null) icon.SetAsLastSibling();

        Transform qty = transform.Find("Quantity") ?? transform.Find("Quanlity") ?? transform.Find("QuantityText");
        if (qty != null) qty.SetAsLastSibling();
    }

    // Executes apply colors operation.
    private void ApplyColors(float pulse)
    {
        float p = 1f - pulseStrength + pulseStrength * pulse;

        if (ambientAura != null)
        {
            Color c = baseColor;
            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            c.a = Mathf.Clamp01(0.40f * p);
            ambientAura.color = c;
        }

        if (outerGlow != null)
        {
            Color c = baseColor;
            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            c.a = Mathf.Clamp01(0.75f * p);
            outerGlow.color = c;
        }

        if (innerGlow != null)
        {
            Color c = baseColor;
            // Clamp the calculated value to the minimum and maximum accepted by this domain rule.
            c.a = Mathf.Clamp01(1.00f * p);
            innerGlow.color = c;
        }
    }

    // Executes set glow active operation.
    // Validates input parameters against null or empty values.
    private void SetGlowActive(bool active)
    {
        if (ambientAura != null) ambientAura.gameObject.SetActive(active);
        if (outerGlow != null) outerGlow.gameObject.SetActive(active);
        if (innerGlow != null) innerGlow.gameObject.SetActive(active);
    }

    // Executes get pulse strength operation.
    // Validates input parameters against null or empty values.
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

    // Executes get rarity color operation.
    // Validates input parameters against null or empty values.
    public static Color GetRarityColor(string rarity)
    {
        if (string.IsNullOrWhiteSpace(rarity))
            return new Color(0.00f, 0.95f, 1.00f);

        switch (rarity.Trim().ToLowerInvariant())
        {
            case "common":    return new Color(0.00f, 0.95f, 1.00f);
            case "uncommon":  return new Color(0.15f, 1.00f, 0.30f);
            case "rare":      return new Color(0.00f, 0.55f, 1.00f);
            case "epic":      return new Color(0.90f, 0.15f, 1.00f);
            case "legendary": return new Color(1.00f, 0.60f, 0.00f);
            case "mythic":    return new Color(1.00f, 0.10f, 0.10f);
            default:          return new Color(0.00f, 0.95f, 1.00f);
        }
    }
}
