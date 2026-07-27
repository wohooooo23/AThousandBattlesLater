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
/// Imports the authored English and Chinese faces, creates dynamic TMP assets and deploys the
/// English face into saved UI. LocalizedText switches every label to the Chinese face at runtime.
/// Both source TTF files live in Resources so WebGL can populate dynamic glyph atlases.
/// </summary>
public static class LocalizationFontBuilder
{
    private const string EnglishFontPath = "Assets/Resources/Fonts/BoldPixels.ttf";
    private const string ChineseFontPath = "Assets/Resources/Fonts/ZCOOLXiaoWei-Regular.ttf";
    private const string EnglishTmpPath = "Assets/Resources/Fonts/BoldPixels SDF.asset";
    private const string ChineseTmpPath = "Assets/Resources/Fonts/ZCOOLXiaoWei SDF.asset";
    private const string BoldPixelsLicensePath = "Assets/Resources/Fonts/BoldPixels-LICENSE.txt";
    private const string ZcoolLicensePath = "Assets/Resources/Fonts/ZCOOLXiaoWei-OFL.txt";
    private const string PrimaryAssetPath =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private static readonly string[] UiScenePaths =
    {
        "Assets/Scenes/StartMenu.unity",
        "Assets/Scenes/Help.unity"
    };

    [MenuItem("Tools/Localization/Build Dual Language Fonts")]
    public static void Build()
    {
        ImportExternalSources();
        ConfigureFontImporter(EnglishFontPath);
        ConfigureFontImporter(ChineseFontPath);

        Font english = RequireFont(EnglishFontPath);
        Font chinese = RequireFont(ChineseFontPath);
        TMP_FontAsset englishTmp = LoadOrCreateTmpFont(english, EnglishTmpPath, "BoldPixels SDF");
        TMP_FontAsset chineseTmp = LoadOrCreateTmpFont(chinese, ChineseTmpPath, "ZCOOLXiaoWei SDF");
        ConfigureFallbacks(englishTmp, chineseTmp);

        foreach (string scenePath in UiScenePaths)
            DeployToScene(scenePath, english, englishTmp);
        DeployToUiPrefabs(english, englishTmp);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Validate();
        Debug.Log("DUAL_LANGUAGE_FONTS_OK: BoldPixels English and ZCOOL XiaoWei Chinese deployed for UI/TMP/WebGL.");
    }

    [MenuItem("Tools/Localization/Validate Dual Language Fonts")]
    public static void Validate()
    {
        Font english = ValidateSourceFont(EnglishFontPath);
        Font chinese = ValidateSourceFont(ChineseFontPath);
        TMP_FontAsset englishTmp = ValidateTmpFont(EnglishTmpPath, english);
        TMP_FontAsset chineseTmp = ValidateTmpFont(ChineseTmpPath, chinese);
        if (englishTmp.fallbackFontAssetTable == null ||
            !englishTmp.fallbackFontAssetTable.Contains(chineseTmp))
            throw new InvalidOperationException("BoldPixels TMP must fall back to ZCOOL XiaoWei for mixed labels.");

        TMP_FontAsset primary = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryAssetPath);
        if (primary == null || primary.fallbackFontAssetTable == null ||
            !primary.fallbackFontAssetTable.Contains(chineseTmp))
            throw new InvalidOperationException("The TMP default face is missing the ZCOOL XiaoWei fallback.");

        foreach (string scenePath in UiScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ValidateLabels(FindInScene<Text>(scene), FindInScene<TMP_Text>(scene), english, englishTmp, scenePath);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab", "Assets/Resources/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;
            ValidateLabels(prefab.GetComponentsInChildren<Text>(true),
                prefab.GetComponentsInChildren<TMP_Text>(true), english, englishTmp, path);
        }

        Debug.Log("DUAL_LANGUAGE_FONTS_VALIDATE_OK: sources, TMP atlases, saved UI and WebGL data are valid.");
    }

    [MenuItem("Tools/Localization/Build WebGL Localization Smoke Player")]
    public static void BuildWebGlSmokePlayer()
    {
        Validate();
        string[] scenes = EditorBuildSettings.scenes.Where(entry => entry.enabled)
            .Select(entry => entry.path).ToArray();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
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

    private static void ImportExternalSources()
    {
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        CopyIfNeeded(Path.Combine(downloads, "webfontkit-BoldPixels", "boldpixels.ttf"), EnglishFontPath);
        CopyIfNeeded(Path.Combine(downloads, "ZCOOL_XiaoWei", "ZCOOLXiaoWei-Regular.ttf"), ChineseFontPath);
        CopyIfNeeded(Path.Combine(downloads, "webfontkit-BoldPixels", "license.txt"), BoldPixelsLicensePath);
        CopyIfNeeded(Path.Combine(downloads, "ZCOOL_XiaoWei", "OFL.txt"), ZcoolLicensePath);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static void CopyIfNeeded(string source, string assetPath)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException("Missing downloaded font source.", source);
        string destination = Path.GetFullPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        if (File.Exists(destination) && FilesMatch(source, destination))
            return;
        File.Copy(source, destination, true);
    }

    private static bool FilesMatch(string first, string second)
    {
        FileInfo a = new FileInfo(first);
        FileInfo b = new FileInfo(second);
        if (a.Length != b.Length)
            return false;
        return File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(second));
    }

    private static void ConfigureFontImporter(string path)
    {
        TrueTypeFontImporter importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
        if (importer == null)
            throw new InvalidOperationException("Font was not imported as TrueType/OpenType: " + path);
        if (importer.includeFontData)
            return;
        importer.includeFontData = true;
        importer.SaveAndReimport();
    }

    private static Font RequireFont(string path)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
        if (font == null)
            throw new InvalidOperationException("Missing imported font at " + path + ".");
        return font;
    }

    private static TMP_FontAsset LoadOrCreateTmpFont(Font source, string path, string name)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null)
            return existing;

        TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(source, 90, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
            AtlasPopulationMode.Dynamic, true);
        if (created == null)
            throw new InvalidOperationException("TMP could not create a font asset from " + source.name + ".");
        created.name = name;
        created.isMultiAtlasTexturesEnabled = true;
        AssetDatabase.CreateAsset(created, path);
        if (created.atlasTextures != null && created.atlasTextures.Length > 0)
        {
            created.atlasTextures[0].name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(created.atlasTextures[0], created);
        }
        if (created.material != null)
        {
            created.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(created.material, created);
        }
        EditorUtility.SetDirty(created);
        return created;
    }

    private static void ConfigureFallbacks(TMP_FontAsset english, TMP_FontAsset chinese)
    {
        english.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        if (!english.fallbackFontAssetTable.Contains(chinese))
            english.fallbackFontAssetTable.Add(chinese);
        EditorUtility.SetDirty(english);

        TMP_FontAsset primary = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrimaryAssetPath);
        if (primary == null)
            throw new InvalidOperationException("TMP Essential Resources are required at " + PrimaryAssetPath + ".");
        primary.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        primary.fallbackFontAssetTable.RemoveAll(asset => asset != null && asset.name.Contains("NotoSansSC"));
        if (!primary.fallbackFontAssetTable.Contains(chinese))
            primary.fallbackFontAssetTable.Add(chinese);
        EditorUtility.SetDirty(primary);
    }

    private static void DeployToScene(string path, Font english, TMP_FontAsset englishTmp)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        bool changed = ApplyFonts(FindInScene<Text>(scene), FindInScene<TMP_Text>(scene), english, englishTmp);
        if (!changed)
            return;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, path))
            throw new InvalidOperationException("Failed to save font changes to " + path + ".");
    }

    private static void DeployToUiPrefabs(Font english, TMP_FontAsset englishTmp)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab", "Assets/Resources/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (ApplyFonts(root.GetComponentsInChildren<Text>(true),
                    root.GetComponentsInChildren<TMP_Text>(true), english, englishTmp))
                    PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static bool ApplyFonts(IEnumerable<Text> legacyLabels, IEnumerable<TMP_Text> tmpLabels,
        Font english, TMP_FontAsset englishTmp)
    {
        bool changed = false;
        foreach (Text label in legacyLabels)
        {
            if (label.font == english)
                continue;
            label.font = english;
            EditorUtility.SetDirty(label);
            changed = true;
        }
        foreach (TMP_Text label in tmpLabels)
        {
            if (label.font == englishTmp)
                continue;
            label.font = englishTmp;
            EditorUtility.SetDirty(label);
            changed = true;
        }
        return changed;
    }

    private static Font ValidateSourceFont(string path)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
        TrueTypeFontImporter importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
        if (font == null || importer == null || !importer.includeFontData)
            throw new InvalidOperationException(path + " must include its font data for WebGL.");
        return font;
    }

    private static TMP_FontAsset ValidateTmpFont(string path, Font source)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
            font.sourceFontFile != source)
            throw new InvalidOperationException(path + " is missing, static, or uses the wrong source font.");
        return font;
    }

    private static void ValidateLabels(IEnumerable<Text> legacyLabels, IEnumerable<TMP_Text> tmpLabels,
        Font english, TMP_FontAsset englishTmp, string owner)
    {
        Text invalidLegacy = legacyLabels.FirstOrDefault(label => label.font != english);
        TMP_Text invalidTmp = tmpLabels.FirstOrDefault(label => label.font != englishTmp);
        if (invalidLegacy != null || invalidTmp != null)
            throw new InvalidOperationException(owner + " still contains a label using an old font.");
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
}
#endif
