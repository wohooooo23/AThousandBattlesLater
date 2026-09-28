using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Authors the eye Animator and upgrades the existing prefab without replacing its objects.</summary>
public static class FlyingEyeAnimatorBuilder
{
    public const string PrefabPath = "Assets/Enemy/Mobs/FlyingEye/Mob_FlyingEye.prefab";
    public const string Folder = "Assets/Enemy/Mobs/FlyingEye/Animations";
    public const string ControllerPath = Folder + "/FlyingEye.controller";
    private const string EyeGuid = "5d932d95a8f44774a8c9c6cb1520f0e3";
    private const string OldGuid = "37a9e165e3901e243a092ee69a783365";
    private const string NewGuid = "4687a975ab714939a8f1d8089e739c3b";
    private static readonly string[] Kinds = { "Idle", "Patrol", "Chase", "Attack", "Hurt", "Death" };
    private static readonly string[] Parameters = { "idle", "patrol", "chase", "attack", "hurt", "dead" };

    [MenuItem("Tools/A Thousand Battles Later/Migrate Flying Eye Animator")]
    public static void MigrateCampaign()
    {
        EnsureAssets();
        EnsurePrefab();
        // Keep the old state component's local ID, so movement overrides continue to target it.
        // Stripped scene references must identify the new script as well.
        foreach (string path in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
        {
            string original = File.ReadAllText(path);
            string migrated = Regex.Replace(original, @"--- !u!114 [\s\S]*?(?=--- !u!|\z)", match =>
                match.Value.Contains("guid: " + EyeGuid) && match.Value.Contains("guid: " + OldGuid)
                    ? match.Value.Replace(OldGuid, NewGuid).Replace("Assembly-CSharp::MobStateMachine", "Assembly-CSharp::FlyingEyeController")
                    : match.Value);
            // Only obsolete eye overrides are removed. Every other authored override stays intact.
            migrated = Regex.Replace(migrated,
                @"    - target: \{fileID: (?:8816576354747114914|2639187856526323005), guid: " + EyeGuid +
                @", type: 3\}\r?\n      propertyPath: (?:flying|hurtDuration|windupDuration|visual)\r?\n      value: [^\r\n]*\r?\n      objectReference: [^\r\n]*\r?\n", "");
            if (original != migrated)
            {
                File.WriteAllText(path, migrated);
                AssetDatabase.ImportAsset(path.Replace('\\', '/'));
            }
        }
        AssetDatabase.SaveAssets();
        ValidateCampaign();
    }

    public static void EnsureAssets()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Enemy/Mobs/FlyingEye", "Animations");
        string[] sheets = { "Flight", "Flight", "Flight", "Attack1", "Take Hit", "Death" };
        float[] fps = { 8f, 12f, 12f, 12f, 12f, 10f };
        AnimationClip[] clips = new AnimationClip[Kinds.Length];
        for (int i = 0; i < Kinds.Length; i++)
        {
            string path = Folder + "/FlyingEye_" + Kinds[i] + ".anim";
            clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath("Assets/Enemy/Mobs/FlyingEye/Sprites/" + sheets[i] + ".png")
                .OfType<Sprite>().OrderBy(sprite => int.Parse(sprite.name[(sprite.name.LastIndexOf('_') + 1)..])).ToArray();
            if (frames.Length != (i < 4 ? 8 : 4))
                throw new InvalidOperationException("Flying Eye sheet has an unexpected frame count: " + sheets[i]);
            AnimationClip clip = clips[i] != null ? clips[i] : new AnimationClip { name = "FlyingEye_" + Kinds[i] };
            clip.frameRate = fps[i];
            ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length];
            for (int frame = 0; frame < keys.Length; frame++)
                keys[frame] = new ObjectReferenceKeyframe { time = frame / fps[i], value = frames[Mathf.Min(frame, frames.Length - 1)] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = i < 3;
            settings.stopTime = frames.Length / fps[i];
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (i >= 3)
            {
                AnimationEvent end = new AnimationEvent { time = frames.Length / fps[i], functionName = "FlyingEyeStateComplete",
                    stringParameter = i == 5 ? "Dead" : Kinds[i] };
                AnimationUtility.SetAnimationEvents(clip, i == 3
                    ? new[] { new AnimationEvent { time = 6f / 12f, functionName = "FlyingEyeShotRelease" }, end }
                    : new[] { end });
            }
            if (clips[i] == null) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            clips[i] = clip;
        }
        AssetDatabase.SaveAssets();
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            return;
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        foreach (string parameter in Parameters)
            controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
        controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        AnimatorStateMachine graph = controller.layers[0].stateMachine;
        AnimatorState[] states = new AnimatorState[Kinds.Length];
        for (int i = 0; i < Kinds.Length; i++)
        {
            states[i] = graph.AddState("FlyingEye_" + Kinds[i], new Vector3(320, 80 + i * 90));
            states[i].motion = clips[i];
            states[i].writeDefaultValues = false;
            if (i == 3)
            {
                states[i].speedParameterActive = true;
                states[i].speedParameter = "AttackSpeed";
            }
            if (i != 5)
            {
                AnimatorStateTransition exit = states[i].AddExitTransition();
                exit.hasExitTime = false;
                exit.duration = 0f;
                exit.AddCondition(AnimatorConditionMode.IfNot, 0, Parameters[i]);
            }
        }
        graph.defaultState = states[0];
        graph.entryPosition = new Vector3(20, 200);
        graph.exitPosition = new Vector3(660, 200);
        graph.anyStatePosition = new Vector3(20, 20);
        for (int i = Kinds.Length - 1; i >= 0; i--)
            graph.AddEntryTransition(states[i]).AddCondition(AnimatorConditionMode.If, 0, Parameters[i]);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    public static void EnsurePrefab()
    {
        EnsureAssets();
        if (!File.Exists(PrefabPath))
        {
            GameObject root = new GameObject("Mob_FlyingEye");
            try
            {
                root.transform.localScale = Vector3.one * 5f;
                root.layer = 8;
                Rigidbody2D body = root.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                root.AddComponent<CapsuleCollider2D>().size = new Vector2(0.82f, 0.81f);
                root.AddComponent<Enemy_Health>();
                GameObject visual = new GameObject("Visual", typeof(SpriteRenderer));
                visual.transform.SetParent(root.transform, false);
                Entity_VFX vfx = root.AddComponent<Entity_VFX>();
                SerializedObject data = new SerializedObject(vfx);
                data.FindProperty("targetRenderer").objectReferenceValue = visual.GetComponent<SpriteRenderer>();
                data.FindProperty("onDamageMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/OnDamage_Material.mat");
                data.ApplyModifiedPropertiesWithoutUndo();
                ConfigureRoot(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return;
        }
        string yaml = File.ReadAllText(PrefabPath);
        if (yaml.Contains(OldGuid))
        {
            File.WriteAllText(PrefabPath, yaml.Replace(OldGuid, NewGuid).Replace("Assembly-CSharp::MobStateMachine", "Assembly-CSharp::FlyingEyeController").Replace("visual: {fileID: 1286609966554763649}", "visual: {fileID: 6228802286591639487}"));
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        }
        GameObject existing = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ConfigureRoot(existing);
            PrefabUtility.SaveAsPrefabAsset(existing, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(existing); }
    }

    public static void ConfigureRoot(GameObject root)
    {
        SpriteRenderer renderer = root.transform.Find("Visual").GetComponent<SpriteRenderer>();
        if (renderer.GetComponent<MobSpriteAnimator>() is { } oldAnimator)
            UnityEngine.Object.DestroyImmediate(oldAnimator);
        if (root.GetComponent<MobStateMachine>() != null)
            throw new InvalidOperationException("Migrate the saved eye prefab before configuring instances, to preserve state component overrides.");
        Animator animator = renderer.GetComponent<Animator>();
        if (animator == null) animator = renderer.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (renderer.GetComponent<Entity_AniamtionTriggers>() == null)
            renderer.gameObject.AddComponent<Entity_AniamtionTriggers>();
        FlyingEyeController eye = root.GetComponent<FlyingEyeController>();
        if (eye == null) eye = root.AddComponent<FlyingEyeController>();
        FlyingEyeRangedAttack ranged = root.GetComponent<FlyingEyeRangedAttack>();
        if (ranged == null) ranged = root.AddComponent<FlyingEyeRangedAttack>();
        SerializedObject attack = new SerializedObject(ranged);
        if (attack.FindProperty("projectilePrefab").objectReferenceValue == null)
            attack.FindProperty("projectilePrefab").objectReferenceValue = EnemyContentBuilder.EnsureFlyingEyeProjectile();
        attack.ApplyModifiedPropertiesWithoutUndo();
        SerializedObject data = new SerializedObject(eye);
        data.FindProperty("visual").objectReferenceValue = renderer;
        data.FindProperty("attackBehaviour").objectReferenceValue = ranged;
        foreach (string kind in new[] { "Attack", "Hurt", "Death" })
            data.FindProperty(char.ToLowerInvariant(kind[0]) + kind[1..] + "Clip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/FlyingEye_" + kind + ".anim");
        data.ApplyModifiedPropertiesWithoutUndo();
        if (renderer.sprite == null)
            renderer.sprite = (Sprite)AnimationUtility.GetObjectReferenceCurve((AnimationClip)((AnimatorController)animator.runtimeAnimatorController).layers[0].stateMachine.defaultState.motion,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"))[0].value;
    }

    public static void ValidateRoot(GameObject root)
    {
        FlyingEyeController eye = root.GetComponent<FlyingEyeController>();
        Animator animator = root.transform.Find("Visual")?.GetComponent<Animator>();
        if (eye == null || root.GetComponent<MobStateMachine>() != null || root.GetComponentInChildren<MobSpriteAnimator>(true) != null ||
            animator == null || animator.runtimeAnimatorController != AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) ||
            animator.GetComponent<Entity_AniamtionTriggers>() == null || eye.AttackBehaviour == null || eye.AttackBehaviour.ProjectilePrefab == null ||
            root.GetComponent<Enemy_Health>() == null || root.GetComponent<Entity_VFX>() == null ||
            root.GetComponent<Rigidbody2D>() == null || root.GetComponent<Collider2D>() == null)
            throw new InvalidOperationException(root.name + " has an incomplete Flying Eye Animator migration.");
        SerializedObject data = new SerializedObject(eye);
        foreach (string field in new[] { "visual", "attackClip", "hurtClip", "deathClip" })
            if (data.FindProperty(field).objectReferenceValue == null)
                throw new InvalidOperationException(root.name + " has a missing " + field);
        AnimatorController controller = (AnimatorController)animator.runtimeAnimatorController;
        AnimatorStateMachine graph = controller.layers[0].stateMachine;
        if (controller.parameters.Length != 7 || !controller.parameters.Any(p => p.name == "AttackSpeed" && p.type == AnimatorControllerParameterType.Float && p.defaultFloat == 1f))
            throw new InvalidOperationException("Flying Eye must define six bools and AttackSpeed.");
        if (graph.states.Length != 6 || graph.defaultState.name != "FlyingEye_Idle" || graph.entryTransitions.Length != 6 || graph.anyStateTransitions.Length != 0)
            throw new InvalidOperationException("Flying Eye must use six Entry/Exit states and default Idle.");
        for (int i = 0; i < 6; i++)
        {
            if (!controller.parameters.Any(p => p.name == Parameters[i] && p.type == AnimatorControllerParameterType.Bool) ||
                graph.entryTransitions[5-i].destinationState.name != "FlyingEye_" + Kinds[i] ||
                graph.entryTransitions[5-i].conditions.Single().parameter != Parameters[i])
                throw new InvalidOperationException("Flying Eye Entry priority/parameters are invalid.");
            AnimatorState state = graph.states.Single(s => s.state.name == "FlyingEye_" + Kinds[i]).state;
            if (i == 5 ? state.transitions.Length != 0 : state.transitions.Length != 1 || !state.transitions[0].isExit ||
                state.transitions[0].hasExitTime || state.transitions[0].duration != 0f ||
                state.transitions[0].conditions.Single().mode != AnimatorConditionMode.IfNot || state.transitions[0].conditions[0].parameter != Parameters[i])
                throw new InvalidOperationException("Flying Eye Exit conditions are invalid.");
            AnimationClip clip = state.motion as AnimationClip;
            float fps = i == 0 ? 8f : i == 5 ? 10f : 12f;
            int frameCount = i < 4 ? 8 : 4;
            if (clip == null || clip.frameRate != fps || Mathf.Abs(clip.length - frameCount / fps) > .001f ||
                AnimationUtility.GetAnimationClipSettings(clip).loopTime != (i < 3))
                throw new InvalidOperationException("Flying Eye clip FPS/duration/loop settings are invalid.");
            if (i >= 3 && data.FindProperty(char.ToLowerInvariant(Kinds[i][0]) + Kinds[i][1..] + "Clip").objectReferenceValue != clip)
                throw new InvalidOperationException("Flying Eye watchdog clip differs from its Controller motion.");
            EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length != 1 || bindings[0].path != "" || bindings[0].type != typeof(SpriteRenderer) ||
                bindings[0].propertyName != "m_Sprite" || AnimationUtility.GetCurveBindings(clip).Length != 0)
                throw new InvalidOperationException("Flying Eye clips must only animate their Visual sprite.");
            ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            if (keys.Length != frameCount || keys.Any(key => key.value == null))
                throw new InvalidOperationException("Flying Eye sprite frames are missing.");
            for (int frame = 0; frame < keys.Length; frame++)
                if (Mathf.Abs(keys[frame].time - frame / fps) > .001f)
                    throw new InvalidOperationException("Flying Eye sprite frames are mistimed.");
            if (clip.events.Length != (i < 3 ? 0 : i == 3 ? 2 : 1))
                throw new InvalidOperationException("Flying Eye has unexpected animation events.");
            if (i >= 3 && !clip.events.Any(e => e.functionName == "FlyingEyeStateComplete" &&
                e.stringParameter == (i == 5 ? "Dead" : Kinds[i]) && Mathf.Abs(e.time - clip.length) < .001f))
                throw new InvalidOperationException("Missing Flying Eye completion event.");
            if (i == 3 && (!state.speedParameterActive || state.speedParameter != "AttackSpeed" ||
                clip.events.Count(e => e.functionName == "FlyingEyeShotRelease" && Mathf.Abs(e.time - .5f) < .001f) != 1))
                throw new InvalidOperationException("Flying Eye release must occur once on frame seven.");
        }
    }

    [MenuItem("Tools/A Thousand Battles Later/Validate Flying Eye Animator")]
    public static void ValidateCampaign()
    {
        ValidateRoot(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int eyes = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                            throw new InvalidOperationException(path + " has a missing script on " + child.name);
                        if (child.GetComponent<FlyingEyeController>() != null)
                        {
                            ValidateRoot(child.gameObject);
                            eyes++;
                        }
                    }
                }
                if (path.Replace('\\','/') is "Assets/Scenes/stage1_full.unity" or "Assets/Scenes/stage2_full.unity" || eyes > 0)
                    Debug.Log("[FlyingEyeAnimator] Loaded " + path + ": " + eyes + " migrated eyes, references valid.");
            }
        }
        finally
        {
            if (setup.Any(scene => scene.isLoaded && scene.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        Debug.Log("[FlyingEyeAnimator] Prefab, campaign and legacy scenes validated.");
    }
}
