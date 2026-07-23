#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Repairs the imported parallax prefab and saves one authored instance in each game scene.</summary>
public static class BackgroundBuilder
{
    private const string PrefabPath = "Assets/Prefab/Background.prefab";
    private const string SceneObjectName = "Parallax Background";
    private const int BackgroundLayer = 8;
    private static readonly string[] GameplayScenePaths =
    {
        "Assets/Scenes/stage1.unity",
        "Assets/Scenes/stage1_full.unity",
        "Assets/Scenes/stage1 boss.unity"
    };

    [MenuItem("Tools/Background/Repair Parallax Background")]
    public static void RepairAll()
    {
        RepairPrefab();
        foreach (string scenePath in GameplayScenePaths)
        {
            if (!File.Exists(scenePath))
                continue;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            InstallIntoScene(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new InvalidOperationException("Failed to save " + scenePath);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateAll();
        Debug.Log("PARALLAX_BACKGROUND_REPAIR_OK: repaired prefab and saved background instances in all game scenes.");
    }

    public static void InstallIntoActiveScene()
    {
        RepairPrefab();
        Scene scene = SceneManager.GetActiveScene();
        InstallIntoScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Background/Validate Parallax Background")]
    public static void ValidateAll()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null || prefab.GetComponent<ParallaxBackground>() == null)
            throw new InvalidOperationException("Background prefab is missing its ParallaxBackground component.");
        if (prefab.GetComponentsInChildren<SpriteRenderer>(true).Length != 6)
            throw new InvalidOperationException("Background prefab must contain three sky and three town sprites.");
        foreach (SpriteRenderer renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sharedMaterial == null || renderer.sortingLayerName != "Default" ||
                renderer.gameObject.layer != BackgroundLayer)
                throw new InvalidOperationException("Background renderers must use the default sprite material/layer setup.");
        }

        foreach (string scenePath in GameplayScenePaths)
        {
            if (!File.Exists(scenePath))
                continue;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ParallaxBackground[] instances = FindInScene<ParallaxBackground>(scene);
            if (instances.Length != 1 || instances[0].name != SceneObjectName)
                throw new InvalidOperationException(scenePath + " must contain one scene-authored parallax background.");
        }
        Debug.Log("PARALLAX_BACKGROUND_VALIDATE_OK: prefab and scene instances are valid.");
    }

    private static void RepairPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Missing imported background prefab at " + PrefabPath);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            root.name = "Background";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Transform sky = FindDeepChild(root.transform, "Background_sky");
            Transform town = FindDeepChild(root.transform, "Background_town");
            if (sky == null || town == null)
                throw new InvalidOperationException("Background prefab requires Background_sky and Background_town.");

            ConfigureLayer(sky, Vector3.zero, Vector3.one * 12f, -100);
            ConfigureLayer(town, new Vector3(0f, -13f, 0f), Vector3.one * 5f, -90);
            EnsureTriplet(sky, -100);
            EnsureTriplet(town, -90);
            SetLayerRecursively(root.transform, BackgroundLayer);

            SerializedObject data = new SerializedObject(root.GetComponent<ParallaxBackground>());
            SerializedProperty layers = data.FindProperty("backgroundLayers");
            layers.arraySize = 2;
            ConfigureParallaxEntry(layers.GetArrayElementAtIndex(0), sky, 1f, 1f);
            ConfigureParallaxEntry(layers.GetArrayElementAtIndex(1), town, 0.72f, 0.5f);
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureParallaxEntry(SerializedProperty entry, Transform target,
        float horizontal, float vertical)
    {
        entry.FindPropertyRelative("background").objectReferenceValue = target;
        entry.FindPropertyRelative("parallaxMultiplier").floatValue = horizontal;
        entry.FindPropertyRelative("verticalMultiplier").floatValue = vertical;
        entry.FindPropertyRelative("imageWidthOffset").floatValue = 1f;
    }

    private static void ConfigureLayer(Transform layer, Vector3 position, Vector3 scale, int order)
    {
        layer.localPosition = position;
        layer.localRotation = Quaternion.identity;
        layer.localScale = scale;
        ConfigureRenderer(layer.GetComponent<SpriteRenderer>(), order);
    }

    private static void EnsureTriplet(Transform centre, int order)
    {
        SpriteRenderer centreRenderer = centre.GetComponent<SpriteRenderer>();
        if (centreRenderer == null || centreRenderer.sprite == null)
            throw new InvalidOperationException(centre.name + " needs a SpriteRenderer with a sprite.");

        string leftName = centre.name + "_left";
        string rightName = centre.name + "_right";
        Transform left = centre.Find(leftName);
        Transform right = centre.Find(rightName);
        if (left == null)
            left = CreateSideSprite(centre, leftName, centreRenderer).transform;
        if (right == null)
            right = CreateSideSprite(centre, rightName, centreRenderer).transform;
        ConfigureSide(left, -10f, centreRenderer, order);
        ConfigureSide(right, 10f, centreRenderer, order);
    }

    private static GameObject CreateSideSprite(Transform parent, string name, SpriteRenderer source)
    {
        GameObject side = new GameObject(name, typeof(SpriteRenderer));
        side.transform.SetParent(parent, false);
        side.GetComponent<SpriteRenderer>().sprite = source.sprite;
        return side;
    }

    private static void ConfigureSide(Transform side, float x, SpriteRenderer source, int order)
    {
        side.localPosition = new Vector3(x, 0f, 0f);
        side.localRotation = Quaternion.identity;
        side.localScale = Vector3.one;
        SpriteRenderer renderer = side.GetComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.color = source.color;
        ConfigureRenderer(renderer, order);
    }

    private static void ConfigureRenderer(SpriteRenderer renderer, int order)
    {
        if (renderer == null)
            return;
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = order;
    }

    private static void InstallIntoScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == SceneObjectName || root.name == "Background")
                UnityEngine.Object.DestroyImmediate(root);

        Camera camera = FindInScene<Camera>(scene).FirstOrDefault(candidate => candidate.CompareTag("MainCamera"));
        if (camera == null)
            throw new InvalidOperationException(scene.path + " has no MainCamera for the background.");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = SceneObjectName;
        instance.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursively(child, layer);
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeepChild(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
}
#endif
