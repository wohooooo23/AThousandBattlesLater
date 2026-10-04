using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class WfcDungeonGameplayBuilder
{
    public static void Configure(Scene scene, WfcDungeonSettings settings)
    {
        var coin = ConfigureCoin();
        const string path = WfcDungeonBuilder.Folder + "/Loot.asset";
        var loot = AssetDatabase.LoadAssetAtPath<DungeonLootTable>(path);
        if (loot == null)
        {
            loot = ScriptableObject.CreateInstance<DungeonLootTable>();
            loot.chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/TreasureChest.prefab");
            DungeonLootDrop Drop(string name, int count = 1)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/EquipmentPickups/" + name + "Pickup.prefab");
                if (prefab == null || prefab.GetComponent<ItemPickup>() == null) throw new MissingReferenceException(name);
                return new DungeonLootDrop { prefab = prefab, count = count < 0 ? prefab.GetComponent<ItemPickup>().count : count };
            }
            loot.recipes = new[] {
                new DungeonLootRecipe { id="claymore",priority=100,equipment=true,drops=new[]{Drop("ClaymoreSword")} },
                new DungeonLootRecipe { id="plate",priority=100,equipment=true,drops=new[]{Drop("PlateShield")} },
                new DungeonLootRecipe { id="crimson",priority=100,equipment=true,drops=new[]{Drop("CrimsonGem")} },
                new DungeonLootRecipe { id="green",priority=50,equipment=true,drops=new[]{Drop("GreenRune")} },
                new DungeonLootRecipe { id="supplies",priority=0,equipment=false,drops=new[]{Drop("HealthPotion"),new DungeonLootDrop{prefab=coin,count=100},Drop("Kunai",-1)} }
            };
            AssetDatabase.CreateAsset(loot,path);
        }
        loot.recipes.Single(r => r.id == "supplies").drops[1].prefab = coin;
        EditorUtility.SetDirty(loot);
        settings.loot = loot; EditorUtility.SetDirty(settings);
        var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIManager>(true)).SingleOrDefault();
        if (ui == null)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Canvas.prefab"),scene);
            root.name = "Alpha UI"; ui = root.GetComponent<UIManager>();
        }
        var forge = ui.GetComponentInChildren<ForgeSystemController>(true);
        ui.GetComponentInChildren<ForgeButton>(true).mForgePanel = forge.gameObject;
        forge.gameObject.SetActive(false);
        var events = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EventSystem>(true)).ToArray();
        if (events.Length == 0) new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        else if (events.Length != 1) throw new InvalidOperationException("Dungeon must have exactly one EventSystem.");
        Validate(scene,settings);
    }
    private static GameObject ConfigureCoin()
    {
        string path = WfcDungeonBuilder.Folder + "/CoinPickup.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = PrefabUtility.LoadPrefabContents(existing != null ? path : "Assets/Prefab/Coin.prefab");
        try
        {
            root.GetComponent<ItemPickup>().itemData = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Prefab/GoldCoin.asset");
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void Validate(Scene scene, WfcDungeonSettings settings)
    {
        if (settings.loot == null || settings.loot.chestPrefab == null || settings.loot.recipes.Length != 5)
            throw new MissingReferenceException("Dungeon requires its five-recipe loot table and chest prefab.");
        foreach (var recipe in settings.loot.recipes)
            if (recipe.drops == null || recipe.drops.Any(d=>d.prefab == null || d.count < 1 || d.prefab.GetComponent<ItemPickup>() == null || d.prefab.GetComponent<ItemPickup>().itemData == null))
                throw new InvalidOperationException("Invalid loot recipe: " + recipe.id);
        if (scene.GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<UIManager>(true).Length) != 1)
            throw new InvalidOperationException("Dungeon needs exactly one gameplay UI.");
    }
}
