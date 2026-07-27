#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class Stage2RecoveryRewardsPlayModeTests
{
    [UnityTest]
    public IEnumerator Stage2ChestsRecoverMissingGearAndAbilitiesWhilePanelsHideKunaiHud()
    {
        Type inventoryType = GameType("RunInventory");
        Type equipmentType = GameType("RunEquipment");
        Type progressType = GameType("RunProgress");
        inventoryType.GetMethod("Reset").Invoke(null, null);
        equipmentType.GetMethod("Reset").Invoke(null, null);
        progressType.GetMethod("Reset").Invoke(null, null);

        SceneManager.LoadScene("stage2_full");
        yield return null;

        MonoBehaviour left = FindBehaviour("TreasureChest2D", "Double Jump Treasure Chest");
        MonoBehaviour right = FindBehaviour("TreasureChest2D", "Dash Treasure Chest");
        Assert.That(left, Is.Not.Null);
        Assert.That(right, Is.Not.Null);
        AssertDropTypes(left, "Potion", "Ammunition", "Weapon");
        AssertDropTypes(right, "GreenRune", "Armor");

        MonoBehaviour[] orbs = FindBehaviours("AbilityUnlockOrb2D");
        Assert.That(orbs.Length, Is.EqualTo(2));
        AssertOrb(orbs, "DoubleJump", left);
        AssertOrb(orbs, "Dash", right);

        GameObject swordPrefab = ConfiguredDrop(left, 2);
        GameObject shieldPrefab = ConfiguredDrop(right, 1);
        UnityEngine.Object sword = ItemFromPickup(swordPrefab);
        UnityEngine.Object shield = ItemFromPickup(shieldPrefab);
        Assert.That(RemainingDrops(left), Does.Contain(swordPrefab));
        Assert.That(RemainingDrops(right), Does.Contain(shieldPrefab));

        inventoryType.GetMethod("Add").Invoke(null, new object[] { sword, 1 });
        inventoryType.GetMethod("Add").Invoke(null, new object[] { shield, 1 });
        Assert.That((bool)equipmentType.GetMethod("Equip").Invoke(null, new object[] { shield }), Is.True);
        Assert.That(RemainingDrops(left).Contains(swordPrefab), Is.False,
            "A sword already in the backpack must not drop again.");
        Assert.That(RemainingDrops(right).Contains(shieldPrefab), Is.False,
            "A shield already equipped on the Hero must not drop again.");

        Type abilityType = GameType("AbilityUnlockKind");
        MethodInfo unlock = progressType.GetMethod("Unlock");
        unlock.Invoke(null, new[] { Enum.Parse(abilityType, "DoubleJump") });
        unlock.Invoke(null, new[] { Enum.Parse(abilityType, "Dash") });
        SceneManager.LoadScene("stage2_full");
        yield return null;
        foreach (MonoBehaviour orb in FindBehaviours("AbilityUnlockOrb2D"))
            Assert.That((bool)orb.GetType().GetProperty("IsCollected").GetValue(orb), Is.True,
                "An ability already unlocked in stage1 must not be offered again in stage2.");

        MonoBehaviour manager = FindBehaviours("UIManager").Single();
        GameObject kunaiHud = manager.GetType().GetProperty("KunaiHud").GetValue(manager) as GameObject;
        MonoBehaviour countHud = FindBehaviours("KunaiCountHud").Single();
        Assert.That(kunaiHud, Is.EqualTo(countHud.gameObject),
            "The saved Canvas prefab must wire UIManager directly to KunaiCountHud.");
        Assert.That(kunaiHud.activeSelf, Is.True);

        GameObject panel = new GameObject("HUD Occlusion Test Panel");
        panel.SetActive(true);
        object openPanels = manager.GetType().GetField("openPanels", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(manager);
        openPanels.GetType().GetMethod("Add").Invoke(openPanels, new object[] { panel });
        MethodInfo updatePauseState = manager.GetType().GetMethod("UpdatePauseState",
            BindingFlags.Instance | BindingFlags.NonPublic);
        updatePauseState.Invoke(manager, null);
        Assert.That(kunaiHud.activeSelf, Is.False,
            "Kunai HUD must hide while inventory or forge panels are open.");
        manager.GetType().GetMethod("CloseAllPanels").Invoke(manager, null);
        Assert.That(kunaiHud.activeSelf, Is.True);

        UnityEngine.Object.Destroy(panel);
        Time.timeScale = 1f;
        inventoryType.GetMethod("Reset").Invoke(null, null);
        equipmentType.GetMethod("Reset").Invoke(null, null);
        progressType.GetMethod("Reset").Invoke(null, null);
    }

    private static void AssertDropTypes(MonoBehaviour chest, params string[] expected)
    {
        Assert.That((int)chest.GetType().GetProperty("ConfiguredDropCount").GetValue(chest),
            Is.EqualTo(expected.Length));
        for (int i = 0; i < expected.Length; i++)
        {
            UnityEngine.Object item = ItemFromPickup(ConfiguredDrop(chest, i));
            string type = item.GetType().GetField("type").GetValue(item).ToString();
            Assert.That(type, Is.EqualTo(expected[i]));
        }
    }

    private static void AssertOrb(MonoBehaviour[] orbs, string ability, MonoBehaviour sourceChest)
    {
        MonoBehaviour orb = orbs.Single(candidate =>
            candidate.GetType().GetProperty("Ability").GetValue(candidate).ToString() == ability);
        Assert.That(orb.GetType().GetProperty("SourceChest").GetValue(orb), Is.EqualTo(sourceChest));
    }

    private static List<GameObject> RemainingDrops(MonoBehaviour chest)
    {
        MethodInfo method = chest.GetType().GetMethod("RemainingDrops", BindingFlags.Instance | BindingFlags.NonPublic);
        return ((IEnumerable)method.Invoke(chest, null)).Cast<GameObject>().ToList();
    }

    private static GameObject ConfiguredDrop(MonoBehaviour chest, int index) =>
        chest.GetType().GetMethod("GetConfiguredDrop").Invoke(chest, new object[] { index }) as GameObject;

    private static UnityEngine.Object ItemFromPickup(GameObject prefab)
    {
        MonoBehaviour pickup = prefab.GetComponent("ItemPickup") as MonoBehaviour;
        return pickup.GetType().GetField("itemData").GetValue(pickup) as UnityEngine.Object;
    }

    private static MonoBehaviour FindBehaviour(string typeName, string objectName) =>
        FindBehaviours(typeName).SingleOrDefault(behaviour => behaviour.name == objectName);

    private static MonoBehaviour[] FindBehaviours(string typeName) =>
        UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Where(behaviour => behaviour != null && behaviour.GetType().Name == typeName).ToArray();

    private static Type GameType(string name)
    {
        Type type = Type.GetType(name + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, name + " must exist in the game assembly.");
        return type;
    }
}
#endif
