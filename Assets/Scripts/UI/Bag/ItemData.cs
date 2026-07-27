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
        return LocalizedName(item, ForgeLevel(item));
    }

    /// <summary>Builds a name for an explicit level, used by the forge before the run level changes.</summary>
    public static string LocalizedName(ItemData item, int forgeLevel)
    {
        if (item == null)
            return string.Empty;
        string sourceName = string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        if (string.IsNullOrWhiteSpace(sourceName))
            sourceName = "Unnamed Item";
        string baseName = Localization.Translate(sourceName);
        // A malformed localisation entry must never erase the identifying label.
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = sourceName;
        int level = Mathf.Max(0, forgeLevel);
        return level > 0 ? baseName + "+" + level : baseName;
    }

    public static string LocalizedStats(ItemData item)
    {
        return LocalizedStats(item, ForgeLevel(item));
    }

    /// <summary>Builds the displayed primary stat from the same formula used by gameplay.</summary>
    public static string LocalizedStats(ItemData item, int forgeLevel)
    {
        if (item == null)
            return string.Empty;
        string statLabel = PrimaryStatLabel(item);
        if (!string.IsNullOrEmpty(statLabel))
            return Format(PrimaryStatValue(item, forgeLevel)) + " " + statLabel;

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

    public static float PrimaryStatValue(ItemData item, int forgeLevel)
    {
        if (item == null)
            return 0f;
        int level = Mathf.Max(0, forgeLevel);
        return item.type switch
        {
            ItemType.Weapon => item.attackBonus + level * WeaponAttackPerLevel,
            ItemType.Armor => item.defenseBonus + level * ArmorDefensePerLevel,
            ItemType.GreenRune => HeroHealth.GetGreenRuneHps(level),
            _ => 0f
        };
    }

    public static float ForgeStatPerLevel(ItemData item)
    {
        if (item == null)
            return 0f;
        return item.type switch
        {
            ItemType.Weapon => WeaponAttackPerLevel,
            ItemType.Armor => ArmorDefensePerLevel,
            ItemType.GreenRune => HeroHealth.GreenRuneHpsPerForgeLevel,
            _ => 0f
        };
    }

    public static string PrimaryStatLabel(ItemData item)
    {
        if (item == null)
            return string.Empty;
        return item.type switch
        {
            ItemType.Weapon => "ATK",
            ItemType.Armor => "DEF",
            ItemType.GreenRune => "HPS",
            _ => string.Empty
        };
    }

    public static string FormatStat(float value) => value.ToString("0.#");

    private static string Format(float value) => FormatStat(value);
}
