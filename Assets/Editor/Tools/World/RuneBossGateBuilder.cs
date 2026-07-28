using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Saves the two rune-key rules, their chest contents and minimap markers directly into the two
/// full-stage scenes. The operation is idempotent and deliberately never writes chest transforms,
/// so hand-authored positions in the Scene view remain authoritative.
/// </summary>
public static class RuneBossGateBuilder
{
    private const string Stage1Path = "Assets/Scenes/stage1_full.unity";
    private const string Stage2Path = "Assets/Scenes/stage2_full.unity";
    private const string SupplyChest = "Supply Treasure Chest";
    private const string LeftChest = "Double Jump Treasure Chest";
    private const string RightChest = "Dash Treasure Chest";
    private const string MarkerPrefix = "Chest Marker - ";
    private const float KeyMarkerScaleMultiplier = 1.35f;

    private static readonly Color RedMarker = new Color(1f, 0.08f, 0.08f, 1f);
    private static readonly Color GreenMarker = new Color(0.12f, 1f, 0.24f, 1f);

    [MenuItem("Tools/A Thousand Battles Later/Build Rune Boss Gates")]
    public static void Build()
    {
        ConfigureStage1();
        ConfigureStage2();
        AssetDatabase.SaveAssets();
        Debug.Log("RUNE_GATE_BUILD_OK: stage1 requires the worn red rune; stage2 requires the worn green rune.");
    }

    [MenuItem("Tools/A Thousand Battles Later/Validate Rune Boss Gates")]
    public static void Validate()
    {
        ValidateStage(Stage1Path, ItemType.Accessory, "You need to equip the Red Rune.",
            SupplyChest, RedMarker, new Dictionary<string, string[]>
            {
                { SupplyChest, new[] { EquipmentBuilder.GemPickupPath, EquipmentBuilder.HealthPotionPickupPath,
                    KunaiInventoryBuilder.PickupPath } },
                { LeftChest, null },
                { RightChest, null }
            });

        ValidateStage(Stage2Path, ItemType.GreenRune, "You need to equip the Green Rune.",
            RightChest, GreenMarker, new Dictionary<string, string[]>
            {
                { SupplyChest, Array.Empty<string>() },
                { LeftChest, new[] { EquipmentBuilder.HealthPotionPickupPath, KunaiInventoryBuilder.PickupPath,
                    EquipmentBuilder.SwordPickupPath } },
                { RightChest, new[] { EquipmentBuilder.GreenRunePickupPath, EquipmentBuilder.ShieldPickupPath } }
            });
        ValidateStage2RecoveryOrbs();
        Debug.Log("RUNE_GATE_VALIDATE_OK: both equipped-rune gates, stage-specific drops and key markers are saved.");
    }

    private static void ConfigureStage1()
    {
        Scene scene = EditorSceneManager.OpenScene(Stage1Path, OpenSceneMode.Single);
        ConfigureGate(scene, ItemType.Accessory, "You need to equip the Red Rune.");
        TreasureChest2D supply = RequireChest(scene, SupplyChest);
        SetDrops(supply, EquipmentBuilder.GemPickupPath, EquipmentBuilder.HealthPotionPickupPath,
            KunaiInventoryBuilder.PickupPath);
        ConfigureKeyMarker(scene, SupplyChest, RedMarker);
        Save(scene, Stage1Path);
    }

    private static void ConfigureStage2()
    {
        Scene scene = EditorSceneManager.OpenScene(Stage2Path, OpenSceneMode.Single);
        ConfigureGate(scene, ItemType.GreenRune, "You need to equip the Green Rune.");

        TreasureChest2D upper = FindInScene<TreasureChest2D>(scene)
            .SingleOrDefault(chest => chest.name == SupplyChest);
        if (upper != null)
            UnityEngine.Object.DestroyImmediate(upper.gameObject);
        DestroyNamed(scene, MarkerPrefix + SupplyChest);

        TreasureChest2D left = RequireChest(scene, LeftChest);
        TreasureChest2D right = RequireChest(scene, RightChest);
        SetDrops(left, EquipmentBuilder.HealthPotionPickupPath, KunaiInventoryBuilder.PickupPath,
            EquipmentBuilder.SwordPickupPath);
        SetDrops(right, EquipmentBuilder.GreenRunePickupPath, EquipmentBuilder.ShieldPickupPath);
        ConfigureRecoveryOrb(scene, AbilityUnlockKind.DoubleJump, left);
        ConfigureRecoveryOrb(scene, AbilityUnlockKind.Dash, right);
        ConfigureKeyMarker(scene, RightChest, GreenMarker);
        Save(scene, Stage2Path);
    }

    private static void ConfigureRecoveryOrb(Scene scene, AbilityUnlockKind ability, TreasureChest2D chest)
    {
        AbilityUnlockOrb2D orb = FindInScene<AbilityUnlockOrb2D>(scene)
            .SingleOrDefault(candidate => candidate.Ability == ability) ??
            throw new MissingReferenceException(scene.path + " is missing its " + ability + " recovery orb.");
        SerializedObject data = new SerializedObject(orb);
        data.FindProperty("ability").enumValueIndex = (int)ability;
        data.FindProperty("sourceChest").objectReferenceValue = chest;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(orb);
    }

    private static void ValidateStage2RecoveryOrbs()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != Stage2Path)
            scene = EditorSceneManager.OpenScene(Stage2Path, OpenSceneMode.Single);
        AbilityUnlockOrb2D[] orbs = FindInScene<AbilityUnlockOrb2D>(scene).ToArray();
        if (orbs.Length != 2)
            throw new InvalidOperationException("stage2 must contain exactly the Double Jump and Dash recovery orbs.");
        TreasureChest2D left = RequireChest(scene, LeftChest);
        TreasureChest2D right = RequireChest(scene, RightChest);
        if (orbs.SingleOrDefault(orb => orb.Ability == AbilityUnlockKind.DoubleJump)?.SourceChest != left ||
            orbs.SingleOrDefault(orb => orb.Ability == AbilityUnlockKind.Dash)?.SourceChest != right)
            throw new InvalidOperationException("stage2 recovery orbs are linked to the wrong treasure chests.");
    }

    private static void ConfigureGate(Scene scene, ItemType slot, string message)
    {
        BossArenaController gate = FindInScene<BossArenaController>(scene).SingleOrDefault();
        if (gate == null)
            throw new MissingReferenceException(scene.path + " requires exactly one BossArenaController.");

        SerializedObject data = new SerializedObject(gate);
        data.FindProperty("requiresEquippedRune").boolValue = true;
        data.FindProperty("requiredRuneSlot").enumValueIndex = (int)slot;
        data.FindProperty("missingRuneMessage").stringValue = message;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gate);
    }

    private static void SetDrops(TreasureChest2D chest, params string[] prefabPaths)
    {
        SerializedObject data = new SerializedObject(chest);
        SerializedProperty drops = data.FindProperty("itemPrefabs");
        drops.arraySize = prefabPaths.Length;
        for (int i = 0; i < prefabPaths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
            if (prefab == null || prefab.GetComponent<ItemPickup>() == null)
                throw new MissingReferenceException("Missing ItemPickup prefab: " + prefabPaths[i]);
            drops.GetArrayElementAtIndex(i).objectReferenceValue = prefab;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(chest);
        EditorUtility.SetDirty(chest);
    }

    private static void ConfigureKeyMarker(Scene scene, string chestName, Color color)
    {
        TreasureChest2D chest = RequireChest(scene, chestName);
        GameObject marker = FindNamed(scene, MarkerPrefix + chestName) ??
            throw new MissingReferenceException(scene.path + " is missing the minimap marker for " + chestName + ".");
        SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>() ??
            throw new MissingReferenceException(marker.name + " needs a SpriteRenderer.");

        float ordinaryScale = FindInScene<SpriteRenderer>(scene)
            .Where(candidate => candidate.name.StartsWith(MarkerPrefix, StringComparison.Ordinal) && candidate != renderer)
            .Select(candidate => candidate.transform.localScale.x)
            .Where(scale => scale > 0.01f)
            .DefaultIfEmpty(marker.transform.localScale.x / KeyMarkerScaleMultiplier)
            .Min();

        marker.transform.position = new Vector3(chest.transform.position.x, chest.transform.position.y, marker.transform.position.z);
        marker.transform.localScale = Vector3.one * ordinaryScale * KeyMarkerScaleMultiplier;
        renderer.color = color;
        EditorUtility.SetDirty(marker.transform);
        EditorUtility.SetDirty(renderer);
    }

    private static void ValidateStage(string path, ItemType slot, string message, string keyChestName,
        Color markerColor, IReadOnlyDictionary<string, string[]> chestRules)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        BossArenaController gate = FindInScene<BossArenaController>(scene).SingleOrDefault();
        if (gate == null || !gate.RequiresEquippedRune || gate.RequiredRuneSlot != slot ||
            gate.MissingRuneMessage != message)
            throw new InvalidOperationException(path + " has the wrong equipped-rune gate configuration.");

        foreach (KeyValuePair<string, string[]> rule in chestRules)
        {
            TreasureChest2D chest = FindInScene<TreasureChest2D>(scene)
                .SingleOrDefault(candidate => candidate.name == rule.Key);
            if (rule.Value != null && rule.Value.Length == 0)
            {
                if (chest != null || FindNamed(scene, MarkerPrefix + rule.Key) != null)
                    throw new InvalidOperationException(path + " must not retain " + rule.Key + " or its marker.");
                continue;
            }
            if (rule.Value == null)
                continue;
            if (chest == null || chest.ConfiguredDropCount != rule.Value.Length)
                throw new InvalidOperationException(path + " has the wrong contents for " + rule.Key + ".");
            for (int i = 0; i < rule.Value.Length; i++)
                if (AssetDatabase.GetAssetPath(chest.GetConfiguredDrop(i)) != rule.Value[i])
                    throw new InvalidOperationException(path + " has the wrong drop at " + rule.Key + " index " + i + ".");
        }

        TreasureChest2D keyChest = RequireChest(scene, keyChestName);
        GameObject keyMarker = FindNamed(scene, MarkerPrefix + keyChestName);
        SpriteRenderer keyRenderer = keyMarker != null ? keyMarker.GetComponent<SpriteRenderer>() : null;
        float largestOtherMarker = FindInScene<SpriteRenderer>(scene)
            .Where(candidate => candidate.name.StartsWith(MarkerPrefix, StringComparison.Ordinal) && candidate != keyRenderer)
            .Select(candidate => candidate.transform.localScale.x).DefaultIfEmpty(0f).Max();
        if (keyRenderer == null || !Approximately(keyRenderer.color, markerColor) ||
            keyMarker.transform.localScale.x <= largestOtherMarker ||
            Vector2.Distance(keyMarker.transform.position, keyChest.transform.position) > 0.01f)
            throw new InvalidOperationException(path + " has an invalid rune chest minimap marker.");
    }

    private static TreasureChest2D RequireChest(Scene scene, string name)
    {
        return FindInScene<TreasureChest2D>(scene).SingleOrDefault(chest => chest.name == name) ??
            throw new MissingReferenceException(scene.path + " is missing " + name + ".");
    }

    private static void DestroyNamed(Scene scene, string name)
    {
        GameObject target = FindNamed(scene, name);
        if (target != null)
            UnityEngine.Object.DestroyImmediate(target);
    }

    private static GameObject FindNamed(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == name);
            if (match != null)
                return match.gameObject;
        }
        return null;
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f &&
               Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
    }

    private static void Save(Scene scene, string path)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, path))
            throw new InvalidOperationException("Failed to save " + path);
    }
}
