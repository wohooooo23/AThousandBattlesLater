using TMPro;
using UnityEngine;

/// <summary>
/// Single source for the two authored UI faces. English uses the pixel face while Chinese uses the
/// bundled CJK face; LocalizedText swaps them together with the translated string.
/// </summary>
public static class UiFont
{
    private const string EnglishResourcePath = "Fonts/BoldPixels";
    private const string ChineseResourcePath = "Fonts/ZCOOLXiaoWei-Regular";
    private const string EnglishTmpResourcePath = "Fonts/BoldPixels SDF";
    private const string ChineseTmpResourcePath = "Fonts/ZCOOLXiaoWei SDF";

    private static Font cachedEnglish;
    private static Font cachedChinese;
    private static TMP_FontAsset cachedEnglishTmp;
    private static TMP_FontAsset cachedChineseTmp;

    public static Font English
    {
        get
        {
            if (cachedEnglish != null)
                return cachedEnglish;
            cachedEnglish = Resources.Load<Font>(EnglishResourcePath);
            if (cachedEnglish == null)
            {
                Debug.LogWarning("Missing " + EnglishResourcePath + "; using Unity's fallback font.");
                cachedEnglish = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return cachedEnglish;
        }
    }

    public static Font Chinese => cachedChinese != null
        ? cachedChinese
        : cachedChinese = Resources.Load<Font>(ChineseResourcePath);

    public static Font Current => Localization.Current == GameLanguage.Chinese && Chinese != null
        ? Chinese
        : English;

    /// <summary>English authoring face retained for editor builders.</summary>
    public static Font Regular => English;

    public static TMP_FontAsset TmpEnglish
    {
        get
        {
            if (cachedEnglishTmp != null)
                return cachedEnglishTmp;
            cachedEnglishTmp = Resources.Load<TMP_FontAsset>(EnglishTmpResourcePath);
            if (cachedEnglishTmp == null)
                Debug.LogError("Missing " + EnglishTmpResourcePath + ".");
            return cachedEnglishTmp;
        }
    }

    public static TMP_FontAsset TmpChinese => cachedChineseTmp != null
        ? cachedChineseTmp
        : cachedChineseTmp = Resources.Load<TMP_FontAsset>(ChineseTmpResourcePath);

    public static TMP_FontAsset TmpCurrent => Localization.Current == GameLanguage.Chinese && TmpChinese != null
        ? TmpChinese
        : TmpEnglish;

    /// <summary>Compatibility alias for systems that explicitly request the Chinese TMP face.</summary>
    public static TMP_FontAsset TmpRegular => TmpChinese;
}
