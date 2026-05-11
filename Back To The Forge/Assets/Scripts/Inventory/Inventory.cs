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

    /// <summary>Returns how many items could not be added (0 if all fit).</summary>
    public int TryAdd(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
            return amount;

        var remaining = amount;
        var changed = false;

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
            changed = true;

            if (remaining <= 0)
            {
                if (changed)
                    NotifyChanged();
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
            changed = true;
        }

        if (changed)
            NotifyChanged();

        return remaining;
    }

    public ReadOnlySpan<Slot> GetSlots() => _slots;

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
}
