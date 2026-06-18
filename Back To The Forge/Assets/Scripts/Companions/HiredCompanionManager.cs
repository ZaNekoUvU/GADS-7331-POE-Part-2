using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime hired allies (max three companions; player is separate). Cleared when the calendar advances.
/// </summary>
public sealed class HiredCompanionManager : MonoBehaviour
{
    public static HiredCompanionManager Instance { get; private set; }

    public const int MaxCompanionSlots = 3;

    public int Slot1UnitId { get; private set; }
    public int Slot2UnitId { get; private set; }
    public int Slot3UnitId { get; private set; }

    public event Action OnRosterChanged;

    private readonly GameObject[] _physicalFollowerRootsBySlot = new GameObject[MaxCompanionSlots];
    private readonly Dictionary<int, CompanionMoraleState> _moraleByUnitId = new();

    public const float MinPartyAttackMultiplier = 0.55f;
    public const float MaxPartyAttackMultiplier = 1.45f;

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

    public int CountHired()
    {
        var n = 0;
        if (Slot1UnitId > 0) n++;
        if (Slot2UnitId > 0) n++;
        if (Slot3UnitId > 0) n++;
        return n;
    }

    public bool IsPartyFull => CountHired() >= MaxCompanionSlots;

    public int FindSlotWithUnitId(int unitId)
    {
        if (unitId <= 0)
            return -1;

        if (Slot1UnitId == unitId) return 0;
        if (Slot2UnitId == unitId) return 1;
        if (Slot3UnitId == unitId) return 2;
        return -1;
    }

    public int FindFirstEmptySlot()
    {
        if (Slot1UnitId <= 0) return 0;
        if (Slot2UnitId <= 0) return 1;
        if (Slot3UnitId <= 0) return 2;
        return -1;
    }

    public int FindSlotForPhysicalRoot(GameObject root)
    {
        if (root == null)
            return -1;

        for (var i = 0; i < MaxCompanionSlots; i++)
        {
            if (_physicalFollowerRootsBySlot[i] == root)
                return i;
        }

        return -1;
    }

    /// <summary>Hires into the first empty slot, or replaces that mercenary's existing slot if already rostered.</summary>
    public bool TryHireAuto(
        int unitId,
        int cost,
        BlacksmithMaster payFrom,
        GameObject physicalFollowerRoot,
        out int assignedSlotIndex)
    {
        assignedSlotIndex = -1;

        if (unitId <= 0)
            return false;

        var existing = FindSlotWithUnitId(unitId);
        if (existing >= 0)
        {
            assignedSlotIndex = existing;
        }
        else
        {
            assignedSlotIndex = FindFirstEmptySlot();
            if (assignedSlotIndex < 0)
                return false;
        }

        return TryHire(assignedSlotIndex, unitId, cost, payFrom, physicalFollowerRoot);
    }

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

        ClearUnitFromOtherSlots(unitId, companionSlotIndex);

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

        EnsureMoraleState(unitId);
        OnRosterChanged?.Invoke();
        return true;
    }

    public void ApplyCompanionDialogueResult(int unitId, CompanionDialogueDto dto)
    {
        if (unitId <= 0 || dto == null)
            return;

        MercenaryOfferLookup.TryGet(unitId, out var offer);
        var state = EnsureMoraleState(unitId);
        state.ApplyDialogueResult(dto, offer);
        OnRosterChanged?.Invoke();
    }

    public bool TryGetMoraleState(int unitId, out CompanionMoraleState state)
    {
        if (unitId > 0 && _moraleByUnitId.TryGetValue(unitId, out state))
            return true;

        state = null;
        return false;
    }

    /// <summary>Builds combat handoff from all hired mercenary morale skills.</summary>
    public void BuildCombatMoraleHandoff(
        out float partyAttackMultiplier,
        out int heroBonusManaRegen,
        out CompanionCombatMoraleHandoff[] companionSlots,
        out string summary)
    {
        partyAttackMultiplier = 1f;
        heroBonusManaRegen = 0;
        companionSlots = new CompanionCombatMoraleHandoff[MaxCompanionSlots];
        var parts = new List<string>(3);

        ApplySlotMorale(Slot1UnitId, 0, ref partyAttackMultiplier, ref heroBonusManaRegen, companionSlots, parts);
        ApplySlotMorale(Slot2UnitId, 1, ref partyAttackMultiplier, ref heroBonusManaRegen, companionSlots, parts);
        ApplySlotMorale(Slot3UnitId, 2, ref partyAttackMultiplier, ref heroBonusManaRegen, companionSlots, parts);

        partyAttackMultiplier = Mathf.Clamp(partyAttackMultiplier, MinPartyAttackMultiplier, MaxPartyAttackMultiplier);
        summary = parts.Count == 0 ? string.Empty : string.Join("; ", parts);
    }

    private void ApplySlotMorale(
        int unitId,
        int companionSlotIndex,
        ref float partyAttackMultiplier,
        ref int heroBonusManaRegen,
        CompanionCombatMoraleHandoff[] companionSlots,
        List<string> summaryParts)
    {
        if (unitId <= 0 || !_moraleByUnitId.TryGetValue(unitId, out var state) || !state.HasActiveSkill)
            return;

        var skill = state.ActiveSkill;
        var label = string.IsNullOrWhiteSpace(state.ActiveSkillLabel) ? skill.skillName : state.ActiveSkillLabel;
        var pct = Mathf.RoundToInt(skill.magnitude * 100f);

        ref var handoff = ref companionSlots[companionSlotIndex];
        handoff.UnitId = unitId;
        handoff.SkillLabel = label;

        switch (skill.effectKind)
        {
            case MercenaryMoraleEffectKind.PartyAttackUp:
                partyAttackMultiplier += skill.magnitude;
                summaryParts.Add($"{label} +{pct}% party ATK");
                break;
            case MercenaryMoraleEffectKind.PartyAttackDown:
                partyAttackMultiplier -= skill.magnitude;
                summaryParts.Add($"{label} -{pct}% party ATK");
                break;
            case MercenaryMoraleEffectKind.SelfAttackUp:
                handoff.SelfAttackMultiplier = 1f + skill.magnitude;
                summaryParts.Add($"{label} +{pct}% merc ATK");
                break;
            case MercenaryMoraleEffectKind.SelfAttackDown:
                handoff.SelfAttackMultiplier = Mathf.Max(0.5f, 1f - skill.magnitude);
                summaryParts.Add($"{label} -{pct}% merc ATK");
                break;
            case MercenaryMoraleEffectKind.SelfMaxHpUp:
                handoff.SelfMaxHpMultiplier = 1f + skill.magnitude;
                summaryParts.Add($"{label} +{pct}% merc HP");
                break;
            case MercenaryMoraleEffectKind.HeroManaRegenUp:
                heroBonusManaRegen += Mathf.RoundToInt(skill.magnitude);
                summaryParts.Add($"{label} +{Mathf.RoundToInt(skill.magnitude)} hero MP/turn");
                break;
        }
    }

    private CompanionMoraleState EnsureMoraleState(int unitId)
    {
        if (!_moraleByUnitId.TryGetValue(unitId, out var state))
        {
            state = new CompanionMoraleState(unitId);
            _moraleByUnitId[unitId] = state;
        }

        return state;
    }

    private void ClearUnitFromOtherSlots(int unitId, int keepSlot)
    {
        if (keepSlot != 0 && Slot1UnitId == unitId)
        {
            Slot1UnitId = 0;
            _physicalFollowerRootsBySlot[0] = null;
        }

        if (keepSlot != 1 && Slot2UnitId == unitId)
        {
            Slot2UnitId = 0;
            _physicalFollowerRootsBySlot[1] = null;
        }

        if (keepSlot != 2 && Slot3UnitId == unitId)
        {
            Slot3UnitId = 0;
            _physicalFollowerRootsBySlot[2] = null;
        }
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
        _moraleByUnitId.Clear();
        OnRosterChanged?.Invoke();
    }

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
