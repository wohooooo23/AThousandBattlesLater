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
/// Authors the stackable Kunai inventory item and pickup. This phase deliberately contains no
/// ranged-attack behaviour: it only connects the imported art to ItemData, RunInventory and the
/// existing treasure-chest drop pipeline.
/// </summary>
public static class KunaiInventoryBuilder
{
    public const string ItemPath = "Assets/Prefab/Kunai.asset";
    public const string PickupPath = "Assets/Prefab/EquipmentPickups/KunaiPickup.prefab";
    public const string IconPath = "Assets/Resources/Sprites/icons/kunai.png";
    public const int StackSize = 16;

    private const string FullMapScenePath = "Assets/Scenes/stage1_full.unity";
    private static readonly string[] ProgressionScenePaths =
    {
        "Assets/Scenes/stage1.unity",
        FullMapScenePath,
        "Assets/Scenes/stage1 boss.unity"
    };

    [MenuItem("Tools/Inventory/Build Kunai Inventory Item")]
    public static void Repair()
    {
        EnsureAssets();
        foreach (string scenePath in ProgressionScenePaths)
            ConfigureScene(scenePath, scenePath == FullMapScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("KUNAI_INVENTORY_OK: fresh-run stack 16 and Supply Chest stack 16 are saved.");
    }

    /// <summary>Creates or updates persistent assets and returns the canonical ItemData.</summary>
    public static ItemData EnsureAssets()
    {
        Sprite icon = EnsureIconImport();
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, ItemPath);
        }
        item.itemName = "Kunai";
        item.description = "Stackable throwing ammunition. Ranged attacks will consume one kunai per shot.";
        item.icon = icon;
        item.type = ItemType.Ammunition;
        item.attackBonus = 0f;
        item.defenseBonus = 0f;
        EditorUtility.SetDirty(item);

        EnsurePickupPrefab(item);
        AssetDatabase.SaveAssets();
        return item;
    }

    /// <summary>Used by scene builders so future rebuilds keep the authored starting stack.</summary>
    public static void ConfigureProgression(PlayerProgression progression, bool resetRunOnAwake)
    {
        if (progression == null)
            throw new ArgumentNullException(nameof(progression));
        SerializedObject data = new SerializedObject(progression);
        data.FindProperty("startingKunaiItem").objectReferenceValue = EnsureAssets();
        data.FindProperty("startingKunaiCount").intValue = StackSize;
        data.FindProperty("resetRunOnAwake").boolValue = resetRunOnAwake;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(progression);
        PrefabUtility.RecordPrefabInstancePropertyModifications(progression);
    }

    /// <summary>Adds exactly one 16-count pickup to the existing Supply Chest drop list.</summary>
    public static void ConfigureSupplyChest(TreasureChest2D chest)
    {
        if (chest == null)
            throw new ArgumentNullException(nameof(chest));
        GameObject kunaiPickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        if (kunaiPickup == null)
            throw new MissingReferenceException(PickupPath);

        SerializedObject data = new SerializedObject(chest);
        SerializedProperty drops = data.FindProperty("itemPrefabs");
        List<GameObject> preserved = new List<GameObject>();
        for (int i = 0; i < drops.arraySize; i++)
        {
            GameObject drop = drops.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (drop != null && AssetDatabase.GetAssetPath(drop) != PickupPath)
                preserved.Add(drop);
        }
        preserved.Add(kunaiPickup);
        drops.arraySize = preserved.Count;
        for (int i = 0; i < preserved.Count; i++)
            drops.GetArrayElementAtIndex(i).objectReferenceValue = preserved[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(chest);
        PrefabUtility.RecordPrefabInstancePropertyModifications(chest);
    }

    [MenuItem("Tools/Inventory/Validate Kunai Inventory Item")]
    public static void Validate()
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath);
        GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath);
        Require(item != null && item.icon != null && item.type == ItemType.Ammunition,
            "Kunai ItemData or icon is missing.");
        ItemPickup pickup = pickupPrefab != null ? pickupPrefab.GetComponent<ItemPickup>() : null;
        Require(pickup != null && pickup.itemData == item && pickup.count == StackSize,
            "Kunai pickup must contain one stack of 16.");
        Require(pickupPrefab.GetComponent<Rigidbody2D>() != null &&
                pickupPrefab.GetComponents<Collider2D>().Any(collider => collider.isTrigger) &&
                pickupPrefab.GetComponents<Collider2D>().Any(collider => !collider.isTrigger),
            "Kunai pickup must keep authored gravity, pickup and ground-collision components.");

        Scene scene = EditorSceneManager.OpenScene(FullMapScenePath, OpenSceneMode.Single);
        PlayerProgression progression = FindInScene<PlayerProgression>(scene).Single();
        Require(progression.ResetsRunOnAwake && progression.StartingKunaiItem == item &&
                progression.StartingKunaiCount == StackSize,
            "stage1_full must grant 16 Kunai only when a fresh run is reset.");
        TreasureChest2D supply = FindInScene<TreasureChest2D>(scene)
            .Single(chest => chest.name == "Supply Treasure Chest");
        List<GameObject> drops = Enumerable.Range(0, supply.ConfiguredDropCount)
            .Select(supply.GetConfiguredDrop).Where(drop => drop != null).ToList();
        Require(drops.Count(drop => AssetDatabase.GetAssetPath(drop) == PickupPath) == 1,
            "Supply Treasure Chest must contain exactly one Kunai stack drop.");
        Require(drops.Single(drop => AssetDatabase.GetAssetPath(drop) == PickupPath)
                .GetComponent<ItemPickup>().count == StackSize,
            "Supply Treasure Chest Kunai drop must contain 16.");

        // Exercise the same model used by ItemPickup and the backpack UI without entering Play
        // Mode: identical ItemData must merge into one stack, and removing one chest-sized group
        // must leave the fresh-run group intact.
        try
        {
            RunInventory.Reset();
            RunInventory.Add(item, StackSize);
            RunInventory.Add(item, StackSize);
            Require(RunInventory.Stacks.Count == 1 && RunInventory.Count(item) == StackSize * 2,
                "Two 16-Kunai sources must merge into one 32-count inventory stack.");
            Require(RunInventory.Remove(item, StackSize) && RunInventory.Count(item) == StackSize,
                "Removing one Kunai group must leave the other 16-count group.");
        }
        finally
        {
            RunInventory.Reset();
        }
        Debug.Log("KUNAI_INVENTORY_VALIDATE_OK: ItemData, initial 16 and chest 16 are persistent and stackable.");
    }

    private static Sprite EnsureIconImport()
    {
        if (!File.Exists(IconPath))
            throw new FileNotFoundException("Imported Kunai icon was not found.", IconPath);
        TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException(IconPath + " is not a TextureImporter asset.");
        bool changed = importer.textureType != TextureImporterType.Sprite ||
                       importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        if (changed)
            importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        if (sprite == null)
            throw new MissingReferenceException("Kunai sprite import failed: " + IconPath);
        return sprite;
    }

    private static void EnsurePickupPrefab(ItemData item)
    {
        const string folder = "Assets/Prefab/EquipmentPickups";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Prefab", "EquipmentPickups");

        GameObject root = new GameObject("Kunai Pickup", typeof(SpriteRenderer), typeof(Rigidbody2D),
            typeof(BoxCollider2D), typeof(ItemPickup));
        try
        {
            // The source is a wide 1301x364 image at 100 PPU; this scale keeps the world pickup
            // readable beside the enlarged Hero without changing the inventory icon.
            root.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = item.icon;
            // "Default" is the parallax backdrop layer: a pickup left there is invisible
            // behind the map once it drops.
            renderer.sortingLayerName = SceneArt.ItemSortingLayer;
            renderer.sortingOrder = 35;

            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.linearDamping = 0.45f;
            body.angularDamping = 0.15f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            BoxCollider2D trigger = root.GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(10f, 3f);
            BoxCollider2D solid = root.AddComponent<BoxCollider2D>();
            solid.isTrigger = false;
            solid.size = new Vector2(8f, 2f);

            ItemPickup pickup = root.GetComponent<ItemPickup>();
            pickup.itemData = item;
            pickup.count = StackSize;
            PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureScene(string scenePath, bool addChestDrop)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        foreach (PlayerProgression progression in FindInScene<PlayerProgression>(scene))
            ConfigureProgression(progression, progression.ResetsRunOnAwake);
        if (addChestDrop)
        {
            TreasureChest2D supply = FindInScene<TreasureChest2D>(scene)
                .Single(chest => chest.name == "Supply Treasure Chest");
            ConfigureSupplyChest(supply);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, scenePath))
            throw new InvalidOperationException("Failed to save " + scenePath);
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
