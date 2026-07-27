using UnityEngine;

/// <summary>Persistent item definition referenced by inventory stacks.</summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item")]
public sealed class ItemData : ScriptableObject
{
    public string itemName;
    [TextArea(2, 5)]
    public string description;
    public Sprite icon;
    public ItemType type;

    [Header("Equipment")]
    [Tooltip("Weapon: the hero's base attack while worn (replaces the bare-handed 10).")]
    public float attackBonus;
    [Tooltip("Armor: the hero's flat damage reduction while worn (replaces the bare 2).")]
    public float defenseBonus;

    /// <summary>Every authored equipment category owns one paperdoll slot.</summary>
    public bool IsEquippable =>
        type == ItemType.Weapon || type == ItemType.Armor ||
        type == ItemType.Accessory || type == ItemType.GreenRune;

    /// <summary>Crimson Rune is wearable but intentionally excluded; Green Rune is the forgeable rune.</summary>
    public bool IsForgeable =>
        type == ItemType.Weapon || type == ItemType.Armor || type == ItemType.GreenRune;
}

public enum ItemType
{
    Weapon,
    Armor,
    Accessory,
    Potion,
    Material,
    KeyItem,
    Ammunition,
    // Appended to preserve the serialized integer values of every existing ItemData asset.
    GreenRune,
    // Read-only entries shown in the right half of the equipment paperdoll.
    // Appended so existing serialized ItemType integer values stay unchanged.
    Ability
}

/// <summary>
/// One presentation source for backpack, equipped slots and forge UI. ItemData stores immutable
/// base values; the current run's forge levels are applied here so every panel displays the same
/// values that PlayerProgression applies to combat.
/// </summary>
public static class ItemDisplay
{
    public const float WeaponAttackPerLevel = 10f;
    public const float ArmorDefensePerLevel = 2f;

    public static int ForgeLevel(ItemData item)
    {
        if (item == null)
            return 0;
        return item.type switch
        {
            ItemType.Weapon => RunProgress.ForgeWeaponLevel,
            ItemType.Armor => RunProgress.ForgeArmorLevel,
            ItemType.GreenRune => RunProgress.ForgeGreenRuneLevel,
            _ => 0
        };
    }

    public static string LocalizedName(ItemData item)
    {
        if (item == null)
            return string.Empty;
        string baseName = Localization.Translate(
            string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName);
        int level = ForgeLevel(item);
        return level > 0 ? baseName + "+" + level : baseName;
    }

    public static string LocalizedStats(ItemData item)
    {
        if (item == null)
            return string.Empty;
        int level = ForgeLevel(item);
        if (item.type == ItemType.Weapon)
            return Format(item.attackBonus + level * WeaponAttackPerLevel) + " ATK";
        if (item.type == ItemType.Armor)
            return Format(item.defenseBonus + level * ArmorDefensePerLevel) + " DEF";
        if (item.type == ItemType.GreenRune)
            return Format(HeroHealth.GetGreenRuneHps(level)) + " HPS";

        string result = string.Empty;
        if (item.attackBonus > 0f)
            result = Format(item.attackBonus) + " ATK";
        if (item.defenseBonus > 0f)
            result += (result.Length > 0 ? "    " : string.Empty) + Format(item.defenseBonus) + " DEF";
        if (result.Length > 0)
            return result;
        return Localization.Translate(item.type == ItemType.Ability ? "Passive movement ability"
            : item.type == ItemType.Potion ? "Restores HP to full"
            : item.type == ItemType.Ammunition ? "Stackable ranged ammunition"
            : item.IsEquippable ? "No stat bonus" : "Stackable item");
    }

    private static string Format(float value) => value.ToString("0.#");
}
