#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键生成演示物品：一张简单贴图（青色菱形）+ DemoItem(ItemData) + DemoItemPickup 预制体。
/// 生成后把 Assets/Prefab/DemoItemPickup.prefab 拖进地图场景，走过去即可拾取，
/// 用来演示背包里的物品移动（和金币一起拖拽换位）。
/// 菜单：Tools > Inventory > Create Demo Item
/// </summary>
public static class DemoItemBuilder
{
    private const string SpritePath = "Assets/Prefab/DemoItemIcon.png";
    private const string ItemPath = "Assets/Prefab/DemoItem.asset";
    private const string PickupPath = "Assets/Prefab/DemoItemPickup.prefab";

    [MenuItem("Tools/Inventory/Create Demo Item")]
    public static void Create()
    {
        EnsureSprite();
        ItemData item = EnsureItemData();
        EnsurePickupPrefab(item);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Demo item ready. 把 " + PickupPath + " 拖进地图场景，走过去拾取后按 B 开背包即可拖拽换位。");
    }

    private static void EnsureSprite()
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath) != null)
            return;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color body = new Color(0.20f, 0.85f, 1f, 1f); // 青色菱形，和金币的圆形区分开
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs((x + 0.5f) / size - 0.5f);
                float py = Mathf.Abs((y + 0.5f) / size - 0.5f);
                texture.SetPixel(x, y, (px + py) <= 0.42f ? body : Color.clear);
            }
        }
        texture.Apply();

        File.WriteAllBytes(Path.Combine(Application.dataPath, SpritePath.Substring("Assets/".Length)), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64f;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static ItemData EnsureItemData()
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, ItemPath);
        }
        item.itemName = "Demo Cube";
        item.description = "A simple material used to demonstrate inventory stacking and rearrangement.";
        item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        item.type = ItemType.Material;
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void EnsurePickupPrefab(ItemData item)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath) != null)
            return;

        GameObject root = new GameObject("DemoItemPickup");
        try
        {
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = item.icon;
            renderer.sortingOrder = 5;

            CircleCollider2D collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            ItemPickup pickup = root.AddComponent<ItemPickup>();
            pickup.itemData = item;
            pickup.count = 1;

            PrefabUtility.SaveAsPrefabAsset(root, PickupPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
#endif
