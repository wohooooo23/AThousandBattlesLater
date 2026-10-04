using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Development-only integration driver, invoked by Play Mode tests.</summary>
public static class WfcDungeonGameplayRegression
{
    private static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
    public static IEnumerator Run(WfcDungeonGenerator generator)
    {
        var hero=generator.hero; var table=generator.settings.loot; var ui=UIManager.Instance;
        Require(table!=null && ui!=null,"Dungeon loot/gameplay UI not wired.");
        var five=table.Allocate(5,3,true);
        Require(five.Chests.Take(3).All(c=>c.Recipe.priority==100) && five.Chests.Take(3).Select(c=>c.Recipe.id).Distinct().Count()==3,
            "Three high-priority items must precede green/supplies.");
        Require(five.Chests[3].Recipe.id=="green" && five.Chests[4].Recipe.id=="supplies","Additional loot order.");
        var supply=table.recipes.Single(r=>r.id=="supplies");
        generator.Rewards.Chests[0]=new DungeonChestState(0,supply);
        var state=generator.Rewards.Chests[0];
        var chest=generator.Content.GetComponentsInChildren<TreasureChest2D>().Single(c=>c.GeneratedState.Id==0);
        chest.ConfigureGenerated(state);
        hero.rb.constraints=RigidbodyConstraints2D.FreezeAll;
        hero.transform.position=chest.transform.position; hero.rb.position=hero.transform.position;
        Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return null;
        Require(chest.IsPlayerInRange,"Hero must enter the authored chest trigger.");
        ui.GetComponentInChildren<BagButton>(true).Toggle(); yield return null;
        Require(generator.InputBlocked && Time.timeScale==0 && !chest.OpenChest(),"Bag must pause and block opening chests.");
        ui.CloseAllPanels(); yield return null;
        Require(chest.OpenChest() && !chest.OpenChest(),"Chest opens exactly once.");
        var drops=generator.Content.GetComponentsInChildren<ItemPickup>();
        Require(drops.Length==3,"Supply chest drops all three entries.");
        var coin=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Prefab/GoldCoin.asset");
        foreach(var drop in drops) { if(drop.TryGetComponent<Rigidbody2D>(out var rb)) rb.simulated=false; drop.transform.position+=Vector3.up*100; }
        yield return new WaitForSeconds(1.1f);
        var coinDrop=drops.Single(d=>d.itemData==coin);
        coinDrop.transform.position=hero.transform.position;
        if(coinDrop.TryGetComponent<Rigidbody2D>(out var coinBody)) {coinBody.simulated=true;coinBody.position=hero.transform.position;}
        Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return null;
        Require(state.IsClaimed(1) && RunInventory.Count(coin)==100,"Actual pickup must claim the 100-coin entry.");
        RunInventory.Remove(coin,100);
        string signature=generator.Layout.Signature();
        generator.Retry(); yield return Wait(generator);
        Require(generator.Layout.Signature()==signature && generator.Rewards.Chests[0]==state,"Retry must preserve layout and reward snapshot.");
        Require(generator.Content.GetComponentsInChildren<ItemPickup>().Length==0,"Old unclaimed drops must be cleaned.");
        chest=generator.Content.GetComponentsInChildren<TreasureChest2D>().Single(c=>c.GeneratedState.Id==0);
        hero.rb.constraints=RigidbodyConstraints2D.FreezeAll; hero.transform.position=chest.transform.position; hero.rb.position=hero.transform.position;
        Physics2D.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return null;
        Require(chest.OpenChest(),"Unclaimed items remain available after retry.");
        Require(generator.Content.GetComponentsInChildren<ItemPickup>().All(d=>d.itemData!=coin),"Spent coins must not respawn on retry.");

        var weapon=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Prefab/Weapon_Claymore.asset");
        var rune=AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Prefab/Rune_Crimson.asset");
        float baseSpeed=hero.speed;
        RunInventory.Add(weapon); Require(RunEquipment.Equip(weapon),"Equip weapon from inventory.");
        RunInventory.Add(rune); Require(RunEquipment.Equip(rune),"Equip crimson rune.");
        float boosted=hero.speed; Require(Mathf.Abs(boosted-baseSpeed*Role.CrimsonMoveMultiplier)<.01f,"Rune applies once.");
        var forge=ui.GetComponentInChildren<ForgeSystemController>(true);
        ui.GetComponentInChildren<ForgeButton>(true).Toggle(); yield return null;
        forge.SelectEquipment(0); forge.StartForge(); yield return null;
        Require(RunProgress.ForgeWeaponLevel==0 && RunInventory.Count(coin)==0,"Insufficient funds cannot forge.");
        RunInventory.Add(coin,100); forge.StartForge();
        yield return new WaitForSecondsRealtime(3.1f);
        Require(RunProgress.ForgeWeaponLevel==1 && RunInventory.Count(coin)==0,"First forge succeeds and costs 100.");
        Require(Mathf.Abs(hero.AttackPower-(hero.BaseDamage+weapon.attackBonus+10))<.01f,"Additive attack formula.");
        ui.CloseAllPanels(); RunProgress.SetForgeLevels(4,0,0);
        ui.GetComponentInChildren<ForgeButton>(true).Toggle(); yield return null;
        forge.SelectEquipment(0); RunInventory.Add(coin,500);
        var savedRandom=UnityEngine.Random.state; int failureSeed=0;
        for(;failureSeed<1000;failureSeed++)
        {
            UnityEngine.Random.InitState(failureSeed);
            for(int i=0;i<20;i++) UnityEngine.Random.Range(-4f,4f);
            if(UnityEngine.Random.value>.25f) break;
        }
        UnityEngine.Random.InitState(failureSeed); forge.StartForge();
        yield return new WaitForSecondsRealtime(3.1f); UnityEngine.Random.state=savedRandom;
        Require(RunProgress.ForgeWeaponLevel==3 && RunInventory.Count(coin)==0,"Failed +5 attempt costs 500 and loses one level.");
        ui.CloseAllPanels(); RunProgress.SetForgeLevels(1,0,0);
        ui.CloseAllPanels(); yield return null;
        generator.Retry(); yield return Wait(generator);
        Require(Mathf.Abs(hero.speed-boosted)<.01f && RunProgress.ForgeWeaponLevel==1 && RunEquipment.Weapon==weapon,"Retry must keep gear, forging and rune movement.");
        Require(!table.Allocate(3,19).Chests.Any(c=>c.Recipe.id=="claymore" || c.Recipe.id=="crimson"),"New maps skip owned gear.");
        var beforeRuneMap=generator.Layout;
        yield return generator.GenerateConfigured(beforeRuneMap.Width,beforeRuneMap.Height,beforeRuneMap.Seed,TraversalProfile.Capture(hero),0,2,true);
        Require(generator.Layout!=beforeRuneMap && Mathf.Abs(generator.Layout.Profile.GroundSpeed-boosted)<.01f &&
            generator.Layout.Signature()!=beforeRuneMap.Signature() && !generator.Layout.Rooms[0].Route.SequenceEqual(beforeRuneMap.Rooms[0].Route),"Same seed with worn crimson must capture current movement and produce a different map.");
        RunEquipment.Unequip(ItemType.Accessory);
        Require(Mathf.Abs(hero.speed-baseSpeed)<.01f,"Removing crimson after generation must restore the original movement base.");
        Require(RunEquipment.Equip(rune) && Mathf.Abs(hero.speed-boosted)<.01f,"Re-equipping crimson must not compound its multiplier.");
        var retained=generator.Layout; var retainedRewards=generator.Rewards;
        yield return generator.GenerateConfigured(100,50,1,retained.Profile,0,10,true);
        Require(generator.Layout==retained && generator.Rewards==retainedRewards && RunEquipment.Weapon==weapon,"Failed generation preserves progress.");
        generator.NewRun(); yield return Wait(generator);
        Require(RunEquipment.Weapon==null && RunEquipment.Rune==null && RunInventory.Stacks.Count==0 && RunProgress.ForgeWeaponLevel==0,
            "Explicit new run clears growth.");
        Require(RunProgress.DoubleJumpUnlocked && RunProgress.DashUnlocked,"New run retains full movement unlocks.");
        hero.rb.constraints=RigidbodyConstraints2D.FreezeRotation;
    }
    private static IEnumerator Wait(WfcDungeonGenerator generator)
    {
        float limit=Time.realtimeSinceStartup+60;
        while(generator.Busy && Time.realtimeSinceStartup<limit) yield return null;
        Require(!generator.Busy,"Generation timed out."); yield return null;
    }
}
