#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Turns the three equipment props placed in stage1 into real, pickable gear and makes the bag's
/// paperdoll slots wearable:
///   1. Builds an ItemData asset per piece (icon taken from the prop's SpriteRenderer).
///   2. Adds a trigger collider + ItemPickup to each prop so walking into it fills the bag.
///   3. Attaches EquipmentSlotUI (Weapon / Armor / Accessory) to the bag's left paperdoll column.
/// Idempotent — safe to re-run.
/// </summary>
public static class EquipmentBuilder
{
    private const string StageScenePath = "Assets/Scenes/Legacy/stage1.unity";
    private const string CanvasPrefabPath = "Assets/Prefab/Canvas.prefab";
    private const string ItemFolder = "Assets/Prefab/";
    public const string PickupFolder = "Assets/Prefab/EquipmentPickups";
    public const string SwordPickupPath = PickupFolder + "/ClaymoreSwordPickup.prefab";
    public const string ShieldPickupPath = PickupFolder + "/PlateShieldPickup.prefab";
    public const string GemPickupPath = PickupFolder + "/CrimsonGemPickup.prefab";
    public const string GreenRuneItemPath = "Assets/Prefab/Rune_Green.asset";
    public const string GreenRuneIconPath = "Assets/Resources/Sprites/icons/rune_green.png";
    public const string GreenRunePickupPath = PickupFolder + "/GreenRunePickup.prefab";
    public const string HealthPotionItemPath = "Assets/Prefab/HealthPotion.asset";
    public const string HealthPotionIconPath = "Assets/Prefab/HealthPotionIcon.png";
    public const string HealthPotionPickupPath = PickupFolder + "/HealthPotionPickup.prefab";

    private struct GearSpec
    {
        public string sceneObject;   // prop already placed in stage1
        public string assetName;     // ItemData asset file
        public string displayName;
        public ItemType type;
        public float attack;
        public float defense;
        public string description;
    }

    private static readonly GearSpec[] Gear =
    {
        new GearSpec { sceneObject = "weapon_claymore_0", assetName = "Weapon_Claymore", displayName = "Claymore Sword",
                       type = ItemType.Weapon, attack = 10f, defense = 0f,
                       description = "A heavy two-handed sword. Equip it to replace the hero's unarmed attack power." },
        new GearSpec { sceneObject = "armor_plate_0",     assetName = "Armor_Plate",     displayName = "Plate Shield",
                       type = ItemType.Armor, attack = 0f, defense = 6f,
                       description = "A sturdy plate shield that reduces the damage received from every enemy hit." },
        new GearSpec { sceneObject = "rune_crimson_0",    assetName = "Rune_Crimson",    displayName = "Crimson Gem",
                       type = ItemType.Accessory, attack = 0f, defense = 0f,
                       description = "A crimson rune that increases movement and jump speed by 10%, and dash speed by 30%. It cannot be forged." },
    };

    [MenuItem("Tools/Inventory/Build Equipment And Wearable Slots")]
    public static void BuildEquipment()
    {
        Scene scene = EditorSceneManager.OpenScene(StageScenePath, OpenSceneMode.Single);

        foreach (GearSpec spec in Gear)
        {
            GameObject prop = GameObject.Find(spec.sceneObject);
            if (prop == null)
            {
                Debug.LogWarning("[Equipment] Prop not found in " + StageScenePath + ": " + spec.sceneObject);
                continue;
            }

            SpriteRenderer renderer = prop.GetComponent<SpriteRenderer>();
            ItemData item = CreateOrUpdateItem(spec, renderer != null ? renderer.sprite : null);
            MakePickable(prop, item);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, StageScenePath))
            throw new InvalidOperationException("Failed to save " + StageScenePath);

        WireWearableSlots();
        EnsurePickupPrefabs();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("<color=lime>EQUIPMENT_OK: claymore/plate/rune are pickable, and the bag's paperdoll slots are wearable.</color>");
    }

    /// <summary>Builds three reusable physics pickup prefabs for treasure-chest drops.</summary>
    public static void EnsurePickupPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(PickupFolder))
            AssetDatabase.CreateFolder("Assets/Prefab", "EquipmentPickups");

        string[] paths = { SwordPickupPath, ShieldPickupPath, GemPickupPath };
        for (int i = 0; i < Gear.Length; i++)
        {
            string itemPath = ItemFolder + Gear[i].assetName + ".asset";
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath);
            if (item == null)
                throw new MissingReferenceException("Missing equipment ItemData: " + itemPath);

            // Keep names/descriptions in sync even when this method is called by the full-map builder.
            item.itemName = Gear[i].displayName;
            item.type = Gear[i].type;
            item.attackBonus = Gear[i].attack;
            item.defenseBonus = Gear[i].defense;
            item.description = Gear[i].description;
            EditorUtility.SetDirty(item);
            CreatePickupPrefab(paths[i], item);
        }
        EnsureGreenRunePickup();
        EnsureHealthPotionPickup();
        AssetDatabase.SaveAssets();
    }

    public static void EnsureGreenRunePickup()
    {
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(GreenRuneIconPath);
        if (icon == null)
            throw new MissingReferenceException("Missing green rune icon: " + GreenRuneIconPath);

        ItemData rune = AssetDatabase.LoadAssetAtPath<ItemData>(GreenRuneItemPath);
        if (rune == null)
        {
            rune = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(rune, GreenRuneItemPath);
        }

        rune.itemName = "Green Rune";
        rune.description = "Restores 2 HP per second while equipped. Each successful forge level adds another 2 HPS.";
        rune.icon = icon;
        rune.type = ItemType.GreenRune;
        rune.attackBonus = 0f;
        rune.defenseBonus = 0f;
        EditorUtility.SetDirty(rune);
        CreatePickupPrefab(GreenRunePickupPath, rune, 0.5f);
    }

    private static void EnsureHealthPotionPickup()
    {
        Sprite icon = EnsureHealthPotionIcon();
        ItemData potion = AssetDatabase.LoadAssetAtPath<ItemData>(HealthPotionItemPath);
        if (potion == null)
        {
            potion = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(potion, HealthPotionItemPath);
        }
        potion.itemName = "Health Potion";
        potion.description = "A single-use red potion. Select it in the backpack and press E to restore HP to full.";
        potion.icon = icon;
        potion.type = ItemType.Potion;
        potion.attackBonus = 0f;
        potion.defenseBonus = 0f;
        EditorUtility.SetDirty(potion);
        CreatePickupPrefab(HealthPotionPickupPath, potion);
    }

    private static Sprite EnsureHealthPotionIcon()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(HealthPotionIconPath);
        if (existing != null)
            return existing;

        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 outline = new Color32(55, 20, 30, 255);
        Color32 glass = new Color32(225, 225, 240, 255);
        Color32 liquid = new Color32(220, 35, 55, 255);
        Color32 shine = new Color32(255, 145, 155, 255);
        Color32[] pixels = new Color32[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        for (int y = 5; y <= 22; y++)
        for (int x = 8; x <= 23; x++)
        {
            bool border = x == 8 || x == 23 || y == 5 || y == 22;
            pixels[y * 32 + x] = border ? outline : (y <= 16 ? liquid : glass);
        }
        for (int y = 23; y <= 28; y++)
        for (int x = 13; x <= 18; x++)
            pixels[y * 32 + x] = x == 13 || x == 18 || y == 28 ? outline : glass;
        for (int y = 10; y <= 15; y++)
            pixels[y * 32 + 11] = shine;

        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(HealthPotionIconPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(HealthPotionIconPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(HealthPotionIconPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(HealthPotionIconPath);
    }

    private static void CreatePickupPrefab(string path, ItemData item, float worldScale = 3f)
    {
        GameObject root = new GameObject(item.itemName + " Pickup", typeof(SpriteRenderer), typeof(Rigidbody2D),
            typeof(CircleCollider2D), typeof(ItemPickup));
        try
        {
            root.transform.localScale = Vector3.one * worldScale;
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

            CircleCollider2D pickupTrigger = root.GetComponent<CircleCollider2D>();
            pickupTrigger.isTrigger = true;
            pickupTrigger.radius = 0.62f;
            CircleCollider2D solid = root.AddComponent<CircleCollider2D>();
            solid.isTrigger = false;
            solid.radius = 0.45f;

            ItemPickup pickup = root.GetComponent<ItemPickup>();
            pickup.itemData = item;
            pickup.count = 1;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static ItemData CreateOrUpdateItem(GearSpec spec, Sprite icon)
    {
        string path = ItemFolder + spec.assetName + ".asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, path);
        }

        item.itemName = spec.displayName;
        item.type = spec.type;
        item.attackBonus = spec.attack;
        item.defenseBonus = spec.defense;
        item.description = spec.description;
        if (icon != null)
            item.icon = icon;
        EditorUtility.SetDirty(item);
        return item;
    }

    /// <summary>Gives the prop a trigger collider + ItemPickup so the hero can walk into it.</summary>
    private static void MakePickable(GameObject prop, ItemData item)
    {
        CircleCollider2D trigger = prop.GetComponent<CircleCollider2D>();
        if (trigger == null)
            trigger = prop.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;

        ItemPickup pickup = prop.GetComponent<ItemPickup>();
        if (pickup == null)
            pickup = prop.AddComponent<ItemPickup>();
        pickup.itemData = item;
        pickup.count = 1;
        EditorUtility.SetDirty(prop);
    }

    /// <summary>Makes the left paperdoll column the Weapon / Armor / Crimson Rune / Green Rune slots.</summary>
    public static void WireWearableSlots()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            ItemType[] slotTypes =
                { ItemType.Weapon, ItemType.Armor, ItemType.Accessory, ItemType.GreenRune };
            for (int i = 0; i < slotTypes.Length; i++)
            {
                Transform slot = FindDeep(root.transform, "EquipSlot_L" + i);
                if (slot == null)
                {
                    Debug.LogWarning("[Equipment] Paperdoll slot missing: EquipSlot_L" + i +
                                     " — run Tools/Alpha UI/Repair Prefab and Gameplay Scenes first.");
                    continue;
                }

                EquipmentSlotUI wearable = slot.GetComponent<EquipmentSlotUI>();
                if (wearable == null)
                    wearable = slot.gameObject.AddComponent<EquipmentSlotUI>();
                wearable.slotType = slotTypes[i];

                Transform iconTransform = slot.Find("Icon");
                if (iconTransform != null)
                    wearable.icon = iconTransform.GetComponent<Image>();

                // The slot itself must catch the drop, so it needs a raycastable graphic.
                Image hit = slot.GetComponent<Image>();
                if (hit != null)
                    hit.raycastTarget = true;
            }

            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindDeep(Transform root, string childName)
    {
        if (root.name == childName)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
