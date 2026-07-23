using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One stack in the backpack: an item type plus how many are held.</summary>
[Serializable]
public sealed class InventoryStack
{
    public ItemData item;
    public int count;

    public InventoryStack(ItemData item, int count)
    {
        this.item = item;
        this.count = count;
    }
}

/// <summary>
/// 全局背包数据模型（方案 B）。用静态列表存放，因此**跨场景保留**——地图里获得的
/// 物品（含金币）会一直带到 Boss 场景。所有物品都是这里的一员，按**获得顺序**排列：
/// 新类型追加到末尾，同类型自动堆叠。
///
/// - UI（InventoryPanel）只负责渲染本模型，并订阅 Changed 刷新。
/// - 金币也是普通物品：PlayerProgression 用 Count/Add/Remove 读写它。
/// - 拖拽换位调用 Move(from, to)。
///
/// 静态字段在同一次 Play 会话内跨场景加载保留；新的一局由 PlayerProgression
/// 的 resetRunOnAwake 触发 Reset() 清空。
/// </summary>
public static class RunInventory
{
    private static readonly List<InventoryStack> stacks = new List<InventoryStack>();

    /// <summary>Occupied stacks, in acquisition order (read-only).</summary>
    public static IReadOnlyList<InventoryStack> Stacks => stacks;

    /// <summary>Raised whenever contents or order change; the UI subscribes to re-render.</summary>
    public static event Action Changed;

    public static int Count(ItemData item)
    {
        if (item == null)
            return 0;
        foreach (InventoryStack stack in stacks)
            if (stack.item == item)
                return stack.count;
        return 0;
    }

    /// <summary>Adds items. Same type stacks; a new type is appended to the end (acquisition order).</summary>
    public static void Add(ItemData item, int count = 1)
    {
        if (item == null || count <= 0)
            return;
        foreach (InventoryStack stack in stacks)
        {
            if (stack.item == item)
            {
                stack.count += count;
                Changed?.Invoke();
                return;
            }
        }
        stacks.Add(new InventoryStack(item, count));
        Changed?.Invoke();
    }

    /// <summary>Removes items. Returns false if there aren't enough. Empty stacks are dropped.</summary>
    public static bool Remove(ItemData item, int count = 1)
    {
        if (item == null || count <= 0)
            return false;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i].item != item)
                continue;
            if (stacks[i].count < count)
                return false;
            stacks[i].count -= count;
            if (stacks[i].count <= 0)
                stacks.RemoveAt(i);
            Changed?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>Reorders one stack to a new position — the drag-and-drop "move item" operation.</summary>
    public static void Move(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= stacks.Count)
            return;
        toIndex = Mathf.Clamp(toIndex, 0, stacks.Count - 1);
        if (fromIndex == toIndex)
            return;
        InventoryStack moved = stacks[fromIndex];
        stacks.RemoveAt(fromIndex);
        stacks.Insert(toIndex, moved);
        Changed?.Invoke();
    }

    /// <summary>Clears the whole run (start a fresh game).</summary>
    public static void Reset()
    {
        if (stacks.Count == 0)
            return;
        stacks.Clear();
        Changed?.Invoke();
    }

    // Statics survive scene loads but not the very first entry into Play; make sure a fresh
    // Play session starts empty (and re-arm the event field).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnFreshPlay()
    {
        stacks.Clear();
        Changed = null;
    }
}
