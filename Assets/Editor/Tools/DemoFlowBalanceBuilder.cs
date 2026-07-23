#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Applies the current full-demo route and actor/combat scale balance without rebuilding authored maps.
/// </summary>
public static class DemoFlowBalanceBuilder
{
    private const string StartMenuScenePath = "Assets/Scenes/StartMenu.unity";
    private const string StageOneScenePath = "Assets/Scenes/stage1.unity";
    private const string FullMapScenePath = "Assets/Scenes/stage1_full.unity";
    private const string BossScenePath = "Assets/Scenes/stage1 boss.unity";

    private const string HeroPrefabPath = "Assets/Prefab/Hero.prefab";
    private const string OrcPrefabPath = "Assets/Enemy/Mobs/Orc/Mob_Orc.prefab";
    private const string FlyingEyePrefabPath = "Assets/Enemy/Mobs/FlyingEye/Mob_FlyingEye.prefab";
    private const string FlyingEyeProjectilePath = "Assets/Enemy/Mobs/FlyingEye/FlyingEyeProjectile.prefab";
    private const string GoblinPrefabPath = "Assets/Enemy/Mobs/Goblin/Mob_Goblin.prefab";
    private const string MushroomPrefabPath = "Assets/Enemy/Mobs/Mushroom/Mob_Mushroom.prefab";
    private const string SkeletonPrefabPath = "Assets/Enemy/Mobs/Skeleton/Mob_Skeleton.prefab";
    private const string BossPrefabPath = "Assets/Enemy/Bosses/EvilWizard/Boss_EvilWizard.prefab";

    private const float SceneActorScale = 5f;
    private const float PrefabActorScale = 1.25f;
    private const float BossScale = 6.25f;
    private const float HeroAttackRadius = 7.5f;
    private const float OrcAttackRadius = 8.75f;
    private const float FlyingEyeWarningDiameter = 7.5f;
    private const float FlyingEyeProjectileScale = 1.75f;
    private const float FlyingEyeProjectileColliderRadius = 0.5625f;

    private static readonly string[] MobPrefabPaths =
    {
        OrcPrefabPath,
        FlyingEyePrefabPath,
        GoblinPrefabPath,
        MushroomPrefabPath,
        SkeletonPrefabPath
    };

    [MenuItem("Tools/A Thousand Battles Later/Apply Full Demo Flow And Scale Balance")]
    public static void ApplyAll()
    {
        PatchActorPrefabs();
        PatchStartMenuScene();
        PatchGameplayScene(StageOneScenePath);
        PatchGameplayScene(FullMapScenePath);
        PatchGameplayScene(BossScenePath);
        SetBuildSettingsOrder();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateAll();
        Debug.Log("DEMO_FLOW_BALANCE_OK: start/victory route, larger actors, larger hit ranges and 400 HP boss are saved.");
    }

    [MenuItem("Tools/A Thousand Battles Later/Validate Full Demo Flow And Scale Balance")]
    public static void ValidateAll()
    {
        ValidatePrefabScale(HeroPrefabPath, PrefabActorScale);
        foreach (string path in MobPrefabPaths)
            ValidatePrefabScale(path, PrefabActorScale);
        ValidatePrefabScale(BossPrefabPath, BossScale);

        GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(FlyingEyeProjectilePath);
        CircleCollider2D projectileCollider = projectile != null ? projectile.GetComponent<CircleCollider2D>() : null;
        Require(projectile != null && Approximately(projectile.transform.localScale.x, FlyingEyeProjectileScale),
            "Flying Eye projectile prefab scale is not current.");
        Require(projectileCollider != null && Approximately(projectileCollider.radius, FlyingEyeProjectileColliderRadius),
            "Flying Eye projectile collider radius is not current.");

        Scene startMenu = EditorSceneManager.OpenScene(StartMenuScenePath, OpenSceneMode.Single);
        StartMenuController menu = FindInScene<StartMenuController>(startMenu).SingleOrDefault();
        Require(menu != null && menu.TargetSceneName == "stage1_full",
            "START must load stage1_full.");

        ValidateGameplayScene(StageOneScenePath, false);
        ValidateGameplayScene(FullMapScenePath, false);
        ValidateGameplayScene(BossScenePath, true);

        string[] enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path).ToArray();
        Require(enabledScenes.Length >= 3 && enabledScenes[0] == StartMenuScenePath &&
                enabledScenes[1] == FullMapScenePath && enabledScenes[2] == BossScenePath,
            "Build Settings must begin StartMenu -> stage1_full -> stage1 boss.");
        Debug.Log("DEMO_FLOW_BALANCE_VALIDATE_OK: saved assets and scenes match the full-demo balance.");
    }

    private static void PatchActorPrefabs()
    {
        PatchPrefab(HeroPrefabPath, PrefabActorScale, root =>
        {
            Entity_Combat combat = root.GetComponent<Entity_Combat>();
            if (combat != null)
                SetFloat(combat, "targetCheckRad", HeroAttackRadius);
        });

        foreach (string path in MobPrefabPaths)
        {
            PatchPrefab(path, PrefabActorScale, root =>
            {
                if (path == OrcPrefabPath)
                    ConfigureOrc(root);
                FlyingEyeRangedAttack ranged = root.GetComponent<FlyingEyeRangedAttack>();
                if (ranged != null)
                    SetFloat(ranged, "warningDiameter", FlyingEyeWarningDiameter);
            });
        }

        PatchPrefab(FlyingEyeProjectilePath, FlyingEyeProjectileScale, root =>
        {
            CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.radius = FlyingEyeProjectileColliderRadius;
                EditorUtility.SetDirty(collider);
            }
        });

        PatchPrefab(BossPrefabPath, BossScale, root =>
        {
            EnemyHealth health = root.GetComponent<EnemyHealth>();
            if (health != null)
                SetFloat(health, "maximumHealth", CombatBalance.BossMaximumHealth);
            ConfigureBossAttacks(root);
        });
    }

    private static void PatchPrefab(string path, float scale, Action<GameObject> configure)
    {
        Require(File.Exists(path), "Missing prefab: " + path);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.localScale = Vector3.one * scale;
            configure(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PatchStartMenuScene()
    {
        Scene scene = EditorSceneManager.OpenScene(StartMenuScenePath, OpenSceneMode.Single);
        StartMenuController menu = FindInScene<StartMenuController>(scene).SingleOrDefault();
        Require(menu != null, "StartMenu scene is missing StartMenuController.");
        SetString(menu, "targetSceneName", "stage1_full");
        SaveScene(scene, StartMenuScenePath);
    }

    private static void PatchGameplayScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        foreach (Role hero in FindInScene<Role>(scene))
        {
            SetScale(hero.transform, SceneActorScale);
            Entity_Combat combat = hero.GetComponent<Entity_Combat>();
            if (combat != null)
                SetFloat(combat, "targetCheckRad", HeroAttackRadius);
        }

        foreach (Enemy_Health mobHealth in FindInScene<Enemy_Health>(scene))
        {
            SetScale(mobHealth.transform, SceneActorScale);
            ConfigureOrc(mobHealth.gameObject);
            FlyingEyeRangedAttack ranged = mobHealth.GetComponent<FlyingEyeRangedAttack>();
            if (ranged != null)
                SetFloat(ranged, "warningDiameter", FlyingEyeWarningDiameter);
        }

        foreach (EnemyHealth bossHealth in FindInScene<EnemyHealth>(scene))
        {
            SetScale(bossHealth.transform, BossScale);
            SetFloat(bossHealth, "maximumHealth", CombatBalance.BossMaximumHealth);
            SetString(bossHealth, "victoryReturnSceneName", "stage1_full");
            ConfigureBossAttacks(bossHealth.gameObject);
        }

        // Actor bounds changed, so re-save prefab-authored head bubbles at the correct offset.
        NarrativeAudioBuilder.InstallIntoActiveScene();
        SaveScene(scene, path);
    }

    private static void ConfigureOrc(GameObject root)
    {
        Enemy enemy = root.GetComponent<Enemy>();
        Entity_Combat combat = root.GetComponent<Entity_Combat>();
        if (enemy == null || combat == null || combat.AttackMode != EntityAttackMode.ForwardFan)
            return;
        enemy.attackDistance = 8.5f;
        EditorUtility.SetDirty(enemy);
        SetFloat(combat, "targetCheckRad", 4.25f);
        SetFloat(combat, "fanRadius", OrcAttackRadius);
    }

    private static void ConfigureBossAttacks(GameObject root)
    {
        SetFloatIfPresent(root.GetComponent<LaserAttackPattern>(), "laserWidth", 12.5f);
        SetFloatIfPresent(root.GetComponent<TargetCircleAttackPattern>(), "radius", 27.5f);
        SetFloatIfPresent(root.GetComponent<SpinSlashAttackPattern>(), "radius", 17.5f);
        SetFloatIfPresent(root.GetComponent<FanVolleyAttackPattern>(), "projectileRadius", 3f);
        SetFloatIfPresent(root.GetComponent<OrbitBurstAttackPattern>(), "projectileRadius", 2f);
        SetFloatIfPresent(root.GetComponent<CrossStrikeAttackPattern>(), "width", 10f);
    }

    private static void SetBuildSettingsOrder()
    {
        string[] preferred = { StartMenuScenePath, FullMapScenePath, BossScenePath, StageOneScenePath };
        List<EditorBuildSettingsScene> scenes = preferred.Where(File.Exists)
            .Select(path => new EditorBuildSettingsScene(path, true)).ToList();
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
        {
            if (!preferred.Contains(existing.path))
                scenes.Add(existing);
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void ValidateGameplayScene(string path, bool expectBoss)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Role[] heroes = FindInScene<Role>(scene);
        Require(heroes.Length == 1 && Approximately(heroes[0].transform.localScale.x, SceneActorScale),
            path + " must contain one 5x Hero.");
        Entity_Combat heroCombat = heroes[0].GetComponent<Entity_Combat>();
        Require(heroCombat != null && Approximately(heroCombat.AttackRadius, HeroAttackRadius),
            path + " Hero attack radius must be 7.5.");

        foreach (Enemy_Health mob in FindInScene<Enemy_Health>(scene))
            Require(Approximately(mob.transform.localScale.x, SceneActorScale),
                path + " contains a mob that is not 5x.");

        EnemyHealth[] bosses = FindInScene<EnemyHealth>(scene);
        Require(expectBoss ? bosses.Length == 1 : bosses.Length == 0,
            path + " Boss count does not match its role.");
        if (!expectBoss)
            return;

        EnemyHealth boss = bosses[0];
        Require(Approximately(boss.transform.localScale.x, BossScale), "Boss must be 6.25x.");
        Require(Approximately(boss.MaximumHealth, CombatBalance.BossMaximumHealth), "Boss must have 400 HP.");
        Require(boss.VictoryReturnSceneName == "stage1_full", "Victory R must load stage1_full.");
        ValidateFloat(boss.GetComponent<LaserAttackPattern>(), "laserWidth", 12.5f);
        ValidateFloat(boss.GetComponent<TargetCircleAttackPattern>(), "radius", 27.5f);
        ValidateFloat(boss.GetComponent<SpinSlashAttackPattern>(), "radius", 17.5f);
        ValidateFloat(boss.GetComponent<FanVolleyAttackPattern>(), "projectileRadius", 3f);
        ValidateFloat(boss.GetComponent<OrbitBurstAttackPattern>(), "projectileRadius", 2f);
        ValidateFloat(boss.GetComponent<CrossStrikeAttackPattern>(), "width", 10f);
    }

    private static void ValidatePrefabScale(string path, float expected)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Require(prefab != null && Approximately(prefab.transform.localScale.x, expected),
            path + " scale is not " + expected + ".");
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }

    private static void SetScale(Transform target, float scale)
    {
        target.localScale = Vector3.one * scale;
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static void SetFloatIfPresent(Component target, string propertyName, float value)
    {
        if (target != null)
            SetFloat(target, propertyName, value);
    }

    private static void SetFloat(Component target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        Require(property != null, target.GetType().Name + " is missing " + propertyName + ".");
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static void SetString(Component target, string propertyName, string value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        Require(property != null, target.GetType().Name + " is missing " + propertyName + ".");
        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static void ValidateFloat(Component target, string propertyName, float expected)
    {
        Require(target != null, "Boss is missing " + propertyName + " attack component.");
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);
        Require(property != null && Approximately(property.floatValue, expected),
            target.GetType().Name + "." + propertyName + " is not " + expected + ".");
    }

    private static void SaveScene(Scene scene, string path)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene, path), "Failed to save " + path);
    }

    private static bool Approximately(float left, float right)
    {
        return Mathf.Abs(left - right) < 0.001f;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
