#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Organizes the BossHp package and authors its animated HUD into the boss scene.</summary>
public static class BossHealthBarBuilder
{
    public const string BossScenePath = "Assets/Scenes/stage1 boss.unity";
    public const string BossBarFolder = "Assets/Enemy/Bosses/EvilWizard/UI/BossHealthBar";
    public const string BossBarPrefabPath = BossBarFolder + "/BossHealthBar.prefab";
    private const string CanvasPrefabPath = "Assets/Prefab/Canvas.prefab";

    [MenuItem("Tools/A Thousand Battles Later/Build Boss Health Bar")]
    public static void BuildAndAttach()
    {
        OrganizeImportedAssets();
        ConfigurePrefab();

        Scene scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Single);
        EnemyHealth health = UnityEngine.Object.FindAnyObjectByType<EnemyHealth>(FindObjectsInactive.Include);
        if (health == null)
            throw new MissingReferenceException(BossScenePath + " has no EnemyHealth boss.");
        AttachToOpenScene(scene, health);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, BossScenePath))
            throw new InvalidOperationException("Failed to save " + BossScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateOpenScene();
        Debug.Log("[BossHealthBarBuilder] Imported reveal animation and live boss HP binding are saved in stage1 boss.");
    }

    [MenuItem("Tools/A Thousand Battles Later/Validate Boss Health Bar")]
    public static void Validate()
    {
        EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Single);
        ValidateOpenScene();
        Debug.Log("[BossHealthBarBuilder] Boss health bar validation passed.");
    }

    public static BossHealthBarController AttachToOpenScene(Scene scene, EnemyHealth health)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BossBarPrefabPath) == null)
        {
            OrganizeImportedAssets();
            ConfigurePrefab();
        }

        RemoveLegacyBossBars(health);
        Canvas canvas = FindAlphaCanvas();
        if (canvas == null)
            throw new MissingReferenceException("The boss scene needs its scene-authored Alpha UI Canvas.");

        BossHealthBarController controller = null;
        foreach (BossHealthBarController candidate in UnityEngine.Object.FindObjectsByType<BossHealthBarController>(
                     FindObjectsInactive.Include))
        {
            if (candidate.gameObject.scene == scene)
            {
                controller = candidate;
                break;
            }
        }

        if (controller == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossBarPrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "Boss HUD Health Bar";
            instance.transform.SetParent(canvas.transform, false);
            controller = instance.GetComponent<BossHealthBarController>();
        }

        RectTransform rect = controller.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -48f);
        rect.sizeDelta = new Vector2(384f, 34f);
        rect.localScale = Vector3.one * 1.55f;
        controller.Configure(health);
        StoryDialogueController story = UnityEngine.Object.FindAnyObjectByType<StoryDialogueController>(
            FindObjectsInactive.Include);
        if (story != null && story.gameObject.scene == scene)
            controller.ConfigureStory(story);

        SerializedObject healthData = new SerializedObject(health);
        SerializedProperty worldBar = healthData.FindProperty("worldHealthBar");
        if (worldBar != null)
            worldBar.objectReferenceValue = null;
        healthData.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(health);
        EditorUtility.SetDirty(controller.FillMask);
        EditorUtility.SetDirty(controller.FillMask.rectTransform);
        EditorUtility.SetDirty(controller.FillImageRect);
        EditorUtility.SetDirty(controller.FillImageRect.gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        PrefabUtility.RecordPrefabInstancePropertyModifications(health);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller.FillMask);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller.FillMask.rectTransform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller.FillImageRect);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller.FillImageRect.gameObject);
        return controller;
    }

    public static void ValidateOpenScene()
    {
        EnemyHealth health = UnityEngine.Object.FindAnyObjectByType<EnemyHealth>(FindObjectsInactive.Include);
        BossHealthBarController[] bars = UnityEngine.Object.FindObjectsByType<BossHealthBarController>(
            FindObjectsInactive.Include);
        if (health == null || bars.Length != 1)
            throw new InvalidOperationException("Boss scene must contain one EnemyHealth and one BossHealthBarController.");

        BossHealthBarController bar = bars[0];
        StoryDialogueController story = UnityEngine.Object.FindAnyObjectByType<StoryDialogueController>(
            FindObjectsInactive.Include);
        if (bar.BoundHealth != health || bar.SpawnAnimator == null || bar.SpawnAnimationObject == null ||
            bar.CombatBarGroup == null || bar.FillMask == null || bar.RevealDelayFrames < 3 ||
            (story != null && bar.StoryController != story))
            throw new InvalidOperationException("Boss HUD references are incomplete.");
        BossHealthBarSpawnRelay relay = bar.SpawnAnimationObject.GetComponent<BossHealthBarSpawnRelay>();
        if (relay == null || relay.Controller != bar)
            throw new InvalidOperationException("Boss reveal animation event relay is missing.");
        if (bar.GetComponentInParent<Canvas>() == null)
            throw new InvalidOperationException("Boss HUD must be stored under a scene Canvas.");
        foreach (EnemyHealthBar worldBar in UnityEngine.Object.FindObjectsByType<EnemyHealthBar>(FindObjectsInactive.Include))
            if (worldBar.FollowTarget == health.transform)
                throw new InvalidOperationException("The legacy world-space boss bar still exists.");
    }

    private static void ConfigurePrefab()
    {
        AnimationClip revealClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            BossBarFolder + "/Visual/BossBar_Spawn.anim");
        if (revealClip == null)
            throw new InvalidOperationException("BossBar_Spawn.anim is missing.");
        EditorCurveBinding[] spriteBindings = AnimationUtility.GetObjectReferenceCurveBindings(revealClip);
        if (spriteBindings.Length == 0)
            throw new InvalidOperationException("BossBar_Spawn.anim has no sprite frames.");
        ObjectReferenceKeyframe[] spriteFrames = AnimationUtility.GetObjectReferenceCurve(revealClip, spriteBindings[0]);
        foreach (EditorCurveBinding oldBinding in spriteBindings)
            AnimationUtility.SetObjectReferenceCurve(revealClip, oldBinding, null);
        AnimationUtility.SetObjectReferenceCurve(revealClip, new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(Image),
            propertyName = "m_Sprite"
        }, spriteFrames);
        AnimationUtility.SetAnimationEvents(revealClip, new[]
        {
            new AnimationEvent { time = revealClip.length, functionName = "OnSpawnFinished" }
        });
        EditorUtility.SetDirty(revealClip);

        GameObject root = PrefabUtility.LoadPrefabContents(BossBarPrefabPath);
        try
        {
            BossHealthBarController controller = root.GetComponent<BossHealthBarController>();
            if (controller == null || controller.SpawnAnimationObject == null || controller.SpawnAnimator == null)
                throw new InvalidOperationException("Imported BossHealthBar.prefab is incomplete.");

            BossHealthBarSpawnRelay relay = controller.SpawnAnimationObject.GetComponent<BossHealthBarSpawnRelay>();
            if (relay == null)
                relay = controller.SpawnAnimationObject.AddComponent<BossHealthBarSpawnRelay>();
            relay.Configure(controller);
            controller.SpawnAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            SerializedObject controllerData = new SerializedObject(controller);
            controllerData.FindProperty("authoredFullWidth").floatValue = 379.4566f;
            controllerData.FindProperty("revealDelayFrames").intValue = 4;
            controllerData.ApplyModifiedPropertiesWithoutUndo();
            controller.SetFraction(1f);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(384f, 34f);
            PrefabUtility.SaveAsPrefabAsset(root, BossBarPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveLegacyBossBars(EnemyHealth health)
    {
        foreach (EnemyHealthBar bar in UnityEngine.Object.FindObjectsByType<EnemyHealthBar>(FindObjectsInactive.Include))
        {
            if (bar.FollowTarget == health.transform || bar.name == "Boss Health Bar")
                UnityEngine.Object.DestroyImmediate(bar.gameObject);
        }
    }

    private static Canvas FindAlphaCanvas()
    {
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            GameObject nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(canvas.gameObject);
            if (nearestRoot != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(nearestRoot) == CanvasPrefabPath)
                return canvas;
        }
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            if (canvas.renderMode != RenderMode.WorldSpace)
                return canvas;
        return null;
    }

    private static void OrganizeImportedAssets()
    {
        EnsureFolder("Assets/Enemy/Bosses/EvilWizard/UI");
        EnsureFolder(BossBarFolder);
        MoveIfNeeded("Assets/Resources/Prefabs/BossHealthBar.prefab", BossBarPrefabPath);
        MoveImportedVisuals();
        // This texture pre-dated BossHp.unitypackage and belongs to the player's existing HPBar prefab.
        // The package shares its source folder, so put it back after moving the Boss-specific files.
        EnsureFolder("Assets/Resources/Sprites/UI/Fantasy");
        MoveIfNeeded(BossBarFolder + "/Visual/bones_UI_HP+mana_clean.png",
            "Assets/Resources/Sprites/UI/Fantasy/bones_UI_HP+mana_clean.png");
        MoveIfNeeded("Assets/Scripts/BossHealthBarController.cs", "Assets/Scripts/UI/BossHealthBarController.cs");
    }

    private static void MoveImportedVisuals()
    {
        const string sourceFolder = "Assets/Resources/Sprites/UI/Fantasy";
        string destinationFolder = BossBarFolder + "/Visual";
        if (!AssetDatabase.IsValidFolder(destinationFolder))
        {
            MoveIfNeeded(sourceFolder, destinationFolder);
            return;
        }

        // On repeat builds the source folder also exists because the player's pre-existing HP
        // texture is deliberately restored there. Move only the BossHp package assets.
        foreach (string file in new[]
                 {
                     "boss_bar1-Sheet.png", "boss_bar1-Sheet_0.controller", "boss_bar_fill.png",
                     "BossBar_Spawn.anim", "boss_bar_frame.png"
                 })
            MoveIfNeeded(sourceFolder + "/" + file, destinationFolder + "/" + file);
    }

    private static void MoveIfNeeded(string source, string destination)
    {
        bool sourceExists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(source));
        bool destinationExists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destination));
        if (!sourceExists)
        {
            if (!destinationExists)
                throw new InvalidOperationException("Missing imported asset: " + source);
            return;
        }
        if (destinationExists)
            throw new InvalidOperationException("Both old and new Boss HP paths exist: " + source);
        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException(error);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int separator = path.LastIndexOf('/');
        string parent = path[..separator];
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path[(separator + 1)..]);
    }
}
#endif
