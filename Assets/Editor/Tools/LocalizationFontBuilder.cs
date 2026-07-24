#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives TextMeshPro the ability to render Chinese.
///
/// The dialogue bubbles use LiberationSans SDF, which has no CJK glyphs, so Chinese rendered as
/// blank boxes. Rather than replace it (which would change the English look), this generates a
/// Noto Sans SC font asset and registers it as LiberationSans' *fallback*: English keeps its
/// current face and Chinese resolves through the fallback automatically.
///
/// The fallback is created in Dynamic atlas mode on purpose — statically baking a full CJK set
/// would produce a multi-hundred-megabyte atlas. Dynamic rasterises only the glyphs actually used.
/// </summary>
public static class LocalizationFontBuilder
{
    private const string SourceFontPath = "Assets/Resources/Fonts/NotoSansSC-Regular.ttf";
    private const string FallbackAssetPath = "Assets/Resources/Fonts/NotoSansSC SDF.asset";
    private const string PrimaryAssetPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("Tools/Localization/Build Chinese Font Fallback")]
    public static void Build()
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null)
            throw new InvalidOperationException("Missing Chinese font at " + SourceFontPath +
                ". Import NotoSansSC-Regular.ttf first.");

        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackAssetPath);
        if (fallback == null)
        {
            // 2048 atlas, dynamic population: glyphs are rasterised on demand as lines are shown.
            fallback = TMP_FontAsset.CreateFontAsset(source, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic);
            if (fallback == null)
                throw new InvalidOperationException("TMP could not create a font asset from " + SourceFontPath + ".");
            fallback.name = Path.GetFileNameWithoutExtension(FallbackAssetPath);
            AssetDatabase.CreateAsset(fallback, FallbackAssetPath);
            // The atlas texture and material are sub-assets of the font asset.
            if (fallback.atlasTextures != null && fallback.atlasTextures.Length > 0)
            {
                fallback.atlasTextures[0].name = fallback.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fallback.atlasTextures[0], fallback);
            }
            if (fallback.material != null)
            {
                fallback.material.name = fallback.name + " Material";
                AssetDatabase.AddObjectToAsset(fallback.material, fallback);
            }
        }

        TMP_FontAsset primary = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryAssetPath);
        if (primary == null)
            throw new InvalidOperationException("TMP Essential Resources are required at " + PrimaryAssetPath + ".");

        primary.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        if (!primary.fallbackFontAssetTable.Contains(fallback))
        {
            primary.fallbackFontAssetTable.Add(fallback);
            EditorUtility.SetDirty(primary);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("LOCALIZATION_FONT_OK: NotoSansSC SDF (Dynamic) registered as a fallback on LiberationSans SDF.");
    }

    [MenuItem("Tools/Localization/Validate Chinese Font Fallback")]
    public static void Validate()
    {
        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackAssetPath);
        if (fallback == null)
            throw new InvalidOperationException("Missing " + FallbackAssetPath + " — run Build Chinese Font Fallback.");
        if (fallback.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            throw new InvalidOperationException("The Chinese fallback must stay Dynamic; a static CJK atlas is enormous.");

        TMP_FontAsset primary = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryAssetPath);
        if (primary == null || primary.fallbackFontAssetTable == null ||
            !primary.fallbackFontAssetTable.Contains(fallback))
            throw new InvalidOperationException("LiberationSans SDF is missing the Chinese fallback.");

        Debug.Log("LOCALIZATION_FONT_VALIDATE_OK: dynamic Chinese fallback is registered.");
    }
}
#endif
