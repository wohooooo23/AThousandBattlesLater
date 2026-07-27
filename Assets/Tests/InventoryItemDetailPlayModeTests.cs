#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class InventoryItemDetailPlayModeTests : InputTestFixture
{
    private Keyboard keyboard;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
    }

    [UnityTest]
    public IEnumerator HoverClickCancelAndKeyboardEquipUseTheAuthoredDetailPanel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("stage1_full");
        yield return null;

        Type itemType = FindRuntimeType("ItemData");
        Type inventoryType = FindRuntimeType("RunInventory");
        Type equipmentType = FindRuntimeType("RunEquipment");
        Type progressType = FindRuntimeType("RunProgress");
        UnityEngine.Object coin = AssetDatabase.LoadAssetAtPath("Assets/Prefab/GoldCoin.asset", itemType);
        UnityEngine.Object weapon = AssetDatabase.LoadAssetAtPath("Assets/Prefab/Weapon_Claymore.asset", itemType);
        UnityEngine.Object potion = AssetDatabase.LoadAssetAtPath("Assets/Prefab/HealthPotion.asset", itemType);
        Assert.That(coin, Is.Not.Null);
        Assert.That(weapon, Is.Not.Null);
        Assert.That(potion, Is.Not.Null);
        Assert.That((string)itemType.GetField("description").GetValue(weapon), Is.Not.Empty);

        inventoryType.GetMethod("Reset").Invoke(null, null);
        equipmentType.GetMethod("Reset").Invoke(null, null);
        inventoryType.GetMethod("Add").Invoke(null, new object[] { coin, 1 });
        inventoryType.GetMethod("Add").Invoke(null, new object[] { weapon, 1 });

        MonoBehaviour bagButton = FindBehaviour("BagButton");
        MonoBehaviour details = FindBehaviour("ItemDetailPanel");
        Assert.That(bagButton, Is.Not.Null);
        Assert.That(details, Is.Not.Null, "ItemDetailPanel must be stored in Canvas.prefab.");
        bagButton.GetType().GetMethod("Toggle").Invoke(bagButton, null);
        yield return null;
        yield return null;

        MonoBehaviour coinSlot = FindSlotHolding(coin);
        MonoBehaviour weaponSlot = FindSlotHolding(weapon);
        Assert.That(coinSlot, Is.Not.Null);
        Assert.That(weaponSlot, Is.Not.Null);

        details.GetType().GetMethod("ShowHover").Invoke(details,
            new object[] { coinSlot, new Vector2(500f, 500f) });
        Assert.That(ReadProperty<bool>(details, "IsVisible"), Is.True);
        Assert.That(ReadProperty<UnityEngine.Object>(details, "CurrentItem"), Is.EqualTo(coin));
        Text title = (Text)ReadField(details, "titleText");
        Text description = (Text)ReadField(details, "descriptionText");
        Assert.That(title.text, Is.EqualTo("Gold Coin"));
        Assert.That(description.text, Is.Not.Empty);

        details.GetType().GetMethod("Pin").Invoke(details,
            new object[] { coinSlot, new Vector2(500f, 500f) });
        Assert.That(ReadProperty<bool>(details, "IsPinned"), Is.True);
        Press(keyboard.qKey);
        yield return null;
        Release(keyboard.qKey);
        Assert.That(ReadProperty<bool>(details, "IsPinned"), Is.False,
            "Q must cancel item actions instead of quitting play mode.");

        details.GetType().GetMethod("Pin").Invoke(details,
            new object[] { weaponSlot, new Vector2(600f, 500f) });
        Press(keyboard.eKey);
        yield return null;
        Release(keyboard.eKey);
        yield return null;

        object weaponType = itemType.GetField("type").GetValue(weapon);
        object equipped = equipmentType.GetMethod("Get").Invoke(null, new[] { weaponType });
        Assert.That(equipped, Is.EqualTo(weapon));
        Assert.That(ReadProperty<bool>(details, "IsVisible"), Is.False);

        MonoBehaviour wornWeaponSlot = FindEquipmentSlotHolding(weapon);
        Assert.That(wornWeaponSlot, Is.Not.Null);
        details.GetType().GetMethod("ShowEquipmentHover").Invoke(details,
            new object[] { wornWeaponSlot, new Vector2(900f, 500f) });
        Assert.That(ReadProperty<bool>(details, "IsVisible"), Is.True);
        Assert.That(ReadProperty<bool>(details, "IsEquipmentItem"), Is.True);
        Assert.That(ReadProperty<UnityEngine.Object>(details, "CurrentItem"), Is.EqualTo(weapon));

        progressType.GetMethod("SetForgeLevels").Invoke(null, new object[] { 1, 0, 0 });
        yield return null;
        Text stats = (Text)ReadField(details, "statsText");
        float forgedAttack = (float)itemType.GetField("attackBonus").GetValue(weapon) + 10f;
        Assert.That(title.text, Is.EqualTo("Claymore Sword+1"));
        Assert.That(stats.text, Is.EqualTo(forgedAttack.ToString("0.#") + " ATK"),
            "Backpack/equipped details must show the same forged ATK used by combat.");

        PointerEventData equipmentClick = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = new Vector2(900f, 500f)
        };
        wornWeaponSlot.GetType().GetMethod("OnPointerClick").Invoke(wornWeaponSlot, new object[] { equipmentClick });
        Press(keyboard.qKey);
        yield return null;
        Release(keyboard.qKey);
        yield return null;
        Assert.That(equipmentType.GetMethod("Get").Invoke(null, new[] { weaponType }), Is.EqualTo(weapon),
            "Q must cancel without removing worn equipment.");

        wornWeaponSlot.GetType().GetMethod("OnPointerClick").Invoke(wornWeaponSlot, new object[] { equipmentClick });
        Assert.That(ReadProperty<bool>(details, "IsPinned"), Is.True);
        Assert.That(ReadProperty<bool>(details, "IsEquipmentItem"), Is.True);
        Assert.That(ReadProperty<UnityEngine.Object>(details, "CurrentItem"), Is.EqualTo(weapon));
        Assert.That(keyboard.eKey.isPressed, Is.False, "E must be released before the unequip action.");
        Press(keyboard.eKey);
        yield return null;
        Release(keyboard.eKey);
        yield return null;
        Assert.That(equipmentType.GetMethod("Get").Invoke(null, new[] { weaponType }), Is.Null,
            "E must unequip the pinned worn item and return it to the bag.");
        Assert.That(ReadProperty<bool>(details, "IsVisible"), Is.False);

        inventoryType.GetMethod("Add").Invoke(null, new object[] { potion, 1 });
        yield return null;
        MonoBehaviour potionSlot = FindSlotHolding(potion);
        Assert.That(potionSlot, Is.Not.Null, "The upper-chest potion must occupy a normal backpack slot.");
        MonoBehaviour heroHealth = FindBehaviour("HeroHealth");
        Assert.That(heroHealth, Is.Not.Null);
        float maximumHealth = ReadProperty<float>(heroHealth, "MaximumHealth");
        heroHealth.GetType().GetMethod("ApplyDamage").Invoke(heroHealth, new object[] { 40f, null });
        Assert.That(ReadProperty<float>(heroHealth, "CurrentHealth"), Is.LessThan(maximumHealth));

        details.GetType().GetMethod("Pin").Invoke(details,
            new object[] { potionSlot, new Vector2(700f, 500f) });
        Text prompt = (Text)ReadField(details, "promptText");
        Assert.That(prompt.text, Does.Contain("Use"));
        Press(keyboard.eKey);
        yield return null;
        Release(keyboard.eKey);
        yield return null;
        Assert.That(ReadProperty<float>(heroHealth, "CurrentHealth"), Is.EqualTo(maximumHealth).Within(0.001f),
            "E on a selected Health Potion must restore the Hero to full HP.");
        Assert.That((int)inventoryType.GetMethod("Count").Invoke(null, new object[] { potion }), Is.Zero,
            "Using the potion must consume exactly one item.");
        Assert.That(ReadProperty<bool>(details, "IsVisible"), Is.False);
        progressType.GetMethod("SetForgeLevels").Invoke(null, new object[] { 0, 0, 0 });
    }

    [Test]
    public void CrimsonRuneUsesReducedMovementMultipliers()
    {
        Type role = FindRuntimeType("Role");
        Assert.That((float)role.GetField("CrimsonMoveMultiplier").GetRawConstantValue(), Is.EqualTo(1.1f));
        Assert.That((float)role.GetField("CrimsonJumpMultiplier").GetRawConstantValue(), Is.EqualTo(1.1f));
        Assert.That((float)role.GetField("CrimsonDashMultiplier").GetRawConstantValue(), Is.EqualTo(1.3f));
    }

    private static MonoBehaviour FindSlotHolding(UnityEngine.Object item)
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (behaviour == null || behaviour.GetType().Name != "ItemSlot")
                continue;
            if (behaviour.GetType().GetField("mItem").GetValue(behaviour) == item)
                return behaviour;
        }
        return null;
    }

    private static MonoBehaviour FindEquipmentSlotHolding(UnityEngine.Object item)
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            if (behaviour == null || behaviour.GetType().Name != "EquipmentSlotUI")
                continue;
            if (ReadProperty<UnityEngine.Object>(behaviour, "CurrentItem") == item)
                return behaviour;
        }
        return null;
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        return null;
    }

    private static Type FindRuntimeType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null)
                return type;
        }
        throw new InvalidOperationException("Runtime type not found: " + name);
    }

    private static T ReadProperty<T>(object target, string name)
    {
        return (T)target.GetType().GetProperty(name).GetValue(target);
    }

    private static object ReadField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
#endif
