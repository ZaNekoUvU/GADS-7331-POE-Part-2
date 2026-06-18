using System;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public const int MaxSlots = 8;
    public const int MaxStack = 10;

    [Serializable]
    public struct Slot
    {
        public ItemDefinition item;
        public int count;

        public readonly bool IsEmpty => item == null || count <= 0;
    }

    private readonly Slot[] _slots = new Slot[MaxSlots];

    public event Action OnChanged;
    public event Action<ItemDefinition, int> OnItemAdded;

    public enum ItemAddContext
    {
        Pickup,
        Gather
    }

    /// <summary>Returns how many items could not be added (0 if all fit).</summary>
    public int TryAdd(ItemDefinition item, int amount, ItemAddContext context = ItemAddContext.Pickup, string sourceDetail = null)
    {
        if (item == null || amount <= 0)
            return amount;

        var remaining = amount;
        var added = 0;

        for (var i = 0; i < MaxSlots; i++)
        {
            var slot = _slots[i];
            if (slot.IsEmpty || slot.item != item)
                continue;

            var space = MaxStack - slot.count;
            if (space <= 0)
                continue;

            var add = Mathf.Min(space, remaining);
            slot.count += add;
            _slots[i] = slot;
            remaining -= add;
            added += add;

            if (remaining <= 0)
            {
                NotifyItemAdded(item, added, context, sourceDetail);
                return 0;
            }
        }

        while (remaining > 0)
        {
            var idx = FindFirstEmptySlot();
            if (idx < 0)
                break;

            var add = Mathf.Min(MaxStack, remaining);
            _slots[idx] = new Slot { item = item, count = add };
            remaining -= add;
            added += add;
        }

        if (added > 0)
            NotifyItemAdded(item, added, context, sourceDetail);

        return remaining;
    }

    public ReadOnlySpan<Slot> GetSlots() => _slots;

    public int CountItem(ItemDefinition item)
    {
        if (item == null)
            return 0;

        var n = 0;
        for (var i = 0; i < MaxSlots; i++)
        {
            var slot = _slots[i];
            if (slot.IsEmpty || slot.item != item)
                continue;
            n += slot.count;
        }

        return n;
    }

    /// <summary>Counts stacked units across any slot whose <see cref="ItemDefinition.ItemId"/> matches (handles duplicate SO instances).</summary>
    public int CountItemWithId(int itemId)
    {
        if (itemId <= 0)
            return 0;

        var n = 0;
        for (var i = 0; i < MaxSlots; i++)
        {
            var slot = _slots[i];
            if (slot.IsEmpty || slot.item == null || slot.item.ItemId != itemId)
                continue;
            n += slot.count;
        }

        return n;
    }

    /// <summary>Removes up to <paramref name="amount"/> of item; returns how many were removed.</summary>
    public int TryRemove(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
            return 0;

        var toRemove = amount;
        var removed = 0;

        for (var i = 0; i < MaxSlots && toRemove > 0; i++)
        {
            var slot = _slots[i];
            if (slot.IsEmpty || slot.item != item)
                continue;

            var take = Mathf.Min(slot.count, toRemove);
            slot.count -= take;
            removed += take;
            toRemove -= take;

            if (slot.count <= 0)
                slot = default;

            _slots[i] = slot;
        }

        if (removed > 0)
            NotifyChanged();

        return removed;
    }

    /// <summary>Removes up to <paramref name="amount"/> across stacks that match <paramref name="itemId"/>.</summary>
    public int TryRemoveItemWithId(int itemId, int amount)
    {
        if (itemId <= 0 || amount <= 0)
            return 0;

        var toRemove = amount;
        var removed = 0;

        for (var i = 0; i < MaxSlots && toRemove > 0; i++)
        {
            var slot = _slots[i];
            if (slot.IsEmpty || slot.item == null || slot.item.ItemId != itemId)
                continue;

            var take = Mathf.Min(slot.count, toRemove);
            slot.count -= take;
            removed += take;
            toRemove -= take;

            if (slot.count <= 0)
                slot = default;

            _slots[i] = slot;
        }

        if (removed > 0)
            NotifyChanged();

        return removed;
    }

    /// <summary>Removes all stacks (e.g. after selling to the blacksmith).</summary>
    public void ClearAll()
    {
        for (var i = 0; i < MaxSlots; i++)
            _slots[i] = default;

        NotifyChanged();
    }

    private int FindFirstEmptySlot()
    {
        for (var i = 0; i < MaxSlots; i++)
        {
            if (_slots[i].IsEmpty)
                return i;
        }

        return -1;
    }

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }

    private void NotifyItemAdded(ItemDefinition item, int amountAdded, ItemAddContext context, string sourceDetail)
    {
        OnChanged?.Invoke();
        if (item == null || amountAdded <= 0)
            return;

        var prefix = context == ItemAddContext.Gather ? "[Gather]" : "[Pickup]";
        var name = string.IsNullOrWhiteSpace(item.DisplayName) ? item.name : item.DisplayName.Trim();
        var detail = string.IsNullOrWhiteSpace(sourceDetail) ? string.Empty : $" from '{sourceDetail.Trim()}'";
        Debug.Log($"{prefix} +{amountAdded} {name}{detail} (total in bag: {CountItem(item)}).", this);

        OnItemAdded?.Invoke(item, amountAdded);
    }
}
