using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ============================================================
// EquipmentSlotUI — 背包纸娃娃上的一个可穿戴槽位
//
// - 从背包把物品拖到这里：类型匹配才穿得上（武器->武器槽，以此类推）。
// - 点击已穿戴的槽位 = 脱下，装备回到背包。
// - 只渲染 RunEquipment 的内容，不自己存数据。
// ============================================================

[RequireComponent(typeof(Image))]
public sealed class EquipmentSlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler,
    IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
{
    [Tooltip("Weapon / Armor / Accessory(=符文). 决定这个槽能穿什么。")]
    public ItemType slotType = ItemType.Weapon;

    [Tooltip("显示已穿戴图标的子物体 Image。")]
    public Image icon;

    private void Awake()
    {
        if (icon == null)
        {
            Transform child = transform.Find("Icon");
            if (child != null)
                icon = child.GetComponent<Image>();
        }
    }

    private void OnEnable()
    {
        RunEquipment.Changed += Render;
        Render();
    }

    private void OnDisable()
    {
        RunEquipment.Changed -= Render;
        ItemDetailPanel.Instance?.InvalidateEquipmentSource(this);
    }

    public ItemData CurrentItem => RunEquipment.Get(slotType);

    public void Render()
    {
        if (icon == null)
            return;
        ItemData worn = RunEquipment.Get(slotType);
        icon.sprite = worn != null ? worn.icon : null;
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.enabled = worn != null && worn.icon != null;
        ItemDetailPanel.Instance?.RefreshEquipmentSource(this);
    }

    /// <summary>Drop from a bag slot: wear it if the type matches this slot.</summary>
    public void OnDrop(PointerEventData eventData)
    {
        ItemSlot source = ItemSlot.DragSource;
        if (source == null || source.mItem == null)
            return;
        if (source.mItem.type != slotType)
            return;   // wrong kind of gear for this slot

        RunEquipment.Equip(source.mItem);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ItemDetailPanel.Instance?.ShowEquipmentHover(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemDetailPanel.Instance?.HideEquipmentHover(this);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        ItemDetailPanel.Instance?.MoveEquipmentPointer(this, eventData.position);
    }

    /// <summary>Click a worn slot to pin its details; E performs the actual unequip action.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            ItemDetailPanel.Instance?.PinEquipment(this, eventData.position);
    }
}
