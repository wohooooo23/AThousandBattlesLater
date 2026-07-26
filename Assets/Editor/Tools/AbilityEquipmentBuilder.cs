#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Authors the three passive ability definitions and the saved right-side paperdoll slots.</summary>
public static class AbilityEquipmentBuilder
{
    private const string CanvasPath = "Assets/Prefab/Canvas.prefab";
    private const string ItemFolder = "Assets/Prefab/Abilities";
    private const string IconFolder = "Assets/Resources/Sprites/icons";

    private static readonly string[] ItemPaths =
    {
        ItemFolder + "/Ability_WallJump.asset",
        ItemFolder + "/Ability_DoubleJump.asset",
        ItemFolder + "/Ability_Dash.asset"
    };

    private static readonly string[] IconPaths =
    {
        IconFolder + "/ability_wall_jump.png",
        IconFolder + "/ability_double_jump.png",
        IconFolder + "/ability_dash.png"
    };

    private static readonly string[] Names = { "Wall Jump Orb", "Double Jump Orb", "Dash Orb" };
    private static readonly string[] Descriptions =
    {
        "Enables wall sliding and a wall jump that launches the hero away from a wall.",
        "Grants one additional jump while airborne.",
        "Enables a high-speed ground and air dash with Shift."
    };

    private static readonly Color32[] Colors =
    {
        new Color32(76, 222, 117, 255),
        new Color32(70, 207, 255, 255),
        new Color32(237, 73, 103, 255)
    };

    [MenuItem("Tools/Inventory/Build Ability Equipment")]
    public static void Build()
    {
        EnsureAssets();
        WireCanvas();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
        Debug.Log("ABILITY_EQUIPMENT_OK: three passive ability items and read-only paperdoll slots are saved.");
    }

    public static void EnsureAssets()
    {
        EnsureFolder(ItemFolder);
        for (int i = 0; i < ItemPaths.Length; i++)
        {
            EnsureOrbIcon(IconPaths[i], Colors[i]);
            ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPaths[i]);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(item, ItemPaths[i]);
            }
            item.itemName = Names[i];
            item.description = Descriptions[i];
            item.type = ItemType.Ability;
            item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPaths[i]);
            item.attackBonus = 0f;
            item.defenseBonus = 0f;
            EditorUtility.SetDirty(item);
        }
    }

    public static ItemData GetItem(AbilityEquipmentKind ability) =>
        AssetDatabase.LoadAssetAtPath<ItemData>(ItemPaths[(int)ability]);

    private static void WireCanvas()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            for (int i = 0; i < 3; i++)
            {
                Transform slot = FindDeep(root.transform, "EquipSlot_R" + i);
                if (slot == null)
                    throw new MissingReferenceException("Canvas is missing EquipSlot_R" + i + ". Run Alpha UI repair first.");
                AbilityEquipmentSlotUI view = slot.GetComponent<AbilityEquipmentSlotUI>();
                if (view == null) view = slot.gameObject.AddComponent<AbilityEquipmentSlotUI>();
                view.ability = (AbilityEquipmentKind)i;
                view.abilityItem = GetItem((AbilityEquipmentKind)i);
                view.icon = slot.Find("Icon")?.GetComponent<Image>();
                EditorUtility.SetDirty(view);
            }
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Inventory/Validate Ability Equipment")]
    public static void Validate()
    {
        for (int i = 0; i < 3; i++)
        {
            ItemData item = GetItem((AbilityEquipmentKind)i);
            if (item == null || item.type != ItemType.Ability || item.icon == null || item.IsEquippable)
                throw new InvalidOperationException(Names[i] + " ItemData is missing or can be manually equipped.");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            for (int i = 0; i < 3; i++)
            {
                Transform slot = FindDeep(root.transform, "EquipSlot_R" + i);
                AbilityEquipmentSlotUI view = slot != null ? slot.GetComponent<AbilityEquipmentSlotUI>() : null;
                bool acceptsDrops = slot != null && slot.GetComponents<MonoBehaviour>().OfType<IDropHandler>().Any();
                if (view == null || view.ability != (AbilityEquipmentKind)i || view.abilityItem != GetItem((AbilityEquipmentKind)i) ||
                    view.icon == null || acceptsDrops || slot.GetComponent<EquipmentSlotUI>() != null)
                    throw new InvalidOperationException("EquipSlot_R" + i + " is not a saved read-only ability slot.");
            }
            Transform spare = FindDeep(root.transform, "EquipSlot_R3");
            if (spare == null || spare.GetComponent<AbilityEquipmentSlotUI>() != null ||
                spare.GetComponent<EquipmentSlotUI>() != null)
                throw new InvalidOperationException("EquipSlot_R3 must remain an empty reserved cell.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        RunProgress.Reset();
        try
        {
            if (RunProgress.IsAbilityEquipped(AbilityEquipmentKind.WallJump))
                throw new InvalidOperationException("Wall Jump should appear when a run starts, not before it.");
            RunProgress.MarkRunStarted();
            if (!RunProgress.IsAbilityEquipped(AbilityEquipmentKind.WallJump) ||
                RunProgress.IsAbilityEquipped(AbilityEquipmentKind.DoubleJump) ||
                RunProgress.IsAbilityEquipped(AbilityEquipmentKind.Dash))
                throw new InvalidOperationException("Initial passive ability state is incorrect.");
            RunProgress.Unlock(AbilityUnlockKind.DoubleJump);
            RunProgress.Unlock(AbilityUnlockKind.Dash);
            if (!Enum.GetValues(typeof(AbilityEquipmentKind)).Cast<AbilityEquipmentKind>().All(RunProgress.IsAbilityEquipped))
                throw new InvalidOperationException("Collected abilities did not auto-equip.");
        }
        finally { RunProgress.Reset(); }

        Debug.Log("ABILITY_EQUIPMENT_VALIDATE_OK.");
    }

    private static void EnsureOrbIcon(string path, Color32 fill)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 ring = new Color32((byte)Mathf.Min(255, fill.r + 18), (byte)Mathf.Min(255, fill.g + 18),
            (byte)Mathf.Min(255, fill.b + 18), 255);
        Vector2 center = new Vector2(31.5f, 31.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center);
            pixels[y * size + x] = distance <= 27f ? (distance >= 22f ? ring : fill) : clear;
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Substring("Assets/".Length).Split('/'))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
