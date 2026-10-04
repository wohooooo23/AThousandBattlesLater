using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Preserves prefab/component IDs and authored tuning while migrating the two stage-two species.</summary>
public static class GroundMobAnimatorBuilder
{
    public static readonly string[] Species = { "Mushroom", "Skeleton" };
    private const string OldGuid = "37a9e165e3901e243a092ee69a783365";
    private const string NewGuid = "d19d890ee0914b3593c6fe7426b01a55";
    public static string PrefabPath(string species) => $"Assets/Enemy/Mobs/{species}/Mob_{species}.prefab";
    private static string Folder(string species) => $"Assets/Enemy/Mobs/{species}/Animations";
    private static string ControllerPath(string species) => $"{Folder(species)}/{species}.controller";
    private static AnimationClip Clip(string species, string kind) => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder(species)}/{species}_{kind}.anim");

    [MenuItem("Tools/A Thousand Battles Later/Migrate Mushroom and Skeleton Animator")]
    public static void Migrate()
    {
        foreach (string species in Species) EnsurePrefab(species);
        foreach (string path in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
        {
            string original = File.ReadAllText(path), yaml = original;
            foreach (string species in Species)
            {
                string guid = AssetDatabase.AssetPathToGUID(PrefabPath(species));
                yaml = Regex.Replace(yaml, @"--- !u!114 [\s\S]*?(?=--- !u!|\z)", m =>
                    m.Value.Contains("guid: " + guid) && m.Value.Contains(OldGuid)
                        ? m.Value.Replace(OldGuid, NewGuid).Replace("Assembly-CSharp::MobStateMachine", "Assembly-CSharp::GroundMobController") : m.Value);
                yaml = Regex.Replace(yaml, @"    - target: \{fileID: -?\d+, guid: " + guid +
                    @", type: 3\}\r?\n      propertyPath: (?:flying|hurtDuration|visual)\r?\n      value: [^\r\n]*\r?\n      objectReference: [^\r\n]*\r?\n", "");
            }
            if (yaml == original) continue;
            File.WriteAllText(path, yaml); AssetDatabase.ImportAsset(path.Replace('\\', '/'));
        }
        AssetDatabase.SaveAssets(); ValidateCampaign();
    }

    public static void EnsurePrefab(string species)
    {
        if (!Species.Contains(species)) throw new ArgumentException("Unsupported ground species: " + species);
        string path = PrefabPath(species);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("Required authored prefab missing: " + path);
        EnsureAssets(species, prefab.GetComponentInChildren<MobSpriteAnimator>(true));
        string yaml = File.ReadAllText(path);
        if (yaml.Contains(OldGuid))
        {
            File.WriteAllText(path, yaml.Replace(OldGuid, NewGuid).Replace("Assembly-CSharp::MobStateMachine", "Assembly-CSharp::GroundMobController"));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        var root = PrefabUtility.LoadPrefabContents(path);
        try { ConfigureRoot(root, species); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void EnsureAssets(string species, MobSpriteAnimator legacy)
    {
        if (!AssetDatabase.IsValidFolder(Folder(species))) AssetDatabase.CreateFolder($"Assets/Enemy/Mobs/{species}", "Animations");
        string[] kinds = { "Idle", "Patrol", "Chase", "Attack", "Death" };
        var old = legacy != null ? new[] { legacy.idle, legacy.move, legacy.move, legacy.attackOne, legacy.dead } : null;
        for (int i = 0; i < kinds.Length; i++)
        {
            // Preserve authored frame order and FPS on the first migration; subsequent runs are idempotent.
            if (Clip(species, kinds[i]) != null) continue;
            if (old == null || old[i].frames.Length == 0) throw new InvalidOperationException(species + " has no source " + kinds[i]);
            var source = old[i]; var clip = new AnimationClip { name = species + "_" + kinds[i], frameRate = source.framesPerSecond };
            var keys = source.frames.Select((sprite, index) => new ObjectReferenceKeyframe { time = index / source.framesPerSecond, value = sprite }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = i < 3; settings.stopTime = source.frames.Length / source.framesPerSecond;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (i == 4) AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent {
                time = settings.stopTime, functionName = "GroundMobStateComplete", stringParameter = "Dead" } });
            AssetDatabase.CreateAsset(clip, $"{Folder(species)}/{species}_{kinds[i]}.anim");
        }
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(species)) != null) return;
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath(species));
        foreach (string p in new[] { "idle", "patrol", "chase", "attack", "dead" }) controller.AddParameter(p, AnimatorControllerParameterType.Bool);
        controller.AddParameter("strike", AnimatorControllerParameterType.Int);
        var graph = controller.layers[0].stateMachine;
        // Entry priority: death, combat phases, chase, patrol, idle. Each state exits via parameters.
        var death = Add("Death", "dead", Clip(species, "Death"), -1, true);
        int count = species == "Skeleton" ? 3 : 1;
        for (int i = 1; i <= count; i++) Add("Attack" + i, "attack", Clip(species, "Attack"), i, false);
        Add("Recovery", "attack", Clip(species, "Idle"), 0, false);
        Add("Chase", "chase", Clip(species, "Chase"), -1, false);
        Add("Patrol", "patrol", Clip(species, "Patrol"), -1, false);
        graph.defaultState = Add("Idle", "idle", Clip(species, "Idle"), -1, false);
        graph.entryPosition = new Vector3(0, 180); graph.exitPosition = new Vector3(650, 180);
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();

        AnimatorState Add(string label, string parameter, AnimationClip clip, int strike, bool terminal)
        {
            var state = graph.AddState(species + "_" + label, new Vector3(300, graph.states.Length * 80));
            state.motion = clip; state.writeDefaultValues = false;
            var entry = graph.AddEntryTransition(state); entry.AddCondition(AnimatorConditionMode.If, 0, parameter);
            if (strike >= 0) entry.AddCondition(AnimatorConditionMode.Equals, strike, "strike");
            if (!terminal)
            {
                Exit(AnimatorConditionMode.IfNot, 0, parameter);
                if (strike >= 0) Exit(AnimatorConditionMode.NotEqual, strike, "strike");
            }
            return state;
            void Exit(AnimatorConditionMode mode, float threshold, string p)
            { var t = state.AddExitTransition(); t.duration = 0; t.hasExitTime = false; t.AddCondition(mode, threshold, p); }
        }
    }
    public static void ConfigureRoot(GameObject root, string species)
    {
        var visual = root.transform.Find("Visual");
        if (visual.GetComponent<MobSpriteAnimator>() is { } legacy) UnityEngine.Object.DestroyImmediate(legacy);
        if (root.GetComponent<MobStateMachine>() != null) throw new InvalidOperationException("Migrate saved prefab before configuring instances.");
        var animator = visual.GetComponent<Animator>();
        if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(species));
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (visual.GetComponent<Entity_AniamtionTriggers>() == null) visual.gameObject.AddComponent<Entity_AniamtionTriggers>();
        var controller = root.GetComponent<GroundMobController>();
        if (controller == null) controller = root.AddComponent<GroundMobController>();
        var data = new SerializedObject(controller);
        data.FindProperty("spriteRenderer").objectReferenceValue = visual.GetComponent<SpriteRenderer>();
        data.FindProperty("attackBehaviour").objectReferenceValue = root.GetComponent<MobAttackBehaviour>();
        data.FindProperty("attackClip").objectReferenceValue = Clip(species, "Attack");
        data.FindProperty("deathClip").objectReferenceValue = Clip(species, "Death");
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void ValidateRoot(GameObject root)
    {
        var mob = root.GetComponent<GroundMobController>();
        string species = root.GetComponent<MushroomPoisonAttack>() != null ? "Mushroom" : "Skeleton";
        var animator = root.transform.Find("Visual")?.GetComponent<Animator>();
        if (mob == null || mob.AttackBehaviour == null || root.GetComponent<MobStateMachine>() != null ||
            root.GetComponentInChildren<MobSpriteAnimator>(true) != null || animator == null ||
            animator.runtimeAnimatorController != AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(species)) ||
            animator.GetComponent<Entity_AniamtionTriggers>() == null)
            throw new InvalidOperationException(root.name + " has incomplete ground Animator references.");
        var data = new SerializedObject(mob);
        foreach (string field in new[] { "spriteRenderer", "attackBehaviour", "attackClip", "deathClip" })
            if (data.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException(root.name + " missing " + field);
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        var graph = controller.layers[0].stateMachine;
        if (controller.parameters.Length != 6 || !controller.parameters.Any(p => p.name == "strike" && p.type == AnimatorControllerParameterType.Int) ||
            new[] { "idle", "patrol", "chase", "attack", "dead" }.Any(name => !controller.parameters.Any(p => p.name == name && p.type == AnimatorControllerParameterType.Bool)) ||
            graph.anyStateTransitions.Length != 0 || graph.states.Length != (species == "Skeleton" ? 8 : 6) ||
            graph.entryTransitions.Length != graph.states.Length || graph.defaultState.name != species + "_Idle" ||
            graph.entryTransitions[0].destinationState.name != species + "_Death")
            throw new InvalidOperationException(species + " has invalid Entry/state graph.");
        foreach (var child in graph.states)
        {
            var state = child.state; var clip = state.motion as AnimationClip;
            if (clip == null || clip.length <= 0 || AnimationUtility.GetCurveBindings(clip).Length > 0)
                throw new InvalidOperationException(species + " must only animate the Visual sprite.");
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length != 1 || bindings[0].path != "" || bindings[0].type != typeof(SpriteRenderer) || bindings[0].propertyName != "m_Sprite" ||
                AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]).Any(k => k.value == null))
                throw new InvalidOperationException(species + " has missing or invalid sprite bindings.");
            if (state.name.EndsWith("Death") ? state.transitions.Length != 0 :
                state.transitions.Length == 0 || state.transitions.Any(t => !t.isExit || t.duration != 0 || t.hasExitTime))
                throw new InvalidOperationException(species + " has invalid Exit conditions.");
        }
        var death = Clip(species, "Death");
        if (death.events.Count(e => e.functionName == "GroundMobStateComplete" && e.stringParameter == "Dead" && Mathf.Abs(e.time - death.length) < .001f) != 1)
            throw new InvalidOperationException(species + " has no authored death completion event.");
    }
    [MenuItem("Tools/A Thousand Battles Later/Validate Mushroom and Skeleton Animator")]
    public static void ValidateCampaign()
    {
        foreach (string species in Species) ValidateRoot(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(species)));
        var setup = EditorSceneManager.GetSceneManagerSetup(); int total = 0;
        try
        {
            foreach (string path in Directory.GetFiles("Assets/Scenes", "*.unity", SearchOption.AllDirectories))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single); int count = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var mob in root.GetComponentsInChildren<MobAttackBehaviour>(true))
                        if (mob is MushroomPoisonAttack or SkeletonTripleSlashAttack) { ValidateRoot(mob.gameObject); count++; }
                total += count;
                if (count > 0) Debug.Log($"[GroundMobAnimator] {path}: {count} migrated ground mobs.");
            }
        }
        finally
        {
            if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        Debug.Log($"[GroundMobAnimator] Controllers, prefabs and all scenes valid: {total} instances.");
    }
}
