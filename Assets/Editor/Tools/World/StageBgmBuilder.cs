#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Saves the independent exploration/Boss BGM slots and arena reference into stage1_full.</summary>
public static class StageBgmBuilder
{
    private const string ScenePath = "Assets/Scenes/stage1_full.unity";
    private const string BossBgmPath = "Assets/Audio/SFX/monume-tension-tension-music-547908.mp3";

    [MenuItem("Tools/Narrative & Audio/Configure Stage and Boss BGM")]
    public static void Configure()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BgmPlayer player = FindInScene<BgmPlayer>(scene).SingleOrDefault();
        BossArenaController arena = FindInScene<BossArenaController>(scene).SingleOrDefault();
        AudioClip bossClip = AssetDatabase.LoadAssetAtPath<AudioClip>(BossBgmPath);
        if (player == null || arena == null || bossClip == null)
            throw new InvalidOperationException("stage1_full is missing its BGM player, Boss arena, or Boss music asset.");

        SerializedObject playerData = new SerializedObject(player);
        playerData.FindProperty("explorationClip").objectReferenceValue = null;
        playerData.FindProperty("explorationResourcesPath").stringValue = string.Empty;
        playerData.FindProperty("bossClip").objectReferenceValue = bossClip;
        playerData.FindProperty("bossResourcesPath").stringValue = string.Empty;
        playerData.FindProperty("startingTrack").enumValueIndex = (int)BgmTrack.Exploration;
        playerData.FindProperty("playOnStart").boolValue = true;
        playerData.FindProperty("persistAcrossScenes").boolValue = false;
        playerData.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject arenaData = new SerializedObject(arena);
        arenaData.FindProperty("bgmPlayer").objectReferenceValue = player;
        arenaData.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(arena);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity could not save the stage BGM configuration.");
        Debug.Log("STAGE_BGM_BUILD_OK: exploration and Boss music slots saved; arena entry switches to Boss BGM.");
    }

    public static void ConfigureFromCommandLine() => Configure();

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
#endif
