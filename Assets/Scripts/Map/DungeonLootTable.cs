using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class DungeonLootDrop
{
    public GameObject prefab;
    [Min(1)] public int count = 1;
}
[Serializable]
public sealed class DungeonLootRecipe
{
    public string id;
    public int priority;
    public bool equipment;
    public DungeonLootDrop[] drops;
}
[CreateAssetMenu(menuName = "A Thousand Battles Later/Dungeon Loot Table")]
public sealed class DungeonLootTable : ScriptableObject
{
    public GameObject chestPrefab;
    [Min(.5f)] public float chestWidthCells = 1.5f;
    [Tooltip("Transparent bottom padding of the closed chest sprite, as a fraction of its frame height (72 / 400 for the campaign art).")]
    [Range(0, .5f)] public float chestBottomPadding = .18f;
    public DungeonLootRecipe[] recipes;
    public DungeonRewardSession Allocate(int chests, int seed, bool freshRun = false)
    {
        var random = new System.Random(seed ^ 0x617328);
        bool Owned(DungeonLootRecipe recipe) => !freshRun && recipe.drops.Any(d =>
        {
            var item = d.prefab.GetComponent<ItemPickup>().itemData;
            return RunInventory.Count(item) > 0 || RunEquipment.Get(item.type) == item;
        });
        var pool = recipes.Where(r => r.equipment && !Owned(r)).OrderByDescending(r => r.priority)
            .ThenBy(_ => random.Next()).ToList();
        var supply = recipes.Single(r => !r.equipment);
        var session = new DungeonRewardSession();
        for (int i = 0; i < chests; i++) session.Chests.Add(new DungeonChestState(i, i < pool.Count ? pool[i] : supply));
        return session;
    }
}
public sealed class DungeonRewardSession
{
    public readonly List<DungeonChestState> Chests = new();
    public string Signature => string.Join("|", Chests.Select(c => c.Id + ":" + c.Recipe.id));
}
public sealed class DungeonChestState
{
    public readonly int Id;
    public readonly DungeonLootRecipe Recipe;
    private readonly bool[] claimed;
    public DungeonChestState(int id, DungeonLootRecipe recipe)
    {
        Id = id;
        Recipe = new DungeonLootRecipe { id = recipe.id, priority = recipe.priority, equipment = recipe.equipment,
            drops = recipe.drops.Select(d => new DungeonLootDrop { prefab = d.prefab, count = d.count }).ToArray() };
        claimed = new bool[Recipe.drops.Length];
    }
    public bool IsClaimed(int index) => claimed[index];
    public void Claim(int index) => claimed[index] = true;
    public bool Complete => claimed.All(value => value);
}
