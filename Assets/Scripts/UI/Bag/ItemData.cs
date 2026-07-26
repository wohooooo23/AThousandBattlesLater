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
