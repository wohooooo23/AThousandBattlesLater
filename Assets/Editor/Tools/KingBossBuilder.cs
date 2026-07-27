#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Replaces only stage2's arena boss internals while preserving every scene reference.</summary>
public static class KingBossBuilder
{
    private const string Stage2Path = "Assets/Scenes/stage2_full.unity";
    private const string Stage1Path = "Assets/Scenes/stage1 boss.unity";
    private const string SpriteFolder = "Assets/Enemy/Bosses/Medieval King Pack 2/Sprites/";
    private const string VisualName = "KingVisual";
    private const string OnDamageMaterialPath = "Assets/Material/OnDamage_Material.mat";
    private const string BladeWaveMaterialPath = "Assets/Material/KingBladeWave_White.mat";
    private const string BladeWavePrefabPath = "Assets/Resources/AttackHitboxes/KingBladeWave.prefab";
    private const float TargetWorldHeight = 10f;

    [MenuItem("Tools/Boss/Replace Stage2 Boss With King")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(Stage2Path, OpenSceneMode.Single);
        BossArenaController arena = FindInScene<BossArenaController>(scene).SingleOrDefault() ??
            throw new MissingReferenceException(Stage2Path + " requires exactly one BossArenaController.");
        GameObject boss = arena.BossRoot ?? throw new MissingReferenceException("BossArenaController has no bossRoot.");

        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(boss);
        if (prefabRoot != null)
            PrefabUtility.UnpackPrefabInstance(prefabRoot, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

        bool alreadyKing = boss.transform.Find(VisualName) != null;
        boss.name = "Arena Boss - Medieval King";
        if (!alreadyKing)
        {
            RemoveWizardVisuals(boss);
            RemoveComponents<EnemyAttackPattern>(boss);
        }
        MeshRenderer placeholder = boss.GetComponent<MeshRenderer>();
        if (placeholder != null) UnityEngine.Object.DestroyImmediate(placeholder);
        MeshFilter placeholderMesh = boss.GetComponent<MeshFilter>();
        if (placeholderMesh != null) UnityEngine.Object.DestroyImmediate(placeholderMesh);

        BossSpriteAnimator animator;
        SpriteRenderer renderer;
        if (alreadyKing)
        {
            Transform visual = boss.transform.Find(VisualName);
            animator = visual.GetComponent<BossSpriteAnimator>();
            renderer = visual.GetComponent<SpriteRenderer>();
            if (animator == null || renderer == null)
                throw new MissingReferenceException("The saved KingVisual is missing its animator or renderer.");
        }
        else
        {
            animator = CreateKingVisual(boss, out renderer);
        }
        BossStateMachine stateMachine = boss.GetComponent<BossStateMachine>() ?? boss.AddComponent<BossStateMachine>();
        SetObject(stateMachine, "animator", animator);

        EnemyAttackController attacks = boss.GetComponent<EnemyAttackController>() ??
            boss.AddComponent<EnemyAttackController>();
        if (boss.GetComponent<EnemyPlatformNavigator>() == null)
            boss.AddComponent<EnemyPlatformNavigator>();
        ConfigureJumpRelocation(boss);
        EnemyHealth health = boss.GetComponent<EnemyHealth>() ??
            throw new MissingReferenceException("The preserved arena boss is missing EnemyHealth.");

        ConfigureHitFlash(boss, renderer);
        KingBladeWaveProjectile bladeWavePrefab = BuildBladeWavePrefab();
        ConfigurePatterns(boss, bladeWavePrefab);
        ConfigureAttackFeedback(boss, attacks);
        attacks.RefreshAttackPatterns();

        SerializedObject healthData = new SerializedObject(health);
        StoryDialogueController story = healthData.FindProperty("storyController")?.objectReferenceValue
            as StoryDialogueController;
        if (story == null)
            story = FindInScene<StoryDialogueController>(scene).FirstOrDefault(controller =>
                controller.SceneMode == StorySceneMode.Boss);
        if (story == null)
            throw new MissingReferenceException("stage2 arena boss has no StoryDialogueController.");
        SetObject(story, "bossVisualRoot", animator.transform);

        EditorUtility.SetDirty(boss);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, Stage2Path))
            throw new InvalidOperationException("Failed to save " + Stage2Path);
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log("KING_BOSS_BUILD_OK: stage2 King has four attacks, twelve accelerating blade waves, audio slots and active-camera shake.");
    }

    [MenuItem("Tools/Boss/Validate Stage2 Medieval King")]
    public static void Validate()
    {
        Scene stage2 = EditorSceneManager.OpenScene(Stage2Path, OpenSceneMode.Single);
        BossArenaController arena = FindInScene<BossArenaController>(stage2).SingleOrDefault() ??
            throw new InvalidOperationException("stage2 must contain one BossArenaController.");
        GameObject boss = arena.BossRoot;
        if (boss == null || boss.name != "Arena Boss - Medieval King")
            throw new InvalidOperationException("stage2 arena boss was not replaced in place.");

        Transform visual = boss.transform.Find(VisualName);
        BossSpriteAnimator animator = visual != null ? visual.GetComponent<BossSpriteAnimator>() : null;
        SpriteRenderer renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (animator == null || renderer == null || !animator.compensateOffCenterPivot ||
            !HasFrames(animator.idle, 8) || !HasFrames(animator.run, 8) ||
            !HasFrames(animator.attack1, 4) || !HasFrames(animator.attack2, 4) ||
            !HasFrames(animator.attack3, 4) || !HasFrames(animator.takeHit, 4) ||
            !HasFrames(animator.death, 6))
            throw new InvalidOperationException("KingVisual is missing one or more sliced animation sets.");

        EnemyAttackPattern[] patterns = boss.GetComponents<EnemyAttackPattern>();
        KingHorizontalSlashPattern horizontal = boss.GetComponent<KingHorizontalSlashPattern>();
        KingUppercutArcPattern uppercut = boss.GetComponent<KingUppercutArcPattern>();
        KingGroundCleavePattern cleave = boss.GetComponent<KingGroundCleavePattern>();
        KingRadialBladeBurstPattern radial = boss.GetComponent<KingRadialBladeBurstPattern>();
        if (patterns.Length != 4 || horizontal == null || uppercut == null || cleave == null || radial == null)
            throw new InvalidOperationException("The King must have exactly the four designed attacks.");
        float arenaHalfWidth = (arena.ArenaMax.x - arena.ArenaMin.x) * 0.5f;
        if (horizontal.CastAnim != CastAnimation.Attack1 || uppercut.CastAnim != CastAnimation.Attack2 ||
            cleave.CastAnim != CastAnimation.Attack3 || radial.CastAnim != CastAnimation.Attack2 ||
            ReadFloat(cleave, "reachDistance") < arenaHalfWidth || ReadFloat(cleave, "cleaveHeight") < 30f ||
            ReadInt(radial, "projectileCount") != 12 || ReadFloat(radial, "slashRadius") < 25f ||
            ReferencedObject(radial, "bladeWavePrefab") == null)
            throw new InvalidOperationException("King attack animation mapping or authored half-arena size is invalid.");
        KingAttackAudio audio = boss.GetComponent<KingAttackAudio>();
        AudioSource audioSource = boss.GetComponent<AudioSource>();
        if (audio == null || audioSource == null || audioSource.playOnAwake || audioSource.loop ||
            ReferencedObject(boss.GetComponent<EnemyAttackController>(), "attackAudio") != audio)
            throw new InvalidOperationException("King attack audio loader and its three animation slots are not saved on the boss.");
        KingBladeWaveProjectile wavePrefab = AssetDatabase.LoadAssetAtPath<KingBladeWaveProjectile>(BladeWavePrefabPath);
        Material waveMaterial = AssetDatabase.LoadAssetAtPath<Material>(BladeWaveMaterialPath);
        if (wavePrefab == null || waveMaterial == null || wavePrefab.GetComponent<MeshRenderer>().sharedMaterial != waveMaterial ||
            waveMaterial.color != Color.white)
            throw new InvalidOperationException("The tapered blade-wave prefab must use the saved pure-white material.");
        CameraShake2D[] cameraShakes = FindInScene<CameraShake2D>(stage2);
        if (!cameraShakes.Any(shake => shake.name == "Main Camera") ||
            !cameraShakes.Any(shake => shake.name == "Boss Arena Camera"))
            throw new InvalidOperationException("Both stage2 gameplay cameras must own a saved CameraShake2D component.");
        BossTeleport relocation = boss.GetComponent<BossTeleport>();
        if (relocation == null || relocation.RelocationMode != BossRelocationMode.Jump ||
            relocation.AttacksPerRelocation != 3 || ReadFloat(relocation, "jumpSpeedMultiplier") <= 1f)
            throw new InvalidOperationException("The stage2 King requires an accelerated every-three-attacks pursuit hop.");
        EnemyHealth bossHealth = boss.GetComponent<EnemyHealth>();
        if (boss.GetComponent<EnemyPlatformNavigator>() == null || boss.GetComponent<EnemyAttackController>() == null ||
            bossHealth == null)
            throw new InvalidOperationException("The King must preserve health, controller and node navigation.");
        BossHealthBarController healthBar = ReferencedObject(arena, "bossHealthBar") as BossHealthBarController;
        if (healthBar == null || healthBar.BoundHealth != bossHealth)
            throw new InvalidOperationException("The arena Boss health bar no longer references the preserved EnemyHealth.");

        BossStateMachine state = boss.GetComponent<BossStateMachine>();
        Entity_VFX vfx = boss.GetComponent<Entity_VFX>();
        if (ReferencedObject(state, "animator") != animator || ReferencedObject(vfx, "targetRenderer") != renderer)
            throw new InvalidOperationException("King animator or hit-flash renderer is not wired.");
        StoryDialogueController story = ReferencedObject(bossHealth, "storyController") as StoryDialogueController;
        if (story == null || ReferencedObject(story, "bossVisualRoot") != visual)
            throw new InvalidOperationException("Boss story is not wired to KingVisual.");

        Scene stage1 = EditorSceneManager.OpenScene(Stage1Path, OpenSceneMode.Single);
        EnemyHealth wizard = FindInScene<EnemyHealth>(stage1).SingleOrDefault();
        if (wizard == null || wizard.transform.Find("WizardVisual") == null ||
            wizard.GetComponents<EnemyAttackPattern>().Any(pattern => pattern is KingHorizontalSlashPattern ||
                pattern is KingUppercutArcPattern || pattern is KingGroundCleavePattern ||
                pattern is KingRadialBladeBurstPattern))
            throw new InvalidOperationException("stage1 Evil Wizard was modified by the King build.");

        Debug.Log("KING_BOSS_VALIDATE_OK: four attacks, radial blade prefab, audio slots, camera shake and retreat hop verified; stage1 unchanged.");
    }

    private static BossSpriteAnimator CreateKingVisual(GameObject boss, out SpriteRenderer renderer)
    {
        GameObject visual = new GameObject(VisualName, typeof(SpriteRenderer), typeof(BossSpriteAnimator));
        visual.transform.SetParent(boss.transform, false);
        renderer = visual.GetComponent<SpriteRenderer>();
        renderer.sortingLayerName = SceneArt.EffectSortingLayer;
        renderer.sortingOrder = 10;

        BossSpriteAnimator animator = visual.GetComponent<BossSpriteAnimator>();
        animator.defaultFacesRight = true;
        animator.compensateOffCenterPivot = true;
        animator.idle.frames = LoadFrames("Idle");
        animator.run.frames = LoadFrames("Run");
        animator.attack1.frames = LoadFrames("Attack1");
        animator.attack2.frames = LoadFrames("Attack2");
        animator.attack3.frames = LoadFrames("Attack3");
        animator.takeHit.frames = LoadFrames("Take Hit");
        animator.death.frames = LoadFrames("Death");
        ConfigureClip(animator.idle, 8f, true, 0);
        ConfigureClip(animator.run, 12f, true, 0);
        ConfigureClip(animator.attack1, 10f, false, 2);
        ConfigureClip(animator.attack2, 10f, false, 2);
        ConfigureClip(animator.attack3, 10f, false, 2);
        ConfigureClip(animator.takeHit, 12f, false, 0);
        ConfigureClip(animator.death, 8f, false, 0);

        if (animator.idle.frames.Length == 0)
            throw new MissingReferenceException("Medieval King Idle sheet has no sliced sprites.");
        Sprite first = animator.idle.frames[0];
        renderer.sprite = first;
        float rootWorldScale = Mathf.Max(0.0001f, Mathf.Abs(boss.transform.lossyScale.y));
        float localScale = TargetWorldHeight / (Mathf.Max(0.01f, first.bounds.size.y) * rootWorldScale);
        visual.transform.localScale = Vector3.one * localScale;

        Collider2D bossCollider = boss.GetComponent<Collider2D>();
        float localFeetY = bossCollider != null
            ? (bossCollider.bounds.min.y - boss.transform.position.y) / rootWorldScale
            : 0f;
        visual.transform.localPosition = new Vector3(-first.bounds.center.x * localScale,
            localFeetY - first.bounds.min.y * localScale, 0f);
        return animator;
    }

    private static void ConfigurePatterns(GameObject boss, KingBladeWaveProjectile bladeWavePrefab)
    {
        KingHorizontalSlashPattern horizontal = boss.GetComponent<KingHorizontalSlashPattern>();
        if (horizontal == null)
        {
            horizontal = boss.AddComponent<KingHorizontalSlashPattern>();
            SetPatternBase(horizontal, 10f, 58f, 1.15f, CastAnimation.Attack1);
            SetFloat(horizontal, "warningDuration", 1.1f);
            SetFloat(horizontal, "length", 42f);
            SetFloat(horizontal, "height", 5f);
        }

        KingUppercutArcPattern uppercut = boss.GetComponent<KingUppercutArcPattern>();
        if (uppercut == null)
        {
            uppercut = boss.AddComponent<KingUppercutArcPattern>();
            SetPatternBase(uppercut, 0f, 23f, 1.35f, CastAnimation.Attack2);
            SetFloat(uppercut, "warningDuration", 1f);
            SetFloat(uppercut, "radius", 20f);
            SetFloat(uppercut, "sectorAngle", 240f);
        }

        KingGroundCleavePattern cleave = boss.GetComponent<KingGroundCleavePattern>();
        if (cleave == null)
        {
            cleave = boss.AddComponent<KingGroundCleavePattern>();
            SetPatternBase(cleave, 8f, 250f, 1f, CastAnimation.Attack3);
            SetFloat(cleave, "warningDuration", 1.25f);
            SetFloat(cleave, "reachDistance", 245f);
            SetFloat(cleave, "cleaveHeight", 32f);
            SetInt(cleave, "groundMask", 1 << 6);
        }

        KingRadialBladeBurstPattern radial = boss.GetComponent<KingRadialBladeBurstPattern>();
        if (radial == null)
        {
            radial = boss.AddComponent<KingRadialBladeBurstPattern>();
            SetPatternBase(radial, 0f, 250f, 1.05f, CastAnimation.Attack2);
            SetFloat(radial, "warningDuration", 1.15f);
            SetFloat(radial, "slashRadius", 28f);
            SetInt(radial, "projectileCount", 12);
            SetFloat(radial, "projectileSpawnRadius", 4f);
            SetFloat(radial, "projectileInitialSpeed", 10f);
            SetFloat(radial, "projectileAcceleration", 18f);
            SetFloat(radial, "projectileSpinSpeed", 300f);
            SetFloat(radial, "projectileLifetime", 3f);
        }
        SetObject(radial, "bladeWavePrefab", bladeWavePrefab);
    }

    private static void ConfigureAttackFeedback(GameObject boss, EnemyAttackController attacks)
    {
        AudioSource source = boss.GetComponent<AudioSource>();
        if (source == null)
            source = boss.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        KingAttackAudio audio = boss.GetComponent<KingAttackAudio>();
        if (audio == null)
            audio = boss.AddComponent<KingAttackAudio>();
        SetObject(attacks, "attackAudio", audio);
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(audio);
    }

    private static KingBladeWaveProjectile BuildBladeWavePrefab()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BladeWaveMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"))
            {
                name = "KingBladeWave_White",
                color = Color.white
            };
            AssetDatabase.CreateAsset(material, BladeWaveMaterialPath);
        }
        else
        {
            material.color = Color.white;
            EditorUtility.SetDirty(material);
        }

        GameObject root = new GameObject("KingBladeWave", typeof(MeshFilter), typeof(MeshRenderer),
            typeof(PolygonCollider2D), typeof(Rigidbody2D), typeof(KingBladeWaveProjectile));
        try
        {
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = SceneArt.EffectSortingLayer;
            renderer.sortingOrder = 31;
            PolygonCollider2D collider = root.GetComponent<PolygonCollider2D>();
            collider.isTrigger = true;
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BladeWavePrefabPath);
            return prefab.GetComponent<KingBladeWaveProjectile>();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void SetPatternBase(EnemyAttackPattern pattern, float min, float max, float weight,
        CastAnimation cast)
    {
        SetFloat(pattern, "minimumRange", min);
        SetFloat(pattern, "maximumRange", max);
        SetFloat(pattern, "selectionWeight", weight);
        SetInt(pattern, "castAnimation", (int)cast);
    }

    private static void ConfigureHitFlash(GameObject boss, SpriteRenderer renderer)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(OnDamageMaterialPath) ??
            throw new MissingReferenceException("Missing " + OnDamageMaterialPath);
        Entity_VFX vfx = boss.GetComponent<Entity_VFX>() ?? boss.AddComponent<Entity_VFX>();
        SetObject(vfx, "targetRenderer", renderer);
        SetObject(vfx, "onDamageMaterial", material);
    }

    private static void ConfigureJumpRelocation(GameObject boss)
    {
        BossTeleport relocation = boss.GetComponent<BossTeleport>() ?? boss.AddComponent<BossTeleport>();
        SetInt(relocation, "attacksPerRelocation", 3);
        SetInt(relocation, "relocationMode", (int)BossRelocationMode.Jump);
        SetFloat(relocation, "jumpSpeedMultiplier", 1.75f);
    }

    private static void RemoveWizardVisuals(GameObject boss)
    {
        foreach (BossSpriteAnimator animator in boss.GetComponentsInChildren<BossSpriteAnimator>(true).ToArray())
            if (animator != null)
                UnityEngine.Object.DestroyImmediate(animator.gameObject);
        Transform wizard = boss.transform.Find("WizardVisual");
        if (wizard != null) UnityEngine.Object.DestroyImmediate(wizard.gameObject);
        Transform king = boss.transform.Find(VisualName);
        if (king != null) UnityEngine.Object.DestroyImmediate(king.gameObject);
    }

    private static void RemoveComponents<T>(GameObject target) where T : Component
    {
        foreach (T component in target.GetComponents<T>())
            UnityEngine.Object.DestroyImmediate(component);
    }

    private static Sprite[] LoadFrames(string sheet)
    {
        string path = SpriteFolder + sheet + ".png";
        List<Sprite> sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().ToList();
        sprites.Sort((left, right) => FrameIndex(left.name).CompareTo(FrameIndex(right.name)));
        if (sprites.Count == 0)
            throw new MissingReferenceException(path + " must be imported as sliced Sprite (Multiple).");
        return sprites.ToArray();
    }

    private static int FrameIndex(string name)
    {
        int split = name.LastIndexOf('_');
        return split >= 0 && int.TryParse(name.Substring(split + 1), out int index) ? index : 0;
    }

    private static void ConfigureClip(BossSpriteAnimator.Clip clip, float fps, bool loop, int releaseFrame)
    {
        clip.fps = fps;
        clip.loop = loop;
        clip.releaseFrame = releaseFrame;
    }

    private static bool HasFrames(BossSpriteAnimator.Clip clip, int expected) =>
        clip != null && clip.frames != null && clip.frames.Length == expected;

    private static T[] FindInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static UnityEngine.Object ReferencedObject(UnityEngine.Object target, string property)
    {
        if (target == null) return null;
        return new SerializedObject(target).FindProperty(property)?.objectReferenceValue;
    }

    private static float ReadFloat(UnityEngine.Object target, string property)
    {
        SerializedProperty field = new SerializedObject(target).FindProperty(property) ??
            throw new MissingFieldException(target.name, property);
        return field.floatValue;
    }

    private static int ReadInt(UnityEngine.Object target, string property)
    {
        SerializedProperty field = new SerializedObject(target).FindProperty(property) ??
            throw new MissingFieldException(target.name, property);
        return field.intValue;
    }

    private static void SetObject(UnityEngine.Object target, string property, UnityEngine.Object value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty field = data.FindProperty(property) ?? throw new MissingFieldException(target.name, property);
        field.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetFloat(UnityEngine.Object target, string property, float value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty field = data.FindProperty(property) ?? throw new MissingFieldException(target.name, property);
        field.floatValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(UnityEngine.Object target, string property, int value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty field = data.FindProperty(property) ?? throw new MissingFieldException(target.name, property);
        field.intValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
