#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ForgeEquipmentDisplayPlayModeTests
{
    [UnityTest]
    public IEnumerator ShieldAndGreenRuneShareTheirRealLevelAndStatsAcrossInventoryAndForge()
    {
        SceneManager.LoadScene("stage1_full");
        yield return null;

        Type itemType = RuntimeType("ItemData");
        Type inventoryType = RuntimeType("RunInventory");
        Type equipmentType = RuntimeType("RunEquipment");
        Type progressType = RuntimeType("RunProgress");
        Type displayType = RuntimeType("ItemDisplay");
        UnityEngine.Object shield = AssetDatabase.LoadAssetAtPath("Assets/Prefab/Armor_Plate.asset", itemType);
        UnityEngine.Object greenRune = AssetDatabase.LoadAssetAtPath("Assets/Prefab/Rune_Green.asset", itemType);
        Assert.That(shield, Is.Not.Null);
        Assert.That(greenRune, Is.Not.Null);

        SetLanguage("English");
        inventoryType.GetMethod("Reset").Invoke(null, null);
        equipmentType.GetMethod("Reset").Invoke(null, null);
        inventoryType.GetMethod("Add").Invoke(null, new object[] { shield, 1 });
        inventoryType.GetMethod("Add").Invoke(null, new object[] { greenRune, 1 });
        Assert.That((bool)equipmentType.GetMethod("Equip").Invoke(null, new[] { shield }), Is.True);
        Assert.That((bool)equipmentType.GetMethod("Equip").Invoke(null, new[] { greenRune }), Is.True);
        progressType.GetMethod("SetForgeLevels").Invoke(null, new object[] { 0, 2, 3 });
        yield return null;

        MethodInfo displayName = displayType.GetMethod("LocalizedName", new[] { itemType });
        MethodInfo displayStats = displayType.GetMethod("LocalizedStats", new[] { itemType });
        Assert.That((string)displayName.Invoke(null, new[] { shield }), Is.EqualTo("Plate Shield+2"));
        Assert.That((string)displayStats.Invoke(null, new[] { shield }), Is.EqualTo("10 DEF"));
        Assert.That((string)displayName.Invoke(null, new[] { greenRune }), Is.EqualTo("Green Rune+3"));
        Assert.That((string)displayStats.Invoke(null, new[] { greenRune }), Is.EqualTo("8 HPS"));

        MonoBehaviour forge = FindBehaviour("ForgeSystemController");
        Assert.That(forge, Is.Not.Null);
        forge.gameObject.SetActive(true);
        yield return null;

        MethodInfo select = forge.GetType().GetMethod("SelectEquipment");
        Text activeName = Field<Text>(forge, "activeItemNameText");
        Text before = Field<Text>(forge, "statBeforeText");
        Text after = Field<Text>(forge, "statAfterText");

        select.Invoke(forge, new object[] { 1 });
        Assert.That(activeName.text, Is.EqualTo("Plate Shield+2"));
        Assert.That(before.text, Is.EqualTo("10 DEF"));
        Assert.That(after.text, Is.EqualTo("→ 12 DEF"));
        Assert.That((int)forge.GetType().GetMethod("GetArmorDEF").Invoke(forge, null), Is.EqualTo(10));

        select.Invoke(forge, new object[] { 2 });
        Assert.That(activeName.text, Is.EqualTo("Green Rune+3"));
        Assert.That(before.text, Is.EqualTo("8 HPS"));
        Assert.That(after.text, Is.EqualTo("→ 10 HPS"));

        MonoBehaviour details = FindBehaviour("ItemDetailPanel");
        MonoBehaviour shieldSlot = FindEquipmentSlot(shield);
        MonoBehaviour runeSlot = FindEquipmentSlot(greenRune);
        Assert.That(details, Is.Not.Null);
        Assert.That(shieldSlot, Is.Not.Null);
        Assert.That(runeSlot, Is.Not.Null);

        MethodInfo show = details.GetType().GetMethod("ShowEquipmentHover");
        Text detailTitle = Field<Text>(details, "titleText");
        Text detailStats = Field<Text>(details, "statsText");
        show.Invoke(details, new object[] { shieldSlot, new Vector2(600f, 500f) });
        Assert.That(detailTitle.text, Is.EqualTo("Plate Shield+2"));
        Assert.That(detailStats.text, Is.EqualTo("10 DEF"));
        show.Invoke(details, new object[] { runeSlot, new Vector2(600f, 500f) });
        Assert.That(detailTitle.text, Is.EqualTo("Green Rune+3"));
        Assert.That(detailStats.text, Is.EqualTo("8 HPS"));

        progressType.GetMethod("SetForgeLevels").Invoke(null, new object[] { 0, 0, 0 });
        equipmentType.GetMethod("Reset").Invoke(null, null);
        inventoryType.GetMethod("Reset").Invoke(null, null);
    }

    private static MonoBehaviour FindEquipmentSlot(UnityEngine.Object item)
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include))
        {
            if (behaviour != null && behaviour.GetType().Name == "EquipmentSlotUI" &&
                (UnityEngine.Object)behaviour.GetType().GetProperty("CurrentItem").GetValue(behaviour) == item)
                return behaviour;
        }
        return null;
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include))
            if (behaviour != null && behaviour.GetType().Name == typeName)
                return behaviour;
        return null;
    }

    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(target);
    }

    private static Type RuntimeType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name);
            if (type != null)
                return type;
        }
        throw new InvalidOperationException("Runtime type not found: " + name);
    }

    private static void SetLanguage(string language)
    {
        Type localization = RuntimeType("Localization");
        MethodInfo method = localization.GetMethod("SetLanguage", BindingFlags.Public | BindingFlags.Static);
        object value = Enum.Parse(method.GetParameters()[0].ParameterType, language);
        method.Invoke(null, new[] { value });
    }
}
#endif
