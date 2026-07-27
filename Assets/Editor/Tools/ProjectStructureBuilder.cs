#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the repository's conservative Unity asset layout. Moves are performed through the
/// AssetDatabase so every .meta GUID survives and scene/prefab references remain intact.
/// </summary>
public static class ProjectStructureBuilder
{
    private const string LegacyScenes = "Assets/Scenes/Legacy";
    private const string Development = "Assets/Development";
    private const string BackgroundRoot = "Assets/Textures/Background";
    private const string EditorTools = "Assets/Editor/Tools";

    private static readonly Dictionary<string, string[]> ToolGroups = new Dictionary<string, string[]>
    {
        ["Flow"] = new[]
        {
            "CampaignFlowBuilder.cs", "DemoFlowBalanceBuilder.cs", "DemoSceneBuilder.cs"
        },
        ["UI"] = new[]
        {
            "AlphaUiBuilder.cs", "BossHealthBarBuilder.cs", "ForgeInterfaceBuilder.cs",
            "KunaiHudBuilder.cs", "LocalizationFontBuilder.cs", "MenuButtonSkinBuilder.cs",
            "MobHealthBarPrefabBuilder.cs", "PauseMenuBuilder.cs", "StartMenuSettingsBuilder.cs",
            "StartMenuTitleBuilder.cs"
        },
        ["Combat"] = new[]
        {
            "BossWizardBuilder.cs", "EnemyContentBuilder.cs", "KingBossBuilder.cs",
            "OrcModelBuilder.cs", "Stage2MobCombatBuilder.cs"
        },
        ["Inventory"] = new[]
        {
            "AbilityEquipmentBuilder.cs", "DemoItemBuilder.cs", "EquipmentBuilder.cs",
            "KunaiInventoryBuilder.cs", "VerdantRuneBuilder.cs"
        },
        ["World"] = new[]
        {
            "BackgroundBuilder.cs", "BossArenaCameraBuilder.cs", "ChestHintTriggerBuilder.cs",
            "FullMapRenderRepair.cs", "RuneBossGateBuilder.cs", "StageBgmBuilder.cs",
            "TreasureChestBuilder.cs"
        },
        ["Narrative"] = new[] { "NarrativeAudioBuilder.cs", "StoryChapterBuilder.cs" }
    };

    [MenuItem("Tools/Project/Organize Asset Structure")]
    public static void Build()
    {
        EnsureFolder(LegacyScenes);
        EnsureFolder(Development);
        EnsureFolder(BackgroundRoot);
        foreach (string group in ToolGroups.Keys)
            EnsureFolder(EditorTools + "/" + group);

        // CreateFolder updates the database asynchronously in some Unity 6 batch-mode runs.
        // Register every destination parent before MoveAsset validates it.
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        MoveAsset("Assets/GeneratedAttackDemo", Development + "/GeneratedAttackDemo");
        MoveAsset("Assets/GeneratedUI", Development + "/GeneratedUI");
        MoveFolderContents("Assets/Textures/Background 1", BackgroundRoot + "/Stage1");
        MoveFolderContents("Assets/Textures/background 2", BackgroundRoot + "/Stage2");

        MoveAsset("Assets/Scenes/New Scene.unity", LegacyScenes + "/New Scene.unity");
        MoveAsset("Assets/Scenes/stage1.unity", LegacyScenes + "/stage1.unity");
        MoveAsset("Assets/Scenes/stage1 boss.unity", LegacyScenes + "/stage1 boss.unity");

        foreach (KeyValuePair<string, string[]> group in ToolGroups)
            foreach (string file in group.Value)
                MoveAsset(EditorTools + "/" + file, EditorTools + "/" + group.Key + "/" + file);

        DeleteRecoveryScenes();
        DeleteEmptyFolder("Assets/Sprites");
        DeleteEmptyFolder("Assets/Animations/Enemy");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("PROJECT_STRUCTURE_OK: legacy, generated, background and editor assets organized with GUIDs preserved.");
    }

    [MenuItem("Tools/Project/Validate Asset Structure")]
    public static void Validate()
    {
        RequireFolder(LegacyScenes);
        RequireFolder(Development + "/GeneratedAttackDemo");
        RequireFolder(Development + "/GeneratedUI");
        RequireFolder(BackgroundRoot + "/Stage1");
        RequireFolder(BackgroundRoot + "/Stage2");

        RequireAsset(LegacyScenes + "/stage1.unity");
        RequireAsset(LegacyScenes + "/stage1 boss.unity");
        RequireAsset(BackgroundRoot + "/Stage1/cover/samurai_no_watermark.png");
        RequireAsset(BackgroundRoot + "/Stage2/1.png");

        foreach (KeyValuePair<string, string[]> group in ToolGroups)
        {
            RequireFolder(EditorTools + "/" + group.Key);
            foreach (string file in group.Value)
                RequireAsset(EditorTools + "/" + group.Key + "/" + file);
        }

        if (AssetDatabase.IsValidFolder("Assets/GeneratedAttackDemo") ||
            AssetDatabase.IsValidFolder("Assets/GeneratedUI") ||
            AssetDatabase.IsValidFolder("Assets/Textures/Background 1") ||
            AssetDatabase.IsValidFolder("Assets/Textures/background 2"))
            throw new InvalidOperationException("A legacy top-level asset folder still exists.");

        string[] recovery = Directory.GetFiles(Application.dataPath, "InitTestScene*.unity", SearchOption.TopDirectoryOnly);
        if (recovery.Length != 0)
            throw new InvalidOperationException("Unity recovery scenes still exist at the Assets root.");

        Debug.Log("PROJECT_STRUCTURE_VALIDATE_OK.");
    }

    private static void MoveAsset(string source, string destination)
    {
        if (AssetDatabase.LoadMainAssetAtPath(destination) != null || AssetDatabase.IsValidFolder(destination))
            return;
        if (AssetDatabase.LoadMainAssetAtPath(source) == null && !AssetDatabase.IsValidFolder(source))
            return;

        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException($"Failed to move {source} -> {destination}: {error}");
    }

    private static void MoveFolderContents(string source, string destination)
    {
        if (!AssetDatabase.IsValidFolder(source))
            return;

        EnsureFolder(destination);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        string absoluteSource = Path.GetFullPath(source);
        foreach (string entry in Directory.GetFileSystemEntries(absoluteSource, "*", SearchOption.TopDirectoryOnly))
        {
            if (entry.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                continue;

            string name = Path.GetFileName(entry);
            MoveAsset(source + "/" + name, destination + "/" + name);
        }

        if (!AssetDatabase.DeleteAsset(source))
            throw new InvalidOperationException("Failed to remove the empty source folder " + source + ".");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent))
            throw new InvalidOperationException("Invalid Unity folder path: " + path);
        EnsureFolder(parent);
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, name)))
            throw new InvalidOperationException("Failed to create Unity folder: " + path);
    }

    private static void DeleteRecoveryScenes()
    {
        foreach (string absolute in Directory.GetFiles(Application.dataPath, "InitTestScene*.unity", SearchOption.TopDirectoryOnly))
        {
            string assetPath = "Assets/" + Path.GetFileName(absolute);
            if (!AssetDatabase.DeleteAsset(assetPath))
                throw new InvalidOperationException("Failed to remove recovery scene " + assetPath + ".");
        }
    }

    private static void DeleteEmptyFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            return;
        string absolute = Path.GetFullPath(path);
        if (Directory.GetFileSystemEntries(absolute).Length != 0)
            return;
        if (!AssetDatabase.DeleteAsset(path))
            throw new InvalidOperationException("Failed to remove empty folder " + path + ".");
    }

    private static void RequireFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            throw new InvalidOperationException("Missing organized folder " + path + ".");
    }

    private static void RequireAsset(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            throw new InvalidOperationException("Missing organized asset " + path + ".");
    }
}
#endif
