using UnityEngine;

/// <summary>
/// Single source for the legacy-UI font. The project used Unity's built-in LegacyRuntime.ttf, which
/// has no CJK glyphs, so every Chinese string rendered as boxes. Noto Sans SC covers Latin *and*
/// Chinese, so one font serves both languages and nothing has to swap fonts when the player
/// switches language.
///
/// Loaded from Resources so runtime code and the editor builders resolve the same asset.
/// </summary>
public static class UiFont
{
    private const string ResourcePath = "Fonts/NotoSansSC-Regular";

    private static Font cached;

    /// <summary>The shared UI font; falls back to Unity's built-in font if the asset is missing.</summary>
    public static Font Regular
    {
        get
        {
            if (cached != null)
                return cached;
            cached = Resources.Load<Font>(ResourcePath);
            if (cached == null)
            {
                Debug.LogWarning("Missing " + ResourcePath + " — Chinese text will render as boxes.");
                cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return cached;
        }
    }
}
