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
    private const string Stage2PrefabPath = "Assets/Prefab/Stage2Background.prefab";
    private const string Stage2ScenePath = "Assets/Scenes/stage2_full.unity";
    private const string SceneObjectName = "Parallax Background";
    private const int BackgroundLayer = 8;
    private const float Stage2PixelsPerUnit = 32f;
    private const float Stage2LayerScale = 8f;
    private static readonly string[] Stage2TexturePaths =
    {
        "Assets/Textures/background 2/1.png",
        "Assets/Textures/background 2/2.png",
        "Assets/Textures/background 2/3.png"
    };
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

    [MenuItem("Tools/Background/Build Stage2 Parallax Background")]
    public static void BuildStage2ParallaxBackground()
    {
        ConfigureStage2TextureImports();
        BuildStage2Prefab();

        Scene scene = EditorSceneManager.OpenScene(Stage2ScenePath, OpenSceneMode.Single);
        InstallIntoScene(scene, Stage2PrefabPath);
        if (!EditorSceneManager.SaveScene(scene, Stage2ScenePath))
            throw new InvalidOperationException("Failed to save " + Stage2ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateStage2Background();
        Debug.Log("STAGE2_PARALLAX_BACKGROUND_OK: three authored layers follow the active gameplay camera.");
    }

    [MenuItem("Tools/Background/Validate Stage2 Parallax Background")]
    public static void ValidateStage2Background()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Stage2PrefabPath);
        ParallaxBackground parallax = prefab != null ? prefab.GetComponent<ParallaxBackground>() : null;
        SpriteRenderer[] renderers = prefab != null
            ? prefab.GetComponentsInChildren<SpriteRenderer>(true)
            : Array.Empty<SpriteRenderer>();
        if (parallax == null || parallax.LayerCount != 3 || renderers.Length != 9)
            throw new InvalidOperationException("Stage2 background prefab must contain three parallax triplets.");

        string[] usedSprites = renderers.Select(renderer => AssetDatabase.GetAssetPath(renderer.sprite))
            .Distinct().OrderBy(path => path).ToArray();
        if (!usedSprites.SequenceEqual(Stage2TexturePaths.OrderBy(path => path)))
            throw new InvalidOperationException("Stage2 background must use background 2 layers 1, 2 and 3.");
        if (renderers.Any(renderer => renderer.gameObject.layer != BackgroundLayer ||
                                     renderer.sortingLayerName != "Default"))
            throw new InvalidOperationException("Stage2 background renderers use an invalid layer or sorting layer.");

        Scene scene = EditorSceneManager.OpenScene(Stage2ScenePath, OpenSceneMode.Single);
        ParallaxBackground[] instances = FindInScene<ParallaxBackground>(scene);
        if (instances.Length != 1 || instances[0].name != SceneObjectName ||
            PrefabUtility.GetCorrespondingObjectFromSource(instances[0].gameObject) != prefab)
            throw new InvalidOperationException("stage2_full must contain one Stage2Background prefab instance.");
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

    private static void ConfigureStage2TextureImports()
    {
        foreach (string texturePath in Stage2TexturePaths)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing stage2 background texture at " + texturePath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Stage2PixelsPerUnit;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }

    private static void BuildStage2Prefab()
    {
        GameObject root = new GameObject("Stage2 Background", typeof(ParallaxBackground));
        try
        {
            root.layer = BackgroundLayer;
            Transform sky = CreateStage2Layer(root.transform, "Stage2 Sky", Stage2TexturePaths[0], -120);
            Transform middle = CreateStage2Layer(root.transform, "Stage2 Middle Clouds", Stage2TexturePaths[1], -110);
            Transform foreground = CreateStage2Layer(root.transform, "Stage2 Foreground Clouds", Stage2TexturePaths[2], -100);

            SerializedObject data = new SerializedObject(root.GetComponent<ParallaxBackground>());
            SerializedProperty layers = data.FindProperty("backgroundLayers");
            layers.arraySize = 3;
            ConfigureParallaxEntry(layers.GetArrayElementAtIndex(0), sky, 1f, 1f);
            ConfigureParallaxEntry(layers.GetArrayElementAtIndex(1), middle, 0.92f, 0.98f);
            ConfigureParallaxEntry(layers.GetArrayElementAtIndex(2), foreground, 0.78f, 0.95f);
            data.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, Stage2PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Transform CreateStage2Layer(Transform parent, string name, string spritePath, int order)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null)
            throw new InvalidOperationException(spritePath + " was not imported as a Sprite.");

        GameObject centre = new GameObject(name, typeof(SpriteRenderer));
        centre.layer = BackgroundLayer;
        centre.transform.SetParent(parent, false);
        centre.transform.localScale = Vector3.one * Stage2LayerScale;
        ConfigureStage2Renderer(centre.GetComponent<SpriteRenderer>(), sprite, order);

        float imageWidth = sprite.bounds.size.x;
        CreateStage2Side(centre.transform, name + " Left", sprite, -imageWidth, order);
        CreateStage2Side(centre.transform, name + " Right", sprite, imageWidth, order);
        return centre.transform;
    }

    private static void CreateStage2Side(Transform parent, string name, Sprite sprite, float localX, int order)
    {
        GameObject side = new GameObject(name, typeof(SpriteRenderer));
        side.layer = BackgroundLayer;
        side.transform.SetParent(parent, false);
        side.transform.localPosition = new Vector3(localX, 0f, 0f);
        ConfigureStage2Renderer(side.GetComponent<SpriteRenderer>(), sprite, order);
    }

    private static void ConfigureStage2Renderer(SpriteRenderer renderer, Sprite sprite, int order)
    {
        renderer.sprite = sprite;
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = order;
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
        InstallIntoScene(scene, PrefabPath);
    }

    private static void InstallIntoScene(Scene scene, string prefabPath)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == SceneObjectName || root.name == "Background")
                UnityEngine.Object.DestroyImmediate(root);

        Camera camera = FindInScene<Camera>(scene).FirstOrDefault(candidate =>
                            candidate.CompareTag("MainCamera") && candidate.gameObject.activeInHierarchy)
                        ?? FindInScene<Camera>(scene).FirstOrDefault(candidate => candidate.CompareTag("MainCamera"));
        if (camera == null)
            throw new InvalidOperationException(scene.path + " has no MainCamera for the background.");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            throw new InvalidOperationException("Missing background prefab at " + prefabPath);
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
