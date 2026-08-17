using TMPro;
using UnityEngine;

// Initializes a new default instance of the SilverFontResolver class.
public static class SilverFontResolver
{
    private static TMP_FontAsset cachedFont;

    // Executes font operation.
    public static TMP_FontAsset Font
    {
        get
        {
            if (cachedFont == null)  // Entity not found — short-circuit with appropriate error result
                cachedFont = Resources.Load<TMP_FontAsset>("SilverRuntimeFont");

            return cachedFont;
        }
    }
}
