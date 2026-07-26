#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saves Mushroom/Skeleton combat on their prefabs, then deterministically randomises the copied
/// stage's Orc population into an even split while preserving scene positions and references.
/// </summary>
public static class Stage2MobCombatBuilder
{
    private const string ScenePath = "Assets/Scenes/stage2_full.unity";
    private const string MushroomPath = "Assets/Enemy/Mobs/Mushroom/Mob_Mushroom.prefab";
    private const string SkeletonPath = "Assets/Enemy/Mobs/Skeleton/Mob_Skeleton.prefab";

    [MenuItem("Tools/Stage 2/Build Mushroom and Skeleton Combat")]
    public static void Build()
    {
        ConfigureMushroomPrefab();
        ConfigureSkeletonPrefab();
        ReplaceStage2Orcs();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("STAGE2_MOB_COMBAT_OK: Mushroom poison and Skeleton triple slash saved; stage2 Orcs replaced evenly.");
    }

    private static void ConfigureMushroomPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MushroomPath);
        try
        {
            MobStateMachine machine = Require<MobStateMachine>(root);
            MobSpriteAnimator visual = RequireInChildren<MobSpriteAnimator>(root);
            MushroomPoisonAttack attack = root.GetComponent<MushroomPoisonAttack>() ?? root.AddComponent<MushroomPoisonAttack>();
            SetObject(attack, "visual", visual);
            SetFloat(attack, "radius", 5f);
            SetFloat(attack, "windupDuration", 0.8f);
            SetFloat(attack, "cooldown", 1.35f);
            SetFloat(attack, "slashDamage", CombatBalance.EnemyDamagePerHit);
            SetFloat(attack, "poisonDamage", 5f);
            SetFloat(attack, "poisonDuration", 1f);
            SetObject(machine, "attackBehaviour", attack);
            SetFloat(machine, "detectionRange", 12f);
            PrefabUtility.SaveAsPrefabAsset(root, MushroomPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConfigureSkeletonPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SkeletonPath);
        try
        {
            MobStateMachine machine = Require<MobStateMachine>(root);
            MobSpriteAnimator visual = RequireInChildren<MobSpriteAnimator>(root);
            SkeletonTripleSlashAttack attack = root.GetComponent<SkeletonTripleSlashAttack>() ??
                root.AddComponent<SkeletonTripleSlashAttack>();
            SerializedObject data = new SerializedObject(attack);
            data.FindProperty("visual").objectReferenceValue = visual;
            SerializedProperty radii = data.FindProperty("radii");
            radii.arraySize = 3;
            radii.GetArrayElementAtIndex(0).floatValue = 3.5f;
            radii.GetArrayElementAtIndex(1).floatValue = 5f;
            radii.GetArrayElementAtIndex(2).floatValue = 6.5f;
            data.FindProperty("sectorAngle").floatValue = 105f;
            data.FindProperty("windupPerSlash").floatValue = 0.42f;
            data.FindProperty("intervalBetweenSlashes").floatValue = 0.16f;
            data.FindProperty("cooldown").floatValue = 1.35f;
            data.FindProperty("damage").floatValue = CombatBalance.EnemyDamagePerHit;
            data.ApplyModifiedPropertiesWithoutUndo();
            SetObject(machine, "attackBehaviour", attack);
            SetFloat(machine, "detectionRange", 12f);
            PrefabUtility.SaveAsPrefabAsset(root, SkeletonPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ReplaceStage2Orcs()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        List<Enemy_Orc> orcs = FindInScene<Enemy_Orc>(scene).OrderBy(orc => orc.transform.position.x)
            .ThenBy(orc => orc.transform.position.y).ToList();
        if (orcs.Count == 0)
        {
            RebalanceExistingStage2Mobs(scene);
            return; // Idempotent once the saved distribution is already spatially balanced.
        }

        // This is an editor-only, one-time distribution. Results are saved as concrete prefab
        // instances in the scene and never randomised at runtime. Shuffle inside each named room,
        // then alternate species so neither kind clusters in one part of the map.
        System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
        List<(Enemy_Orc enemy, bool mushroom)> assignments = BuildBalancedAssignments(orcs, random);
        GameObject mushroom = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomPath);
        GameObject skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
        if (mushroom == null || skeleton == null)
            throw new MissingReferenceException("Stage 2 mob prefabs are missing.");

        foreach ((Enemy_Orc oldOrc, bool useMushroom) in assignments)
        {
            GameObject source = useMushroom ? mushroom : skeleton;
            string species = useMushroom ? "Mushroom" : "Skeleton";
            ReplaceCombatant(scene, oldOrc, source, species);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save " + ScenePath);
    }

    private static List<(T enemy, bool mushroom)> BuildBalancedAssignments<T>(IEnumerable<T> enemies,
        System.Random random) where T : Component
    {
        List<IGrouping<string, T>> rooms = enemies.GroupBy(enemy => RoomKey(enemy.name)).ToList();
        int total = rooms.Sum(room => room.Count());
        int mushroomTarget = (total + 1) / 2;
        int baseMushrooms = rooms.Sum(room => room.Count() / 2);
        HashSet<string> roomsReceivingOddExtra = rooms.Where(room => room.Count() % 2 != 0)
            .OrderBy(_ => random.Next()).Take(mushroomTarget - baseMushrooms).Select(room => room.Key).ToHashSet();
        List<(T, bool)> result = new List<(T, bool)>();
        foreach (IGrouping<string, T> room in rooms.OrderBy(_ => random.Next()))
        {
            List<T> shuffled = room.OrderBy(_ => random.Next()).ToList();
            int roomMushrooms = shuffled.Count / 2 + (roomsReceivingOddExtra.Contains(room.Key) ? 1 : 0);
            result.AddRange(shuffled.Select((enemy, index) => (enemy, index < roomMushrooms)));
        }
        return result;
    }

    private static void RebalanceExistingStage2Mobs(Scene scene)
    {
        List<MobAttackBehaviour> current = FindInScene<MobAttackBehaviour>(scene).Where(attack =>
            attack is MushroomPoisonAttack || attack is SkeletonTripleSlashAttack).ToList();
        if (current.Count == 0) return;
        System.Random random = new System.Random(Guid.NewGuid().GetHashCode());
        List<(MobAttackBehaviour enemy, bool mushroom)> assignments = BuildBalancedAssignments(current, random);
        GameObject mushroom = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomPath);
        GameObject skeleton = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
        bool changed = false;
        foreach ((MobAttackBehaviour enemy, bool useMushroom) in assignments)
        {
            bool alreadyCorrect = useMushroom ? enemy is MushroomPoisonAttack : enemy is SkeletonTripleSlashAttack;
            if (alreadyCorrect) continue;
            ReplaceCombatant(scene, enemy, useMushroom ? mushroom : skeleton,
                useMushroom ? "Mushroom" : "Skeleton");
            changed = true;
        }
        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save rebalanced " + ScenePath);
        }
    }

    private static void ReplaceCombatant(Scene scene, Component oldActor, GameObject source, string species)
    {
        Transform oldTransform = oldActor.transform;
        Transform parent = oldTransform.parent;
        int sibling = oldTransform.GetSiblingIndex();
        Vector3 position = oldTransform.position;
        Quaternion rotation = oldTransform.rotation;
        string oldName = oldActor.name;
        Enemy_Health oldHealth = oldActor.GetComponent<Enemy_Health>();
        EnemyHealthBar bar = oldHealth != null
            ? new SerializedObject(oldHealth).FindProperty("worldHealthBar")?.objectReferenceValue as EnemyHealthBar
            : null;

        GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        replacement.transform.SetParent(parent, true);
        replacement.transform.SetPositionAndRotation(position, rotation);
        replacement.transform.SetSiblingIndex(sibling);
        replacement.name = ReplaceSpeciesName(oldName, species);
        Enemy_Health newHealth = Require<Enemy_Health>(replacement);
        if (bar != null)
        {
            bar.transform.SetParent(replacement.transform, true);
            bar.name = ReplaceSpeciesName(bar.name, species);
            SetObject(bar, "followTarget", replacement.transform);
            SetObject(newHealth, "worldHealthBar", bar);
        }
        ReplaceSceneReferences(scene, oldActor.gameObject, replacement, oldHealth, newHealth);
        UnityEngine.Object.DestroyImmediate(oldActor.gameObject);
    }

    private static string ReplaceSpeciesName(string value, string species)
    {
        foreach (string oldSpecies in new[] { "Orc", "Mushroom", "Skeleton" })
        {
            int index = value.IndexOf(oldSpecies, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
                return value.Substring(0, index) + species + value.Substring(index + oldSpecies.Length);
        }
        return value + " " + species;
    }

    private static string RoomKey(string enemyName)
    {
        string[] species = { " Orc", " Mushroom", " Skeleton" };
        int marker = species.Select(label => enemyName.IndexOf(label, StringComparison.OrdinalIgnoreCase))
            .Where(index => index > 0).DefaultIfEmpty(-1).Min();
        return marker > 0 ? enemyName.Substring(0, marker) : enemyName;
    }

    private static void ReplaceSceneReferences(Scene scene, GameObject oldObject, GameObject replacement,
        Enemy_Health oldHealth, Enemy_Health newHealth)
    {
        foreach (MonoBehaviour component in FindInScene<MonoBehaviour>(scene))
        {
            if (component == null || component.transform.IsChildOf(oldObject.transform)) continue;
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                UnityEngine.Object current = property.objectReferenceValue;
                if (current == oldHealth) { property.objectReferenceValue = newHealth; changed = true; }
                else if (current == oldObject) { property.objectReferenceValue = replacement; changed = true; }
                else if (current == oldObject.transform) { property.objectReferenceValue = replacement.transform; changed = true; }
            }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    [MenuItem("Tools/Stage 2/Validate Mushroom and Skeleton Combat")]
    public static void Validate()
    {
        ValidatePrefab<MushroomPoisonAttack>(MushroomPath);
        ValidatePrefab<SkeletonTripleSlashAttack>(SkeletonPath);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int orcs = FindInScene<Enemy_Orc>(scene).Length;
        int mushrooms = FindInScene<MushroomPoisonAttack>(scene).Length;
        int skeletons = FindInScene<SkeletonTripleSlashAttack>(scene).Length;
        if (orcs != 0 || mushrooms == 0 || skeletons == 0 || Mathf.Abs(mushrooms - skeletons) > 1)
            throw new InvalidOperationException($"stage2 split invalid: Orc={orcs}, Mushroom={mushrooms}, Skeleton={skeletons}.");
        IEnumerable<MobAttackBehaviour> stage2Melee = FindInScene<MobAttackBehaviour>(scene).Where(attack =>
            attack is MushroomPoisonAttack || attack is SkeletonTripleSlashAttack);
        foreach (IGrouping<string, MobAttackBehaviour> room in stage2Melee.GroupBy(attack => RoomKey(attack.name)))
        {
            int roomMushrooms = room.Count(attack => attack is MushroomPoisonAttack);
            int roomSkeletons = room.Count(attack => attack is SkeletonTripleSlashAttack);
            if (room.Count() > 1 && Mathf.Abs(roomMushrooms - roomSkeletons) > 1)
                throw new InvalidOperationException($"{room.Key} is spatially unbalanced: {roomMushrooms} Mushroom, {roomSkeletons} Skeleton.");
        }
        foreach (MobStateMachine machine in FindInScene<MobStateMachine>(scene))
            if ((machine.GetComponent<MushroomPoisonAttack>() != null || machine.GetComponent<SkeletonTripleSlashAttack>() != null) &&
                machine.AttackBehaviour == null)
                throw new InvalidOperationException(machine.name + " has an attack component that is not wired into its FSM.");
        foreach (Enemy_Health health in FindInScene<Enemy_Health>(scene).Where(health =>
                     health.GetComponent<MushroomPoisonAttack>() != null || health.GetComponent<SkeletonTripleSlashAttack>() != null))
        {
            EnemyHealthBar bar = new SerializedObject(health).FindProperty("worldHealthBar")?.objectReferenceValue as EnemyHealthBar;
            if (bar == null || bar.FollowTarget != health.transform || !bar.transform.IsChildOf(health.transform))
                throw new InvalidOperationException(health.name + " did not preserve its scene-authored health bar.");
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                throw new InvalidOperationException(child.name + " contains a missing script after replacement.");

        Debug.Log($"STAGE2_MOB_COMBAT_VALIDATE_OK: {mushrooms} Mushroom, {skeletons} Skeleton, 0 Orc.");
    }

    private static void ValidatePrefab<T>(string path) where T : MobAttackBehaviour
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        T attack = prefab != null ? prefab.GetComponent<T>() : null;
        MobStateMachine machine = prefab != null ? prefab.GetComponent<MobStateMachine>() : null;
        if (attack == null || machine == null || machine.AttackBehaviour != attack || attack.AttackRange <= 0f)
            throw new InvalidOperationException(path + " does not contain a saved and wired attack.");
    }

    private static T Require<T>(GameObject root) where T : Component => root.GetComponent<T>() ??
        throw new MissingReferenceException(root.name + " requires " + typeof(T).Name + ".");
    private static T RequireInChildren<T>(GameObject root) where T : Component => root.GetComponentInChildren<T>(true) ??
        throw new MissingReferenceException(root.name + " requires child " + typeof(T).Name + ".");

    private static void SetFloat(UnityEngine.Object target, string field, float value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field) ?? throw new MissingFieldException(target.name, field);
        property.floatValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field) ?? throw new MissingFieldException(target.name, field);
        property.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
}
#endif
