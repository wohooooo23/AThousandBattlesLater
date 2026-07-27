#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    private const string HelpScenePath = "Assets/Scenes/Help.unity";
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

        RepairHelpSceneFont(source);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("LOCALIZATION_FONT_OK: NotoSansSC SDF (Dynamic) registered as a fallback on LiberationSans SDF.");
    }

    [MenuItem("Tools/Localization/Validate Chinese Font Fallback")]
    public static void Validate()
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        TrueTypeFontImporter importer = AssetImporter.GetAtPath(SourceFontPath) as TrueTypeFontImporter;
        if (source == null || importer == null || !importer.includeFontData)
            throw new InvalidOperationException("The bundled Noto Sans SC TTF must include font data for WebGL.");
        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackAssetPath);
        if (fallback == null)
            throw new InvalidOperationException("Missing " + FallbackAssetPath + " — run Build Chinese Font Fallback.");
        if (fallback.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            throw new InvalidOperationException("The Chinese fallback must stay Dynamic; a static CJK atlas is enormous.");

        TMP_FontAsset primary = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryAssetPath);
        if (primary == null || primary.fallbackFontAssetTable == null ||
            !primary.fallbackFontAssetTable.Contains(fallback))
            throw new InvalidOperationException("LiberationSans SDF is missing the Chinese fallback.");

        Scene help = EditorSceneManager.OpenScene(HelpScenePath, OpenSceneMode.Single);
        Text[] labels = FindInScene<Text>(help);
        foreach (Text label in labels)
            if (label.font != source)
                throw new InvalidOperationException("Help label still uses an OS-dependent font: " + label.name);
        Text body = Array.Find(labels, label => label.name == "Controls Body");
        if (body == null || !LocalizationTable.TryGetChinese(body.text, out string translated) ||
            translated == body.text)
            throw new InvalidOperationException("The Help body must have an exact Chinese translation key.");

        Debug.Log("LOCALIZATION_FONT_VALIDATE_OK: WebGL TTF/TMP fonts and Help translation are bundled.");
    }

    [MenuItem("Tools/Localization/Build WebGL Localization Smoke Player")]
    public static void BuildWebGlSmokePlayer()
    {
        Validate();
        string[] scenes = EditorBuildSettings.scenes.Where(entry => entry.enabled)
            .Select(entry => entry.path).ToArray();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        // Unity 6000.5 rejects Player output under its internal Library work directory. Builds is
        // ignored by source control and is the supported location for this disposable smoke player.
        string outputPath = Path.Combine(projectRoot, "Builds", "CodexWebGLLocalization");
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("WebGL localization smoke build failed: " + report.summary.result);
        Debug.Log("WEBGL_LOCALIZATION_BUILD_OK: " + report.summary.totalSize + " bytes.");
    }

    private static void RepairHelpSceneFont(Font source)
    {
        Scene help = EditorSceneManager.OpenScene(HelpScenePath, OpenSceneMode.Single);
        foreach (Text label in FindInScene<Text>(help))
        {
            if (label.font == source)
                continue;
            label.font = source;
            EditorUtility.SetDirty(label);
        }
        EditorSceneManager.MarkSceneDirty(help);
        if (!EditorSceneManager.SaveScene(help, HelpScenePath))
            throw new InvalidOperationException("Failed to save the repaired Help scene.");
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        List<T> found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<T>(true));
        return found.ToArray();
    }
}
#endif
