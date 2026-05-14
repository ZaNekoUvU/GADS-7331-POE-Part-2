using System;
using UnityEngine;

/// <summary>
/// Runtime hired allies (max three companions; player is separate). Cleared when the calendar advances.
/// Drives <see cref="ExplorationCombatParty"/> via <see cref="ExplorationCombatParty.ApplyToCombatSession"/>.
/// </summary>
public sealed class HiredCompanionManager : MonoBehaviour
{
    public static HiredCompanionManager Instance { get; private set; }

    public const int MaxCompanionSlots = 3;

    /// <summary>Ally combat index 1. 0 = empty.</summary>
    public int Slot1UnitId { get; private set; }

    /// <summary>Ally combat index 2. 0 = empty.</summary>
    public int Slot2UnitId { get; private set; }

    /// <summary>Ally combat index 3. 0 = empty.</summary>
    public int Slot3UnitId { get; private set; }

    public event Action OnRosterChanged;

    /// <summary>
    /// Exploration objects (e.g. a <see cref="CompanionRecruiter"/>) that should follow the player instead of a spawned registry prefab.
    /// Cleared when the slot empties or the recruiter returns home.
    /// </summary>
    private readonly GameObject[] _physicalFollowerRootsBySlot = new GameObject[MaxCompanionSlots];

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static HiredCompanionManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<HiredCompanionManager>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(HiredCompanionManager)}]");
        return go.AddComponent<HiredCompanionManager>();
    }

    /// <summary>Hires or replaces companion slot 0..2 (maps to party slots 1..3). Charges gold on success.</summary>
    /// <param name="physicalFollowerRoot">
    /// If set (e.g. the mercenary NPC), bound before <see cref="OnRosterChanged"/> so presenters skip duplicate spawns.
    /// </param>
    public bool TryHire(
        int companionSlotIndex,
        int unitId,
        int cost,
        BlacksmithMaster payFrom,
        GameObject physicalFollowerRoot = null)
    {
        if (unitId <= 0)
            return false;

        if ((uint)companionSlotIndex >= MaxCompanionSlots)
            return false;

        if (payFrom == null || !payFrom.TrySpendGold(cost))
            return false;

        switch (companionSlotIndex)
        {
            case 0:
                Slot1UnitId = unitId;
                break;
            case 1:
                Slot2UnitId = unitId;
                break;
            default:
                Slot3UnitId = unitId;
                break;
        }

        if (physicalFollowerRoot != null)
            BindPhysicalFollowerToSlot(companionSlotIndex, physicalFollowerRoot);

        OnRosterChanged?.Invoke();
        return true;
    }

    public void BindPhysicalFollowerToSlot(int companionSlotIndex0To2, GameObject root)
    {
        if ((uint)companionSlotIndex0To2 >= MaxCompanionSlots || root == null)
            return;

        _physicalFollowerRootsBySlot[companionSlotIndex0To2] = root;
    }

    public void UnbindPhysicalFollowerSlot(int companionSlotIndex0To2)
    {
        if ((uint)companionSlotIndex0To2 >= MaxCompanionSlots)
            return;

        _physicalFollowerRootsBySlot[companionSlotIndex0To2] = null;
    }

    /// <summary>Optional world root that follows the player for this slot (mercenary NPC). Null if using presenter spawns.</summary>
    public GameObject GetPhysicalFollowerRoot(int companionSlotIndex0To2)
    {
        if ((uint)companionSlotIndex0To2 >= MaxCompanionSlots)
            return null;

        return _physicalFollowerRootsBySlot[companionSlotIndex0To2];
    }

    public void ClearHiresForNewDay()
    {
        if (Slot1UnitId == 0 && Slot2UnitId == 0 && Slot3UnitId == 0)
            return;

        Slot1UnitId = 0;
        Slot2UnitId = 0;
        Slot3UnitId = 0;
        // Physical followers are unbound when CompanionRecruiter restores after this event.
        OnRosterChanged?.Invoke();
    }

    /// <summary>Unit id in companion slot 0..2 (party indices 1..3). 0 if empty.</summary>
    public int GetCompanionSlotUnitId(int companionSlotIndex0To2)
    {
        switch (companionSlotIndex0To2)
        {
            case 0:
                return Slot1UnitId;
            case 1:
                return Slot2UnitId;
            case 2:
                return Slot3UnitId;
            default:
                return 0;
        }
    }
}
