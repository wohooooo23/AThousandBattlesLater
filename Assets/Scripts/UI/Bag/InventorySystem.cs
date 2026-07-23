using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One rendered inventory stack: drag/drop plus hover and keyboard item details.</summary>
public sealed class ItemSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler, IPointerClickHandler
{
    [Header("Visuals")]
    public Image mIcon;
    public Text mCountText;

    [Header("Runtime data")]
    public ItemData mItem;
    public int mCount;

    public int SlotIndex { get; set; }

    private static ItemSlot dragSource;
    private static GameObject dragGhost;

    public static ItemSlot DragSource => dragSource;

    public void SetItem(ItemData item)
    {
        mItem = item;
        if (mIcon != null)
        {
            mIcon.sprite = item != null ? item.icon : null;
            mIcon.enabled = item != null;
        }
        ItemDetailPanel.Instance?.RefreshSource(this);
    }

    public void RefreshCount()
    {
        if (mCountText != null)
            mCountText.text = mCount > 1 ? mCount.ToString() : string.Empty;
    }

    public void Clear()
    {
        ItemDetailPanel.Instance?.InvalidateSource(this);
        mItem = null;
        mCount = 0;
        if (mIcon != null)
        {
            mIcon.sprite = null;
            mIcon.enabled = false;
        }
        if (mCountText != null)
            mCountText.text = string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (mItem != null)
            ItemDetailPanel.Instance?.ShowHover(this, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        ItemDetailPanel.Instance?.MovePointer(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ItemDetailPanel.Instance?.HideHover(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        if (mItem == null)
        {
            ItemDetailPanel.Instance?.HideImmediate();
            return;
        }
        ItemDetailPanel.Instance?.Pin(this, eventData.position);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (mItem == null)
            return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        ItemDetailPanel.Instance?.HideImmediate();
        dragSource = this;
        dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        RectTransform ghostRect = (RectTransform)dragGhost.transform;
        ghostRect.SetParent(canvas.transform, false);
        ghostRect.SetAsLastSibling();
        ghostRect.anchorMin = ghostRect.anchorMax = ghostRect.pivot = new Vector2(0.5f, 0.5f);
        ghostRect.sizeDelta = ((RectTransform)transform).rect.size;
        ghostRect.position = eventData.position;

        Image ghostImage = dragGhost.GetComponent<Image>();
        ghostImage.sprite = mItem.icon;
        ghostImage.raycastTarget = false;
        ghostImage.color = new Color(1f, 1f, 1f, 0.75f);
        dragGhost.GetComponent<CanvasGroup>().blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
            dragGhost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null)
            Destroy(dragGhost);
        dragGhost = null;
        dragSource = null;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (dragSource != null && dragSource != this)
            RunInventory.Move(dragSource.SlotIndex, SlotIndex);
    }
}
