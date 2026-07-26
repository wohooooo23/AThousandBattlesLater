#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Authors the fourth equipment category without rebuilding the level: creates the reusable green
/// rune assets, wires the saved paperdoll slot, and saves one drop in stage2's lower-right chest.
/// Existing chest transforms and unrelated prefab overrides are deliberately preserved.
/// </summary>
public static class VerdantRuneBuilder
{
    private const string ScenePath = "Assets/Scenes/stage2_full.unity";
    private const string CanvasPath = "Assets/Prefab/Canvas.prefab";
    private const string RuneChestName = "Dash Treasure Chest";

    [MenuItem("Tools/Inventory/Build Green Rune")]
    public static void Build()
    {
        ConfigureIconImporter();
        EquipmentBuilder.EnsureGreenRunePickup();
        ConfigureCrimsonRuneDescription();
        EquipmentBuilder.WireWearableSlots();
        ConfigureRuneChestDrop();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("GREEN_RUNE_OK: item, fourth equipment slot and stage2 rune chest drop are saved.");
    }

    [MenuItem("Tools/Inventory/Validate Green Rune")]
    public static void Validate()
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(EquipmentBuilder.GreenRuneItemPath);
        if (item == null || item.type != ItemType.GreenRune || item.icon == null || !item.IsEquippable ||
            !item.IsForgeable)
            throw new InvalidOperationException("Green Rune ItemData is missing or incorrectly configured.");
        ItemData crimson = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Prefab/Rune_Crimson.asset");
        if (crimson == null || crimson.type != ItemType.Accessory || !crimson.IsEquippable || crimson.IsForgeable)
            throw new InvalidOperationException("Crimson Rune must remain wearable but cannot be forged.");
        if (!Mathf.Approximately(Role.CrimsonMoveMultiplier, 1.3f) ||
            !Mathf.Approximately(Role.CrimsonJumpMultiplier, 1.3f) ||
            !Mathf.Approximately(Role.CrimsonDashMultiplier, 1.5f))
            throw new InvalidOperationException("Crimson Rune movement multipliers are incorrect.");
        if (!Mathf.Approximately(HeroHealth.GetGreenRuneHps(0), 2f) ||
            !Mathf.Approximately(HeroHealth.GetGreenRuneHps(1), 4f) ||
            !Mathf.Approximately(HeroHealth.GetGreenRuneHps(5), 12f))
            throw new InvalidOperationException("Green Rune HPS progression is incorrect.");

        GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentBuilder.GreenRunePickupPath);
        ItemPickup pickupData = pickup != null ? pickup.GetComponent<ItemPickup>() : null;
        if (pickupData == null || pickupData.itemData != item || pickupData.count != 1 ||
            (pickup.transform.localScale - Vector3.one * 0.5f).sqrMagnitude > 0.0001f)
            throw new InvalidOperationException("Green Rune pickup prefab is missing or incorrectly configured.");

        GameObject canvas = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            Transform slot = FindDeep(canvas.transform, "EquipSlot_L3");
            EquipmentSlotUI wearable = slot != null ? slot.GetComponent<EquipmentSlotUI>() : null;
            if (wearable == null || wearable.slotType != ItemType.GreenRune || wearable.icon == null)
                throw new InvalidOperationException("The fourth paperdoll cell is not the Green Rune equipment slot.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(canvas);
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TreasureChest2D chest = FindInScene<TreasureChest2D>(scene)
            .SingleOrDefault(candidate => candidate.name == RuneChestName);
        if (chest == null || chest.ConfiguredDropCount != 1 || chest.GetConfiguredDrop(0) != pickup)
            throw new InvalidOperationException("Stage2's lower-right chest must contain only one Green Rune drop.");

        // Exercise the real inventory/equipment route without entering Play Mode. The new rune must
        // occupy its dedicated slot and return to the bag when unequipped.
        RunInventory.Reset();
        RunEquipment.Reset();
        try
        {
            RunInventory.Add(item, 1);
            if (!RunEquipment.Equip(item) || RunEquipment.GreenRune != item || RunInventory.Count(item) != 0)
                throw new InvalidOperationException("Green Rune cannot travel from inventory to its equipment slot.");
            if (!RunEquipment.Unequip(ItemType.GreenRune) || RunEquipment.GreenRune != null ||
                RunInventory.Count(item) != 1)
                throw new InvalidOperationException("Green Rune cannot return from its equipment slot to inventory.");
        }
        finally
        {
            RunInventory.Reset();
            RunEquipment.Reset();
        }

        Debug.Log("GREEN_RUNE_VALIDATE_OK.");
    }

    private static void ConfigureCrimsonRuneDescription()
    {
        ItemData crimson = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Prefab/Rune_Crimson.asset");
        if (crimson == null)
            throw new MissingReferenceException("Missing Crimson Rune ItemData.");
        crimson.description =
            "A crimson rune that increases movement and jump speed by 30%, and dash speed by 50%. It cannot be forged.";
        EditorUtility.SetDirty(crimson);
    }

    private static void ConfigureIconImporter()
    {
        AssetDatabase.ImportAsset(EquipmentBuilder.GreenRuneIconPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(EquipmentBuilder.GreenRuneIconPath) as TextureImporter;
        if (importer == null)
            throw new MissingReferenceException("Unable to import " + EquipmentBuilder.GreenRuneIconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void ConfigureRuneChestDrop()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TreasureChest2D chest = FindInScene<TreasureChest2D>(scene)
            .SingleOrDefault(candidate => candidate.name == RuneChestName);
        GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(EquipmentBuilder.GreenRunePickupPath);
        if (chest == null || pickup == null)
            throw new MissingReferenceException("Stage2 rune chest or Green Rune pickup is missing.");

        SerializedObject data = new SerializedObject(chest);
        SerializedProperty drops = data.FindProperty("itemPrefabs");
        drops.arraySize = 1;
        drops.GetArrayElementAtIndex(0).objectReferenceValue = pickup;
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(chest);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException("Failed to save " + ScenePath);
    }

    private static T[] FindInScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static Transform FindDeep(Transform root, string childName)
    {
        if (root.name == childName)
            return root;
        foreach (Transform child in root)
        {
            Transform result = FindDeep(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }
}
#endif
