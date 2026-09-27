#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attaches the Evil Wizard 2 sprite pack to Boss.prefab: adds a "WizardVisual" child with a
/// SpriteRenderer + Animator (driven by the shared Wizard controller), adds the
/// BossStateMachine, and assigns each existing skill a cast animation. Idempotent — re-running
/// rebuilds the visual child and re-applies the wiring.
/// </summary>
public static class BossWizardBuilder
{
    private const string BossPrefabPath = "Assets/Enemy/Bosses/EvilWizard/Boss_EvilWizard.prefab";
    private const string SpriteFolder = "Assets/Enemy/Bosses/EvilWizard/Visual/Sprites/";
    private const string AnimatorControllerPath = "Assets/Animations/Boss/Wizard.controller";
    private const string VisualName = "WizardVisual";
    private const string BossScenePath = "Assets/Scenes/Legacy/stage1 boss.unity";
    private const string OrcPrefabPath = "Assets/Enemy/Mobs/Orc/Mob_Orc.prefab";
    private const string OnDamageMaterialPath = "Assets/Material/OnDamage_Material.mat";
    private const float TargetHeight = 3.2f;   // local height; the 6.25x Boss root produces the enlarged world model

    [MenuItem("Tools/Boss/Attach Evil Wizard Model")]
    public static void AttachWizard()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
        try
        {
            // The imported wizard is the sole Boss model. The root MeshRenderer/MeshFilter
            // belonged to the old blue UnitCircle prototype and must never coexist with it.
            MeshRenderer placeholderRenderer = root.GetComponent<MeshRenderer>();
            if (placeholderRenderer != null)
                Object.DestroyImmediate(placeholderRenderer);
            MeshFilter placeholderFilter = root.GetComponent<MeshFilter>();
            if (placeholderFilter != null)
                Object.DestroyImmediate(placeholderFilter);

            // ---- visual child (rebuild if present) ----
            Transform existing = root.transform.Find(VisualName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            GameObject visual = new GameObject(VisualName, typeof(SpriteRenderer), typeof(Animator));
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;

            Animator anim = visual.GetComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
            if (anim.runtimeAnimatorController == null)
                throw new MissingReferenceException("Missing " + AnimatorControllerPath);
            Sprite[] idleFrames = LoadFrames("Idle");

            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;
            if (idleFrames.Length > 0)
            {
                renderer.sprite = idleFrames[0];
                float spriteHeight = idleFrames[0].bounds.size.y;
                float scale = spriteHeight > 0.01f ? TargetHeight / spriteHeight : 1f;
                visual.transform.localScale = new Vector3(scale, scale, 1f);
            }

            // ---- state machine on the boss root ----
            BossStateMachine stateMachine = root.GetComponent<BossStateMachine>();
            if (stateMachine == null)
                stateMachine = root.AddComponent<BossStateMachine>();
            AssignAnimator(stateMachine, anim, renderer);

            // Hit flash + every-3-attacks blink, on the root so EnemyHealth/EnemyAttackController resolve them.
            AttachHitFlashAndTeleport(root, renderer);

            // ---- each skill declares its cast animation ----
            // Ranged / spell skills raise the staff (Attack1); melee skills slash (Attack2).
            SetCastAnim(root, typeof(LaserAttackPattern), CastAnimation.Attack1);
            SetCastAnim(root, typeof(FanVolleyAttackPattern), CastAnimation.Attack1);
            SetCastAnim(root, typeof(TargetCircleAttackPattern), CastAnimation.Attack1);
            SetCastAnim(root, typeof(OrbitBurstAttackPattern), CastAnimation.Attack1);
            SetCastAnim(root, typeof(SpinSlashAttackPattern), CastAnimation.Attack2);
            SetCastAnim(root, typeof(CrossStrikeAttackPattern), CastAnimation.Attack2);

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            Debug.Log("<color=lime>BOSS_WIZARD_OK: Evil Wizard model + state machine attached to Boss.prefab. " +
                      "Tune WizardVisual scale/Y in the prefab if the sprite sits too high/low.</color>");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Boss/Repair Wizard Visual And Orc Rewards")]
    public static void RepairWizardVisualAndOrcRewards()
    {
        AttachWizard();
        ResaveOrcPrefab();

        Scene scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Single);
        EnemyHealth bossHealth = Object.FindFirstObjectByType<EnemyHealth>(FindObjectsInactive.Include);
        if (bossHealth == null)
            throw new MissingReferenceException(BossScenePath + " is missing EnemyHealth.");

        GameObject boss = bossHealth.gameObject;
        MeshRenderer scenePlaceholderRenderer = boss.GetComponent<MeshRenderer>();
        if (scenePlaceholderRenderer != null)
            Object.DestroyImmediate(scenePlaceholderRenderer);
        MeshFilter scenePlaceholderFilter = boss.GetComponent<MeshFilter>();
        if (scenePlaceholderFilter != null)
            Object.DestroyImmediate(scenePlaceholderFilter);

        Transform wizard = boss.transform.Find(VisualName);
        if (wizard == null || wizard.GetComponent<SpriteRenderer>() == null)
            throw new MissingReferenceException("The current Boss scene is missing the Evil Wizard visual.");

        Animator wizardAnimator = wizard.GetComponent<Animator>();
        if (wizardAnimator == null || wizardAnimator.runtimeAnimatorController == null)
            throw new MissingReferenceException("The current Boss scene is missing the Evil Wizard Animator Controller.");

        // The boss-room instance historically removed most components from the prefab and added
        // tuned replacements. That override also removed BossStateMachine, which made the attack
        // controller fall back to its old jitter/shrink pose even though WizardVisual still existed.
        // Store a scene-owned state machine and its visual reference so the fix survives reopening.
        BossStateMachine sceneStateMachine = boss.GetComponent<BossStateMachine>();
        if (sceneStateMachine == null)
            sceneStateMachine = boss.AddComponent<BossStateMachine>();
        AssignAnimator(sceneStateMachine, wizardAnimator, wizard.GetComponent<SpriteRenderer>());
        AttachHitFlashAndTeleport(boss, wizard.GetComponent<SpriteRenderer>());

        SetCastAnim(boss, typeof(LaserAttackPattern), CastAnimation.Attack1);
        SetCastAnim(boss, typeof(FanVolleyAttackPattern), CastAnimation.Attack1);
        SetCastAnim(boss, typeof(TargetCircleAttackPattern), CastAnimation.Attack1);
        SetCastAnim(boss, typeof(OrbitBurstAttackPattern), CastAnimation.Attack1);
        SetCastAnim(boss, typeof(SpinSlashAttackPattern), CastAnimation.Attack2);
        SetCastAnim(boss, typeof(CrossStrikeAttackPattern), CastAnimation.Attack2);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, BossScenePath))
            throw new System.InvalidOperationException("Failed to save " + BossScenePath);

        AssetDatabase.SaveAssets();
        Debug.Log("BOSS_ORC_REPAIR_OK: blue Boss placeholder removed; every Enemy_Health Orc now awards its prefab coinReward.");
    }

    private static void AssignAnimator(BossStateMachine stateMachine, Animator animator, SpriteRenderer renderer)
    {
        SerializedObject serialized = new SerializedObject(stateMachine);
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("visualRenderer").objectReferenceValue = renderer;
        serialized.FindProperty("defaultFacesRight").boolValue = true;
        serialized.FindProperty("compensateOffCenterPivot").boolValue = false;
        serialized.FindProperty("attack1ReleaseFrame").intValue = 5;
        serialized.FindProperty("attack2ReleaseFrame").intValue = 5;
        serialized.FindProperty("attack3ReleaseFrame").intValue = 5;
        serialized.FindProperty("attack1FrameCount").intValue = 8;
        serialized.FindProperty("attack2FrameCount").intValue = 8;
        serialized.FindProperty("attack3FrameCount").intValue = 8;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stateMachine);
    }

    /// <summary>
    /// Adds the hit flash (same material swap the mobs use) and the every-3-attacks blink. Both live
    /// on the Boss root: Entity_VFX is resolved by EnemyHealth via GetComponent, and BossTeleport is
    /// found the same way by EnemyAttackController. Idempotent, so both the prefab build and the
    /// scene repair can call it.
    /// </summary>
    private static void AttachHitFlashAndTeleport(GameObject root, SpriteRenderer visualRenderer)
    {
        Material onDamage = AssetDatabase.LoadAssetAtPath<Material>(OnDamageMaterialPath);
        if (onDamage == null)
            throw new MissingReferenceException("Missing hit-flash material at " + OnDamageMaterialPath + ".");

        Entity_VFX vfx = root.GetComponent<Entity_VFX>();
        if (vfx == null)
            vfx = root.AddComponent<Entity_VFX>();
        SerializedObject vfxData = new SerializedObject(vfx);
        vfxData.FindProperty("targetRenderer").objectReferenceValue = visualRenderer;
        vfxData.FindProperty("onDamageMaterial").objectReferenceValue = onDamage;
        vfxData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(vfx);

        BossTeleport teleport = root.GetComponent<BossTeleport>();
        if (teleport == null)
            teleport = root.AddComponent<BossTeleport>();
        SerializedObject teleportData = new SerializedObject(teleport);
        teleportData.FindProperty("flash").objectReferenceValue = vfx;
        teleportData.FindProperty("attacksPerRelocation").intValue = 3;
        teleportData.FindProperty("relocationMode").enumValueIndex = (int)BossRelocationMode.Blink;
        teleportData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(teleport);
    }

    private static void ResaveOrcPrefab()
    {
        GameObject orc = PrefabUtility.LoadPrefabContents(OrcPrefabPath);
        try
        {
            Enemy_Health health = orc.GetComponent<Enemy_Health>();
            if (health == null)
                throw new MissingReferenceException("Enemy_Orc.prefab is missing Enemy_Health.");
            EditorUtility.SetDirty(health);
            PrefabUtility.SaveAsPrefabAsset(orc, OrcPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(orc);
        }
    }

    private static Sprite[] LoadFrames(string sheet)
    {
        string path = SpriteFolder + sheet + ".png";
        Object[] representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        List<Sprite> frames = new List<Sprite>();
        foreach (Object representation in representations)
            if (representation is Sprite sprite)
                frames.Add(sprite);

        frames.Sort((a, b) => FrameIndex(a.name).CompareTo(FrameIndex(b.name)));
        if (frames.Count == 0)
            Debug.LogWarning("[BossWizard] No sliced sub-sprites at " + path +
                             " — make sure the texture is imported as Sprite Mode: Multiple.");
        return frames.ToArray();
    }

    private static int FrameIndex(string spriteName)
    {
        int underscore = spriteName.LastIndexOf('_');
        if (underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out int index))
            return index;
        return 0;
    }

    private static void SetCastAnim(GameObject boss, System.Type patternType, CastAnimation cast)
    {
        Component pattern = boss.GetComponent(patternType);
        if (pattern == null)
            return;
        SerializedObject serialized = new SerializedObject(pattern);
        SerializedProperty property = serialized.FindProperty("castAnimation");
        if (property != null)
        {
            property.enumValueIndex = (int)cast;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
