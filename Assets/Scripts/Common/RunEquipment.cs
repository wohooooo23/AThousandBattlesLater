using System;
using UnityEngine;

// ============================================================
// RunEquipment — 本轮已穿戴的装备（武器 / 防具 / 符文）
//
// - 和 RunInventory 一样是静态的，所以切场景（关卡 -> Boss）不会丢。
// - 穿上 = 从背包取出并戴上；换下来的那件自动回到背包。
// - Changed 是唯一刷新信号：装备槽 UI、勇者属性、锻造面板都订阅它。
// ============================================================

public static class RunEquipment
{
    public static ItemData Weapon { get; private set; }
    public static ItemData Armor { get; private set; }
    public static ItemData Rune { get; private set; }
    public static ItemData GreenRune { get; private set; }

    public static event Action Changed;

    /// <summary>Accessory is the rune slot.</summary>
    public static ItemData Get(ItemType slot)
    {
        switch (slot)
        {
            case ItemType.Weapon: return Weapon;
            case ItemType.Armor: return Armor;
            case ItemType.Accessory: return Rune;
            case ItemType.GreenRune: return GreenRune;
            default: return null;
        }
    }

    /// <summary>Takes the item out of the bag and wears it; anything already worn goes back.</summary>
    public static bool Equip(ItemData item)
    {
        if (item == null || !item.IsEquippable)
            return false;
        if (!RunInventory.Remove(item, 1))
            return false;

        ItemData previous = Get(item.type);
        Set(item.type, item);
        if (previous != null)
            RunInventory.Add(previous, 1);

        Changed?.Invoke();
        return true;
    }

    /// <summary>Takes the slot off and returns the piece to the bag.</summary>
    public static bool Unequip(ItemType slot)
    {
        ItemData current = Get(slot);
        if (current == null)
            return false;

        Set(slot, null);
        RunInventory.Add(current, 1);
        Changed?.Invoke();
        return true;
    }

    public static void Reset()
    {
        Weapon = Armor = Rune = GreenRune = null;
        Changed?.Invoke();
    }

    private static void Set(ItemType slot, ItemData item)
    {
        switch (slot)
        {
            case ItemType.Weapon: Weapon = item; break;
            case ItemType.Armor: Armor = item; break;
            case ItemType.Accessory: Rune = item; break;
            case ItemType.GreenRune: GreenRune = item; break;
        }
    }

    /// <summary>A fresh Play session must not inherit the previous run's gear or subscribers.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnFreshPlay()
    {
        Weapon = Armor = Rune = GreenRune = null;
        Changed = null;
    }
}
