#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Persists one fully-referenced health bar in every mob prefab and repairs existing stages.</summary>
public static class MobHealthBarPrefabBuilder
{
    private const string SquareSpritePath = "Assets/Resources/AttackHitboxes/AttackSquare.png";
    private static readonly string[] PrefabPaths =
    {
        "Assets/Enemy/Mobs/Orc/Mob_Orc.prefab",
        "Assets/Enemy/Mobs/Goblin/Mob_Goblin.prefab",
        "Assets/Enemy/Mobs/Mushroom/Mob_Mushroom.prefab",
        "Assets/Enemy/Mobs/FlyingEye/Mob_FlyingEye.prefab",
        "Assets/Enemy/Mobs/Skeleton/Mob_Skeleton.prefab"
    };
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/stage1_full.unity",
        "Assets/Scenes/stage2_full.unity"
    };

    [MenuItem("Tools/Enemies/Repair Prefab Health Bars")]
    public static void Build()
    {
        BuildPrefabsOnly();
        foreach (string scenePath in ScenePaths)
            RepairScene(scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("MOB_HEALTH_BARS_OK: persistent sprites and references saved in all mob prefabs and stages.");
    }

    public static void BuildPrefabsOnly()
    {
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        if (square == null)
            throw new MissingReferenceException("Health bars require persistent sprite " + SquareSpritePath);
        foreach (string path in PrefabPaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Enemy_Health health = root.GetComponent<Enemy_Health>() ??
                    throw new MissingReferenceException(path + " has no Enemy_Health.");
                EnemyHealthBar bar = EnsureSingleBar(root.transform, root.name.Contains("FlyingEye"), square);
                SetObject(health, "worldHealthBar", bar);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    private static EnemyHealthBar EnsureSingleBar(Transform actor, bool flying, Sprite square)
    {
        EnemyHealthBar[] bars = actor.GetComponentsInChildren<EnemyHealthBar>(true);
        EnemyHealthBar bar = bars.FirstOrDefault();
        foreach (EnemyHealthBar duplicate in bars.Skip(1).ToArray())
            UnityEngine.Object.DestroyImmediate(duplicate.gameObject);

        if (bar == null)
        {
            GameObject barObject = new GameObject("Health Bar", typeof(EnemyHealthBar));
            barObject.transform.SetParent(actor, false);
            bar = barObject.GetComponent<EnemyHealthBar>();
        }
        bar.name = "Health Bar";
        bar.transform.localPosition = new Vector3(0f, flying ? 0.9f : 3.2f, 0f);
        bar.transform.localRotation = Quaternion.identity;
        bar.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

        Transform capacity = bar.transform.Find("Capacity") ?? CreateChild(bar.transform, "Capacity");
        SpriteRenderer capacityRenderer = capacity.GetComponent<SpriteRenderer>();
        if (capacityRenderer == null)
            capacityRenderer = capacity.gameObject.AddComponent<SpriteRenderer>();
        ConfigureRenderer(capacityRenderer, square, new Color(0.32f, 0.015f, 0.025f, 0.92f), 60);
        capacity.localPosition = Vector3.zero;
        capacity.localRotation = Quaternion.identity;
        capacity.localScale = new Vector3(4.5f, 0.55f, 1f);

        Transform anchor = bar.transform.Find("Fill Anchor") ?? CreateChild(bar.transform, "Fill Anchor");
        anchor.localPosition = new Vector3(-2.25f, 0f, 0f);
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
        Transform current = anchor.Find("Current") ?? CreateChild(anchor, "Current");
        SpriteRenderer currentRenderer = current.GetComponent<SpriteRenderer>();
        if (currentRenderer == null)
            currentRenderer = current.gameObject.AddComponent<SpriteRenderer>();
        ConfigureRenderer(currentRenderer, square, new Color(1f, 0.32f, 0.36f, 0.96f), 61);

        SetObject(bar, "followTarget", actor);
        SetObject(bar, "fillSprite", current);
        SetVector3(bar, "followOffset", new Vector3(0f, 4.5f, 0f));
        SetFloat(bar, "width", 4.5f);
        SetFloat(bar, "height", 0.55f);
        bar.SetFraction(1f);
        EditorUtility.SetDirty(bar);
        return bar;
    }

    private static void RepairScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        foreach (Enemy_Health health in FindInScene<Enemy_Health>(scene))
        {
            EnemyHealthBar[] bars = health.GetComponentsInChildren<EnemyHealthBar>(true);
            EnemyHealthBar prefabBar = bars.FirstOrDefault(candidate =>
                PrefabUtility.GetCorrespondingObjectFromSource(candidate) != null);
            EnemyHealthBar chosen = prefabBar ?? bars.FirstOrDefault();
            if (chosen == null)
                continue; // A non-prefab legacy enemy is left untouched rather than runtime-building UI.

            foreach (EnemyHealthBar duplicate in bars.Where(candidate => candidate != chosen).ToArray())
                UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
            foreach (SpriteRenderer renderer in chosen.GetComponentsInChildren<SpriteRenderer>(true))
                if (renderer.sprite == null) renderer.sprite = square;
            SetObject(chosen, "followTarget", health.transform);
            Transform current = FindDeep(chosen.transform, "Current");
            if (current != null) SetObject(chosen, "fillSprite", current);
            SetObject(health, "worldHealthBar", chosen);
            chosen.SetFraction(1f);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, path))
            throw new InvalidOperationException("Failed to save " + path);
    }

    [MenuItem("Tools/Enemies/Validate Prefab Health Bars")]
    public static void Validate()
    {
        foreach (string path in PrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Enemy_Health health = prefab != null ? prefab.GetComponent<Enemy_Health>() : null;
            EnemyHealthBar[] bars = prefab != null ? prefab.GetComponentsInChildren<EnemyHealthBar>(true) : Array.Empty<EnemyHealthBar>();
            EnemyHealthBar referenced = health != null
                ? new SerializedObject(health).FindProperty("worldHealthBar")?.objectReferenceValue as EnemyHealthBar
                : null;
            if (bars.Length != 1 || referenced != bars[0] || bars[0].FollowTarget != prefab.transform ||
                bars[0].GetComponentsInChildren<SpriteRenderer>(true).Length != 2 ||
                bars[0].GetComponentsInChildren<SpriteRenderer>(true).Any(renderer => renderer.sprite == null))
                throw new InvalidOperationException(path + " health bar is not persistently authored.");
        }
        Debug.Log("MOB_HEALTH_BARS_VALIDATE_OK: 5 prefabs each contain one visible, referenced health bar.");
    }

    private static void ConfigureRenderer(SpriteRenderer renderer, Sprite sprite, Color color, int order)
    {
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = SceneArt.EffectSortingLayer;
        renderer.sortingOrder = order;
        renderer.enabled = true;
        EditorUtility.SetDirty(renderer);
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static void SetObject(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(name).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetFloat(UnityEngine.Object target, string name, float value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(name).floatValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetVector3(UnityEngine.Object target, string name, Vector3 value)
    {
        SerializedObject data = new SerializedObject(target);
        data.FindProperty(name).vector3Value = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
