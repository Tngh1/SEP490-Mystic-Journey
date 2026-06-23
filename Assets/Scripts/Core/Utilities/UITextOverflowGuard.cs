using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UITextOverflowGuard
{
    private const float MinReadableTmpSize = 10f;
    private const int MinReadableTextSize = 10;

    /// <summary>
    /// Dùng cho UI nhỏ:
    /// Quest slot, inventory slot, reward slot...
    /// Có autosize + ellipsis để tránh tràn.
    /// </summary>
    public static void ApplyCompact(GameObject root)
    {
        if (root == null)
            return;

        var tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
            ConfigureCompact(tmpTexts[i]);

        var legacyTexts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
            ConfigureCompact(legacyTexts[i]);
    }

    /// <summary>
    /// Dùng cho UI lớn:
    /// Quest detail, item detail, dialog...
    /// Giữ full text, không autosize.
    /// </summary>
    public static void ApplyExpanded(GameObject root)
    {
        if (root == null)
            return;

        var tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
            ConfigureExpanded(tmpTexts[i]);

        var legacyTexts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
            ConfigureExpanded(legacyTexts[i]);
    }

    /// <summary>
    /// Bật/tắt tham gia layout group.
    /// </summary>
    public static void SetLayoutParticipation(GameObject target, bool participates)
    {
        if (target == null)
            return;

        var element = target.GetComponent<LayoutElement>();
        if (element == null)
            element = target.AddComponent<LayoutElement>();

        element.ignoreLayout = !participates;
    }

    /// <summary>
    /// Force rebuild layout ngay lập tức.
    /// Dùng sau khi set text động.
    /// </summary>
    public static void RebuildLayout(Transform root)
    {
        if (root == null)
            return;

        var rect = root as RectTransform ?? root.GetComponent<RectTransform>();
        if (rect == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    // =========================
    // COMPACT
    // =========================

    private static void ConfigureCompact(TMP_Text text)
    {
        if (text == null)
            return;

        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;

        float currentSize = text.fontSize > 0 ? text.fontSize : 18f;

        text.enableAutoSizing = true;
        text.fontSizeMax = currentSize;
        text.fontSizeMin = Mathf.Clamp(
            currentSize * 0.65f,
            MinReadableTmpSize,
            currentSize
        );
    }

    private static void ConfigureCompact(Text text)
    {
        if (text == null)
            return;

        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = Mathf.Max(text.fontSize, MinReadableTextSize);
        text.resizeTextMinSize = Mathf.Clamp(
            Mathf.RoundToInt(text.fontSize * 0.65f),
            MinReadableTextSize,
            text.resizeTextMaxSize
        );
    }

    // =========================
    // EXPANDED
    // =========================

    private static void ConfigureExpanded(TMP_Text text)
    {
        if (text == null)
            return;

        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private static void ConfigureExpanded(Text text)
    {
        if (text == null)
            return;

        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }
}