using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>One-shot, repeatable migration/build pipeline for the unified enemy catalogue.</summary>
public static class EnemyContentBuilder
{
    private const string EnemyRoot = "Assets/Enemy";
    private const string OrcPrefab = EnemyRoot + "/Mobs/Orc/Mob_Orc.prefab";
    private const string BossPrefab = EnemyRoot + "/Bosses/EvilWizard/Boss_EvilWizard.prefab";

    private sealed class MobDefinition
    {
        public string name;
        public string sourceFolder;
        public string idleSheet;
        public string moveSheet;
        public bool flying;
    }

    private static readonly MobDefinition[] NewMobs =
    {
        new() { name = "Goblin", sourceFolder = "Goblin", idleSheet = "Idle.png", moveSheet = "Run.png" },
        new() { name = "Mushroom", sourceFolder = "Mushroom", idleSheet = "Idle.png", moveSheet = "Run.png" },
        new() { name = "FlyingEye", sourceFolder = "Flying eye", idleSheet = "Flight.png", moveSheet = "Flight.png", flying = true },
        new() { name = "Skeleton", sourceFolder = "Skeleton", idleSheet = "Idle.png", moveSheet = "Walk.png" }
    };

    [MenuItem("Tools/A Thousand Battles Later/Build Unified Enemy Catalogue")]
    public static void Build()
    {
        try
        {
            EnsureFolders();
            float orcHealth = ReadOrcHealth();
            int orcReward = ReadOrcReward();
            MoveExistingEnemies();
            MoveNewMobSprites();

            foreach (MobDefinition mob in NewMobs)
                BuildMobPrefab(mob, orcHealth, orcReward);

            RemoveImportedDemoContent();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateOrThrow();
            Debug.Log("[EnemyContentBuilder] Unified enemy catalogue built and validated successfully.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    [MenuItem("Tools/A Thousand Battles Later/Validate Unified Enemy Catalogue")]
    public static void Validate()
    {
        ValidateOrThrow();
        Debug.Log("[EnemyContentBuilder] Enemy catalogue validation passed: 5 mobs, 1 boss, 4 new state machines.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder(EnemyRoot);
        EnsureFolder(EnemyRoot + "/Mobs");
        EnsureFolder(EnemyRoot + "/Bosses");
        EnsureFolder(EnemyRoot + "/Mobs/Orc");
        EnsureFolder(EnemyRoot + "/Bosses/EvilWizard");
        foreach (MobDefinition mob in NewMobs)
            EnsureFolder(EnemyRoot + "/Mobs/" + mob.name);
    }

    private static void MoveExistingEnemies()
    {
        MoveAssetIfNeeded("Assets/Prefab/Enemy_Orc.prefab", OrcPrefab);
        MoveAssetIfNeeded("Assets/Textures/Orc", EnemyRoot + "/Mobs/Orc/Sprites");
        MoveAssetIfNeeded("Assets/Animations/Enemy/Orc", EnemyRoot + "/Mobs/Orc/Animations");

        MoveAssetIfNeeded("Assets/Prefab/Boss.prefab", BossPrefab);
        MoveAssetIfNeeded("Assets/EvilWizard2", EnemyRoot + "/Bosses/EvilWizard/Visual");
    }

    private static void MoveNewMobSprites()
    {
        foreach (MobDefinition mob in NewMobs)
        {
            string source = "Assets/Sprites/" + mob.sourceFolder;
            string destination = EnemyRoot + "/Mobs/" + mob.name + "/Sprites";
            MoveAssetIfNeeded(source, destination);
        }
        AssetDatabase.Refresh();
    }

    private static void BuildMobPrefab(MobDefinition mob, float maximumHealth, int coinReward)
    {
        string speciesFolder = EnemyRoot + "/Mobs/" + mob.name;
        string spriteFolder = speciesFolder + "/Sprites/";
        string prefabPath = speciesFolder + "/Mob_" + mob.name + ".prefab";

        Sprite[] idle = LoadFrames(spriteFolder + mob.idleSheet);
        Sprite[] move = LoadFrames(spriteFolder + mob.moveSheet);
        Sprite[] hurt = LoadFrames(spriteFolder + "Take Hit.png");
        Sprite[] dead = LoadFrames(spriteFolder + "Death.png");
        Sprite[] attackOne = LoadFrames(spriteFolder + "Attack1.png");
        Sprite[] attackTwo = LoadFrames(spriteFolder + "Attack2.png");
        if (idle.Length == 0 || move.Length == 0 || hurt.Length == 0 || dead.Length == 0)
            throw new InvalidOperationException($"{mob.name} is missing a required sprite-sheet animation.");

        GameObject root = new GameObject("Mob_" + mob.name);
        try
        {
            root.transform.localScale = Vector3.one * 1.25f;
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.gravityScale = mob.flying ? 0f : 3.4f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D collider = root.AddComponent<CapsuleCollider2D>();
            collider.size = mob.flying ? new Vector2(1.8f, 1.25f) : new Vector2(1.2f, 2.2f);
            collider.direction = mob.flying ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;

            Enemy_Health health = root.AddComponent<Enemy_Health>();
            SerializedObject healthData = new SerializedObject(health);
            healthData.FindProperty("maximumHealth").floatValue = maximumHealth;
            healthData.FindProperty("coinReward").intValue = coinReward;
            healthData.ApplyModifiedPropertiesWithoutUndo();

            GameObject visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sprite = idle[0];
            renderer.sortingOrder = 2;

            float desiredHeight = mob.flying ? 2.4f : 3.2f;
            float spriteHeight = Mathf.Max(0.01f, idle[0].bounds.size.y);
            visualObject.transform.localScale = Vector3.one * (desiredHeight / spriteHeight);

            MobSpriteAnimator animator = visualObject.AddComponent<MobSpriteAnimator>();
            animator.idle = Clip(idle, 8f, true);
            animator.move = Clip(move, 12f, true);
            animator.hurt = Clip(hurt, 12f, false);
            animator.dead = Clip(dead, 10f, false);
            animator.attackOne = Clip(attackOne, 12f, false);
            animator.attackTwo = Clip(attackTwo, 12f, false);

            MobStateMachine machine = root.AddComponent<MobStateMachine>();
            SerializedObject machineData = new SerializedObject(machine);
            machineData.FindProperty("visual").objectReferenceValue = animator;
            machineData.FindProperty("flying").boolValue = mob.flying;

            // The Flying Eye is the only mob with attack logic, and this builder rebuilds prefabs
            // from scratch — so it has to author the ranged attack itself. Leaving it to
            // DemoSceneBuilder meant a standalone catalogue rebuild stripped the component and then
            // failed its own ValidateOrThrow check.
            if (mob.name == "FlyingEye")
            {
                FlyingEyeRangedAttack ranged = root.AddComponent<FlyingEyeRangedAttack>();
                SerializedObject rangedData = new SerializedObject(ranged);
                rangedData.FindProperty("visual").objectReferenceValue = animator;
                rangedData.FindProperty("projectilePrefab").objectReferenceValue = EnsureFlyingEyeProjectile();
                rangedData.FindProperty("attackRange").floatValue = FlyingEyeAttackRange;
                rangedData.FindProperty("preferredDistance").floatValue = FlyingEyePreferredDistance;
                rangedData.FindProperty("windupDuration").floatValue = MobAttackWindup;
                rangedData.FindProperty("cooldown").floatValue = MobAttackCooldown;
                rangedData.FindProperty("projectileSpeed").floatValue = FlyingEyeProjectileSpeed;
                rangedData.FindProperty("damage").floatValue = CombatBalance.EnemyDamagePerHit;
                rangedData.FindProperty("warningDiameter").floatValue = FlyingEyeWarningDiameter;
                rangedData.ApplyModifiedPropertiesWithoutUndo();

                machineData.FindProperty("rangedAttack").objectReferenceValue = ranged;
                machineData.FindProperty("detectionRange").floatValue = FlyingEyeDetectionRange;
                machineData.FindProperty("patrolRange").floatValue = 10f;
                machineData.FindProperty("patrolSpeed").floatValue = 5f;
                machineData.FindProperty("chaseSpeed").floatValue = 8f;
            }

            machineData.ApplyModifiedPropertiesWithoutUndo();

            // Same hit feedback as the Orc. Entity_Health.Awake resolves this with GetComponent, so
            // the component has to sit on the root while the flash targets the Visual child.
            Entity_VFX vfx = root.AddComponent<Entity_VFX>();
            SerializedObject vfxData = new SerializedObject(vfx);
            vfxData.FindProperty("targetRenderer").objectReferenceValue = renderer;
            vfxData.FindProperty("onDamageMaterial").objectReferenceValue = LoadOnDamageMaterial();
            vfxData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // Flying Eye ranged-attack tuning. DemoSceneBuilder re-applies the same values when it authors
    // scene instances; these keep a standalone catalogue rebuild valid on its own.
    private const float MobAttackWindup = 0.95f;
    private const float MobAttackCooldown = 1.35f;
    private const float FlyingEyeAttackRange = 38f;
    private const float FlyingEyePreferredDistance = 24f;
    private const float FlyingEyeDetectionRange = 48f;
    private const float FlyingEyeProjectileSpeed = 22f;
    private const float FlyingEyeProjectileScale = 1.75f;
    private const float FlyingEyeWarningDiameter = 7.5f;
    private const string FlyingEyeProjectilePath = EnemyRoot + "/Mobs/FlyingEye/FlyingEyeProjectile.prefab";
    private const string AttackCircleSpritePath = "Assets/Resources/AttackHitboxes/AttackCircle.png";

    /// <summary>
    /// Returns the Flying Eye projectile prefab, creating it when absent. FlyingEyeRangedAttack
    /// throws at Awake without it, so the reference can never be left empty.
    /// </summary>
    private static GameObject EnsureFlyingEyeProjectile()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(FlyingEyeProjectilePath);
        if (existing != null)
            return existing;

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AttackCircleSpritePath);
        if (sprite == null)
            throw new InvalidOperationException("Flying Eye projectile requires " + AttackCircleSpritePath + ".");

        GameObject projectile = new GameObject("FlyingEyeProjectile", typeof(SpriteRenderer),
            typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(FlyingEyeProjectile2D));
        try
        {
            projectile.transform.localScale = Vector3.one * FlyingEyeProjectileScale;
            SpriteRenderer renderer = projectile.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 0.05f, 0.05f, 0.95f);
            renderer.sortingOrder = 30;
            Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CircleCollider2D collider = projectile.GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5625f;
            return PrefabUtility.SaveAsPrefabAsset(projectile, FlyingEyeProjectilePath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(projectile);
        }
    }

    private const string OnDamageMaterialPath = "Assets/Material/OnDamage_Material.mat";

    private static Material LoadOnDamageMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(OnDamageMaterialPath);
        if (material == null)
            throw new InvalidOperationException("Missing hit-flash material at " + OnDamageMaterialPath + ".");
        return material;
    }

    private static MobAnimationFrames Clip(Sprite[] frames, float fps, bool loop)
    {
        return new MobAnimationFrames { frames = frames, framesPerSecond = fps, loop = loop };
    }

    private static Sprite[] LoadFrames(string texturePath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(texturePath)
            .OfType<Sprite>()
            .OrderBy(sprite => FrameNumber(sprite.name))
            .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();
    }

    private static int FrameNumber(string spriteName)
    {
        int separator = spriteName.LastIndexOf('_');
        return separator >= 0 && int.TryParse(spriteName[(separator + 1)..], out int frame) ? frame : int.MaxValue;
    }

    private static float ReadOrcHealth()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrcPrefab) ??
                            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy_Orc.prefab");
        Enemy_Health health = prefab != null ? prefab.GetComponent<Enemy_Health>() : null;
        return health != null ? Mathf.Max(1f, health.MaximumHealth) : CombatBalance.DefaultMaximumHealth;
    }

    private static int ReadOrcReward()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrcPrefab) ??
                            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy_Orc.prefab");
        Enemy_Health health = prefab != null ? prefab.GetComponent<Enemy_Health>() : null;
        return health != null ? Mathf.Max(1, health.CoinReward) : 20;
    }

    private static void ValidateOrThrow()
    {
        string[] expectedPrefabs =
        {
            OrcPrefab,
            EnemyRoot + "/Mobs/Goblin/Mob_Goblin.prefab",
            EnemyRoot + "/Mobs/Mushroom/Mob_Mushroom.prefab",
            EnemyRoot + "/Mobs/FlyingEye/Mob_FlyingEye.prefab",
            EnemyRoot + "/Mobs/Skeleton/Mob_Skeleton.prefab",
            BossPrefab
        };

        foreach (string path in expectedPrefabs)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new InvalidOperationException("Missing enemy prefab: " + path);

        foreach (MobDefinition mob in NewMobs)
        {
            string path = EnemyRoot + "/Mobs/" + mob.name + "/Mob_" + mob.name + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            MobStateMachine machine = prefab.GetComponent<MobStateMachine>();
            MobSpriteAnimator animator = prefab.GetComponentInChildren<MobSpriteAnimator>(true);
            Enemy_Health health = prefab.GetComponent<Enemy_Health>();
            if (machine == null || animator == null || health == null)
                throw new InvalidOperationException(mob.name + " is missing its saved state, animation, or health component.");
            // Entity_Health resolves the flash with GetComponent, so it only works from the root.
            if (prefab.GetComponent<Entity_VFX>() == null)
                throw new InvalidOperationException(mob.name + " is missing the root Entity_VFX hit flash.");
            bool shouldAttack = mob.name == "FlyingEye";
            if (machine.HasAttackLogic != shouldAttack)
                throw new InvalidOperationException(mob.name + (shouldAttack
                    ? " is missing its designed ranged attack logic."
                    : " unexpectedly contains attack logic."));
            if (!HasFrames(animator.idle) || !HasFrames(animator.move) || !HasFrames(animator.hurt) ||
                !HasFrames(animator.dead) || !HasFrames(animator.attackOne) || !HasFrames(animator.attackTwo))
                throw new InvalidOperationException(mob.name + " has an incomplete animation set.");
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy_Orc.prefab") != null ||
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Boss.prefab") != null)
            throw new InvalidOperationException("Legacy enemy prefab paths still exist.");
    }

    private static bool HasFrames(MobAnimationFrames clip) => clip?.frames != null && clip.frames.Length > 0;

    private static void RemoveImportedDemoContent()
    {
        string[] packageOnlyPaths =
        {
            "Assets/Demo",
            "Assets/Scenes/SampleScene.unity",
            "Assets/Settings/Scenes",
            "Assets/Settings/Renderer2D.asset",
            "Assets/Settings/Lit2DSceneTemplate.scenetemplate",
            "Assets/Settings/UniversalRP.asset",
            "Assets/InputSystem_Actions.inputactions",
            "Assets/DefaultVolumeProfile.asset",
            "Assets/UniversalRenderPipelineGlobalSettings.asset",
            "Assets/Animations/Goblin",
            "Assets/Animations/Mushroom",
            "Assets/Animations/Flying eye",
            "Assets/Animations/Skeleton"
        };
        foreach (string path in packageOnlyPaths)
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                AssetDatabase.DeleteAsset(path);
    }

    private static void MoveAssetIfNeeded(string source, string destination)
    {
        bool sourceExists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(source));
        bool destinationExists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destination));
        if (!sourceExists)
        {
            if (!destinationExists)
                throw new InvalidOperationException($"Cannot find source or destination asset: {source} -> {destination}");
            return;
        }
        if (destinationExists)
            throw new InvalidOperationException($"Both source and destination exist: {source} and {destination}");

        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error))
            throw new InvalidOperationException($"Failed to move {source} to {destination}: {error}");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        int separator = path.LastIndexOf('/');
        string parent = path[..separator];
        string name = path[(separator + 1)..];
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
