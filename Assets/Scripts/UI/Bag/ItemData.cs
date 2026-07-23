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

    [Header("Equipment (Weapon / Armor / Accessory only)")]
    [Tooltip("Weapon: the hero's base attack while worn (replaces the bare-handed 10).")]
    public float attackBonus;
    [Tooltip("Armor: the hero's flat damage reduction while worn (replaces the bare 2).")]
    public float defenseBonus;

    /// <summary>Weapon / Armor / Accessory can be worn in the bag's paperdoll slots.</summary>
    public bool IsEquippable =>
        type == ItemType.Weapon || type == ItemType.Armor || type == ItemType.Accessory;
}

public enum ItemType
{
    Weapon,
    Armor,
    Accessory,
    Potion,
    Material,
    KeyItem,
    Ammunition
}
