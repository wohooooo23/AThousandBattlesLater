using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Scene-authored, read-only paperdoll slot for a movement ability. The slot mirrors RunProgress:
/// abilities appear automatically when granted and deliberately expose no drop or unequip route.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class AbilityEquipmentSlotUI : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
{
    public AbilityEquipmentKind ability;
    public ItemData abilityItem;
    public Image icon;

    public ItemData CurrentItem => RunProgress.IsAbilityEquipped(ability) ? abilityItem : null;

    private void Awake()
    {
        if (icon == null)
            icon = transform.Find("Icon")?.GetComponent<Image>();
    }

    private void OnEnable()
    {
        RunProgress.Changed += Render;
        Render();
    }

    private void OnDisable()
    {
        RunProgress.Changed -= Render;
        ItemDetailPanel.Instance?.InvalidateAbilitySource(this);
    }

    public void Render()
    {
        if (icon == null)
            return;
        ItemData item = CurrentItem;
        icon.sprite = item != null ? item.icon : null;
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.enabled = item != null && item.icon != null;
        ItemDetailPanel.Instance?.RefreshAbilitySource(this);
    }

    public void OnPointerEnter(PointerEventData eventData) =>
        ItemDetailPanel.Instance?.ShowAbilityHover(this, eventData.position);

    public void OnPointerExit(PointerEventData eventData) =>
        ItemDetailPanel.Instance?.HideAbilityHover(this);

    public void OnPointerMove(PointerEventData eventData) =>
        ItemDetailPanel.Instance?.MoveAbilityPointer(this, eventData.position);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            ItemDetailPanel.Instance?.PinAbility(this, eventData.position);
    }
}
