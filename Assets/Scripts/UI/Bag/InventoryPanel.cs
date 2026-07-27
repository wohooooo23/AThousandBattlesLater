using System.Collections.Generic;
using UnityEngine;

// ============================================================
// InventoryPanel — 背包面板（方案 B：只负责渲染跨场景的 RunInventory）
//
// - Start 时按 mSlotCount 生成 ItemSlot 网格，并给每格编号 SlotIndex。
// - 订阅 RunInventory.Changed，按获得顺序把物品渲染到格子里；金币也是普通物品。
// - AddItem 只是转发给 RunInventory.Add（兼容 ItemPickup）。
// - 拖拽换位由 ItemSlot 直接调用 RunInventory.Move。
// ============================================================

public class InventoryPanel : MonoBehaviour
{
    [Header("拖拽绑定")]
    [Tooltip("挂 GridLayoutGroup 的格子容器")]
    public Transform mSlotGrid;

    [Tooltip("单个格子的 Prefab（ItemSlot.prefab）")]
    public GameObject mSlotPrefab;

    [Tooltip("总格子数")]
    public int mSlotCount = 20;

    [HideInInspector]
    public List<ItemSlot> mSlots = new List<ItemSlot>();

    public static InventoryPanel Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        for (int i = 0; i < mSlotCount; i++)
        {
            GameObject obj = Instantiate(mSlotPrefab, mSlotGrid);
            ItemSlot slot = obj.GetComponent<ItemSlot>();
            slot.SlotIndex = i;
            mSlots.Add(slot);
        }

        RunInventory.Changed += Render;
        RunProgress.Changed += Render;
        Render();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        RunInventory.Changed -= Render;
        RunProgress.Changed -= Render;
    }

    private void OnDisable()
    {
        ItemDetailPanel.Instance?.HideImmediate();
    }

    /// <summary>Renders RunInventory stacks (acquisition order) into the fixed slots.</summary>
    public void Render()
    {
        IReadOnlyList<InventoryStack> stacks = RunInventory.Stacks;
        for (int i = 0; i < mSlots.Count; i++)
        {
            if (i < stacks.Count && stacks[i].item != null)
            {
                mSlots[i].SetItem(stacks[i].item);
                mSlots[i].mCount = stacks[i].count;
                mSlots[i].RefreshCount();
            }
            else
            {
                mSlots[i].Clear();
            }
        }
    }

    /// <summary>Compatibility passthrough (e.g. ItemPickup). The model raises Changed → re-render.</summary>
    public bool AddItem(ItemData item, int count = 1)
    {
        if (item == null)
            return false;
        RunInventory.Add(item, count);
        return true;
    }
}
