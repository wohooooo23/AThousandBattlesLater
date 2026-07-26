using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saves the playable campaign route directly into StartMenu, stage1_full and stage2_full. Stage 1
/// uses its existing black story overlay as a transition; stage 2 remains the only final Victory.
/// </summary>
public static class CampaignFlowBuilder
{
    private const string StartPath = "Assets/Scenes/StartMenu.unity";
    private const string Stage1Path = "Assets/Scenes/stage1_full.unity";
    private const string Stage2Path = "Assets/Scenes/stage2_full.unity";
    private const string HelpPath = "Assets/Scenes/Help.unity";
    private const string Stage1Name = "stage1_full";
    private const string Stage2Name = "stage2_full";
    private const string StartName = "StartMenu";
    private const float FadeDuration = 1.15f;

    [MenuItem("Tools/A Thousand Battles Later/Build Campaign Flow")]
    public static void Build()
    {
        ConfigureStartMenu();
        ConfigureStage(Stage1Path, Stage2Name);
        ConfigureStage(Stage2Path, string.Empty);
        SetBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("CAMPAIGN_FLOW_BUILD_OK: Start -> stage1 -> black fade -> stage2 -> Victory.");
    }

    [MenuItem("Tools/A Thousand Battles Later/Validate Campaign Flow")]
    public static void Validate()
    {
        Scene start = EditorSceneManager.OpenScene(StartPath, OpenSceneMode.Single);
        StartMenuController menu = FindInScene<StartMenuController>(start).SingleOrDefault();
        Require(menu != null && menu.TargetSceneName == Stage1Name, "START must load stage1_full.");

        ValidateStage(Stage1Path, Stage2Name, false);
        ValidateStage(Stage2Path, string.Empty, true);

        string[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path).ToArray();
        string[] expected = { StartPath, Stage1Path, Stage2Path, HelpPath };
        Require(enabled.SequenceEqual(expected),
            "Build Settings must be StartMenu -> stage1_full -> stage2_full -> Help.");
        Debug.Log("CAMPAIGN_FLOW_VALIDATE_OK: intermediate fade transition and final Victory route are saved.");
    }

    private static void ConfigureStartMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(StartPath, OpenSceneMode.Single);
        StartMenuController menu = FindInScene<StartMenuController>(scene).SingleOrDefault();
        if (menu == null)
            throw new MissingReferenceException(StartPath + " is missing StartMenuController.");
        SetString(menu, "targetSceneName", Stage1Name);
        Save(scene, StartPath);
    }

    private static void ConfigureStage(string path, string nextStage)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        EnemyHealth boss = FindInScene<EnemyHealth>(scene).SingleOrDefault();
        StoryDialogueController story = FindInScene<StoryDialogueController>(scene).SingleOrDefault();
        if (boss == null || story == null || story.FadeOverlay == null)
            throw new MissingReferenceException(path + " requires one Boss, Story controller and saved black fade.");

        // This object used to be left inactive in the copied full-map scenes. Saving it active makes
        // the opening fade, Boss dialogue and cross-stage fade all use the authored controller.
        story.gameObject.SetActive(true);
        EditorUtility.SetDirty(story.gameObject);

        SetString(boss, "nextStageSceneName", nextStage);
        SetObject(boss, "transitionFade", story.FadeOverlay);
        SetFloat(boss, "transitionFadeDuration", FadeDuration);
        SetString(boss, "victoryReturnSceneName", StartName);
        Save(scene, path);
    }

    private static void ValidateStage(string path, string nextStage, bool finalStage)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        EnemyHealth boss = FindInScene<EnemyHealth>(scene).SingleOrDefault();
        StoryDialogueController story = FindInScene<StoryDialogueController>(scene).SingleOrDefault();
        Require(boss != null && story != null && story.gameObject.activeSelf,
            path + " must save its Boss and active Story System in the scene.");
        Require(boss.NextStageSceneName == nextStage && boss.TransitionFade == story.FadeOverlay &&
                Mathf.Abs(boss.TransitionFadeDuration - FadeDuration) < 0.001f,
            path + " has the wrong campaign transition configuration.");
        Require(finalStage == string.IsNullOrWhiteSpace(boss.NextStageSceneName),
            path + " final/intermediate stage role is incorrect.");
        Require(boss.VictoryReturnSceneName == StartName,
            path + " final Victory must return to StartMenu.");
    }

    private static void SetBuildSettings()
    {
        string[] paths = { StartPath, Stage1Path, Stage2Path, HelpPath };
        foreach (string path in paths)
            if (!File.Exists(path))
                throw new MissingReferenceException("Missing campaign scene: " + path);
        EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

    private static void SetString(UnityEngine.Object target, string property, string value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(property).stringValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetFloat(UnityEngine.Object target, string property, float value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(property).floatValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetObject(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(property).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void Save(Scene scene, string path)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, path))
            throw new InvalidOperationException("Failed to save " + path);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
