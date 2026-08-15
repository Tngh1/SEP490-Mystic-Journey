using TMPro;
using UnityEngine;

/// <summary>
/// Resolves the shared Silver SDF font for runtime-generated combat and loot text.
/// </summary>
public static class SilverFontResolver
{
    private static TMP_FontAsset cachedFont;

    public static TMP_FontAsset Font
    {
        get
        {
            if (cachedFont == null)
                cachedFont = Resources.Load<TMP_FontAsset>("SilverRuntimeFont");

            return cachedFont;
        }
    }
}
