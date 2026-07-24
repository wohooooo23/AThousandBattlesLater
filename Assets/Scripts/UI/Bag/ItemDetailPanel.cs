using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Scene-authored inventory tooltip. Hover previews an item; click pins keyboard actions.
/// It stays alive under Canvas and uses CanvasGroup visibility instead of runtime UI construction.
/// </summary>
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(Image))]
public sealed class ItemDetailPanel : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text typeText;
    [SerializeField] private Text statsText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text promptText;
    [SerializeField] private Vector2 pointerOffset = new Vector2(22f, -18f);

    private CanvasGroup canvasGroup;
    private RectTransform panelRect;
    private RectTransform canvasRect;
    private Canvas ownerCanvas;
    private ItemSlot source;
    private EquipmentSlotUI equipmentSource;
    private bool pinned;

    public static ItemDetailPanel Instance { get; private set; }

    public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.5f;
    public bool IsPinned => pinned;
    public ItemData CurrentItem => equipmentSource != null
        ? RunEquipment.Get(equipmentSource.slotType)
        : source != null ? source.mItem : null;
    public bool IsEquipmentItem => equipmentSource != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        panelRect = (RectTransform)transform;
        ownerCanvas = GetComponentInParent<Canvas>();
        canvasRect = ownerCanvas != null ? ownerCanvas.transform as RectTransform : null;
        SetVisible(false);
    }

    private void Update()
    {
        if (!pinned || CurrentItem == null)
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.qKey.wasPressedThisFrame)
        {
            HideImmediate();
            return;
        }

        if (keyboard.eKey.wasPressedThisFrame && equipmentSource != null)
        {
            ItemType slot = equipmentSource.slotType;
            if (RunEquipment.Unequip(slot))
                HideImmediate();
            return;
        }

        if (keyboard.eKey.wasPressedThisFrame && CurrentItem.IsEquippable)
        {
            ItemData item = CurrentItem;
            if (RunEquipment.Equip(item))
                HideImmediate();
            return;
        }

        if (keyboard.eKey.wasPressedThisFrame && CurrentItem.type == ItemType.Potion)
            TryUsePotion(CurrentItem);
    }

    private void LateUpdate()
    {
        if (IsVisible && !pinned && Mouse.current != null)
            PositionAt(Mouse.current.position.ReadValue());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowHover(ItemSlot itemSlot, Vector2 screenPosition)
    {
        if (pinned || itemSlot == null || itemSlot.mItem == null)
            return;
        SetSource(itemSlot);
        Render(false);
        PositionAt(screenPosition);
        SetVisible(true);
    }

    public void MovePointer(ItemSlot itemSlot, Vector2 screenPosition)
    {
        if (!pinned && source == itemSlot && IsVisible)
        {
            PositionAt(screenPosition);
        }
    }

    public void HideHover(ItemSlot itemSlot)
    {
        if (!pinned && source == itemSlot)
            HideImmediate();
    }

    public void Pin(ItemSlot itemSlot, Vector2 screenPosition)
    {
        if (itemSlot == null || itemSlot.mItem == null)
            return;
        SetSource(itemSlot);
        pinned = true;
        Render(true);
        PositionAt(screenPosition);
        SetVisible(true);
    }

    public void RefreshSource(ItemSlot itemSlot)
    {
        if (source != itemSlot)
            return;
        if (itemSlot.mItem == null)
            HideImmediate();
        else
            Render(pinned);
    }

    public void InvalidateSource(ItemSlot itemSlot)
    {
        if (source == itemSlot)
            HideImmediate();
    }

    public void ShowEquipmentHover(EquipmentSlotUI equipmentSlot, Vector2 screenPosition)
    {
        if (pinned || equipmentSlot == null || equipmentSlot.CurrentItem == null)
            return;
        SetSource(equipmentSlot);
        Render(false);
        PositionAt(screenPosition);
        SetVisible(true);
    }

    public void MoveEquipmentPointer(EquipmentSlotUI equipmentSlot, Vector2 screenPosition)
    {
        if (!pinned && equipmentSource == equipmentSlot && IsVisible)
            PositionAt(screenPosition);
    }

    public void HideEquipmentHover(EquipmentSlotUI equipmentSlot)
    {
        if (!pinned && equipmentSource == equipmentSlot)
            HideImmediate();
    }

    public void PinEquipment(EquipmentSlotUI equipmentSlot, Vector2 screenPosition)
    {
        if (equipmentSlot == null || equipmentSlot.CurrentItem == null)
            return;
        SetSource(equipmentSlot);
        pinned = true;
        Render(true);
        PositionAt(screenPosition);
        SetVisible(true);
    }

    public void RefreshEquipmentSource(EquipmentSlotUI equipmentSlot)
    {
        if (equipmentSource != equipmentSlot)
            return;
        if (equipmentSlot.CurrentItem == null)
            HideImmediate();
        else
            Render(pinned);
    }

    public void InvalidateEquipmentSource(EquipmentSlotUI equipmentSlot)
    {
        if (equipmentSource == equipmentSlot)
            HideImmediate();
    }

    public void HideImmediate()
    {
        pinned = false;
        source = null;
        equipmentSource = null;
        SetVisible(false);
    }

    private void SetSource(ItemSlot itemSlot)
    {
        source = itemSlot;
        equipmentSource = null;
    }

    private void SetSource(EquipmentSlotUI equipmentSlot)
    {
        equipmentSource = equipmentSlot;
        source = null;
    }

    private void Render(bool actionMode)
    {
        ItemData item = CurrentItem;
        if (item == null)
            return;

        titleText.text = string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        typeText.text = TypeLabel(item.type);
        statsText.text = BuildStats(item);
        // Rewritten on every open, so translate here rather than through LocalizedText.
        descriptionText.text = string.IsNullOrWhiteSpace(item.description)
            ? Localization.Translate("No description available.")
            : item.description;
        promptText.text = Localization.Translate(actionMode
            ? equipmentSource != null ? "[E] Unequip    [Q] Cancel"
            : item.IsEquippable ? "[E] Equip    [Q] Cancel"
            : item.type == ItemType.Potion ? "[E] Use    [Q] Cancel" : "[Q] Close"
            : "Click for actions");
    }

    private static string BuildStats(ItemData item)
    {
        StringBuilder result = new StringBuilder();
        if (item.attackBonus > 0f)
            result.Append("ATK ").Append(item.attackBonus.ToString("0.#"));
        if (item.defenseBonus > 0f)
        {
            if (result.Length > 0) result.Append("    ");
            result.Append("DEF ").Append(item.defenseBonus.ToString("0.#"));
        }
        if (result.Length == 0)
            result.Append(item.type == ItemType.Potion ? "Restores HP to full"
                : item.type == ItemType.Ammunition ? "Stackable ranged ammunition"
                : item.IsEquippable ? "No stat bonus" : "Stackable item");
        return result.ToString();
    }

    private bool TryUsePotion(ItemData item)
    {
        HeroHealth hero = FindAnyObjectByType<HeroHealth>();
        if (hero == null || hero.IsDead || hero.CurrentHealth >= hero.MaximumHealth || RunInventory.Count(item) <= 0)
            return false;
        if (!RunInventory.Remove(item, 1))
            return false;
        hero.RestoreFullHealth();
        HideImmediate();
        return true;
    }

    private static string TypeLabel(ItemType type)
    {
        return type switch
        {
            ItemType.Weapon => "WEAPON",
            ItemType.Armor => "ARMOR",
            ItemType.Accessory => "ACCESSORY",
            ItemType.Potion => "POTION",
            ItemType.Ammunition => "AMMUNITION",
            ItemType.Material => "MATERIAL",
            ItemType.KeyItem => "KEY ITEM",
            _ => type.ToString().ToUpperInvariant()
        };
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        if (visible)
            panelRect.SetAsLastSibling();
    }

    private void PositionAt(Vector2 screenPosition)
    {
        if (panelRect == null || canvasRect == null || ownerCanvas == null)
            return;
        Camera camera = ownerCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ownerCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, camera, out Vector2 local))
            return;

        local += pointerOffset;
        Rect bounds = canvasRect.rect;
        Vector2 size = panelRect.rect.size;
        local.x = Mathf.Clamp(local.x, bounds.xMin, bounds.xMax - size.x);
        local.y = Mathf.Clamp(local.y, bounds.yMin + size.y, bounds.yMax);
        panelRect.anchoredPosition = local;
    }
}
