#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the dedicated Boss camera as a saved scene component. This is an editor-only build step;
/// gameplay never creates cameras or other required components at runtime.
/// </summary>
public static class BossArenaCameraBuilder
{
    private const string ScenePath = "Assets/Scenes/stage1_full.unity";
    private const string ExplorationCameraName = "Main Camera";
    private const string BossCameraName = "Boss Arena Camera";

    [MenuItem("Tools/A Thousand Battles Later/Build Boss Arena Camera")]
    public static void BuildBossArenaCamera()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BossArenaController arena = FindSceneComponent<BossArenaController>(scene);
        MapCameraFollow2D explorationCamera = FindSceneComponents<MapCameraFollow2D>(scene)
            .FirstOrDefault(component => component.name == ExplorationCameraName);
        HeroHealth hero = FindSceneComponent<HeroHealth>(scene);
        GameObject minimapHud = FindSceneObject(scene, "Minimap HUD");
        UIManager uiManager = FindSceneComponent<UIManager>(scene);
        BgmPlayer bgmPlayer = FindSceneComponent<BgmPlayer>(scene);

        if (arena == null || explorationCamera == null || hero == null || minimapHud == null || uiManager == null || bgmPlayer == null)
            throw new InvalidOperationException("stage1_full is missing its Boss arena, exploration camera, Hero, minimap HUD, UI manager, or BGM player.");

        GameObject previousBossCamera = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(candidate => candidate.name == BossCameraName);
        if (previousBossCamera != null)
            UnityEngine.Object.DestroyImmediate(previousBossCamera);

        GameObject bossCameraObject = UnityEngine.Object.Instantiate(explorationCamera.gameObject);
        bossCameraObject.name = BossCameraName;
        bossCameraObject.transform.SetParent(explorationCamera.transform.parent, true);
        bossCameraObject.transform.SetSiblingIndex(explorationCamera.transform.GetSiblingIndex() + 1);
        bossCameraObject.SetActive(false);

        MapCameraFollow2D copiedFollow = bossCameraObject.GetComponent<MapCameraFollow2D>();
        if (copiedFollow != null)
            UnityEngine.Object.DestroyImmediate(copiedFollow);
        BossArenaCamera2D bossCamera = bossCameraObject.AddComponent<BossArenaCamera2D>();

        Vector2 arenaMin = arena.ArenaMin;
        Vector2 arenaMax = arena.ArenaMax;
        SerializedObject arenaData = new SerializedObject(arena);
        Transform heroSpawnPoint = arenaData.FindProperty("heroSpawnPoint").objectReferenceValue as Transform;
        if (heroSpawnPoint == null)
            throw new InvalidOperationException("The Boss arena is missing its Hero spawn point.");
        float viewBottom = heroSpawnPoint.position.y - 5f;
        float viewTop = arenaMax.y;
        float verticalCentre = (viewBottom + viewTop) * 0.5f;
        float orthographicSize = (viewTop - viewBottom) * 0.5f;

        SerializedObject cameraData = new SerializedObject(bossCamera);
        cameraData.FindProperty("target").objectReferenceValue = hero.transform;
        cameraData.FindProperty("arenaMin").vector2Value = arenaMin;
        cameraData.FindProperty("arenaMax").vector2Value = arenaMax;
        cameraData.FindProperty("verticalCentre").floatValue = verticalCentre;
        cameraData.FindProperty("orthographicSize").floatValue = orthographicSize;
        cameraData.FindProperty("smoothTime").floatValue = 0.16f;
        cameraData.ApplyModifiedPropertiesWithoutUndo();

        Vector3 cameraPosition = explorationCamera.transform.position;
        cameraPosition.x = Mathf.Clamp(hero.transform.position.x, arenaMin.x, arenaMax.x);
        cameraPosition.y = verticalCentre;
        bossCameraObject.transform.position = cameraPosition;

        arenaData.FindProperty("explorationCamera").objectReferenceValue = explorationCamera;
        arenaData.FindProperty("bossCamera").objectReferenceValue = bossCamera;
        arenaData.FindProperty("minimapHud").objectReferenceValue = minimapHud;
        arenaData.FindProperty("uiManager").objectReferenceValue = uiManager;
        arenaData.FindProperty("bgmPlayer").objectReferenceValue = bgmPlayer;
        arenaData.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(arena);
        EditorUtility.SetDirty(bossCamera);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Unity could not save the Boss camera into stage1_full.");

        Debug.Log("BossArenaCameraBuilder: saved a dedicated scene-authored Boss camera in stage1_full.");
    }

    public static void BuildFromCommandLine()
    {
        BuildBossArenaCamera();
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        return FindSceneComponents<T>(scene).FirstOrDefault();
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(candidate => candidate.name == objectName);
    }
}
#endif
