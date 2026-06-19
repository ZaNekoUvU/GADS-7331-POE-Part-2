using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Static handoff between exploration and the additively loaded combat scene.
/// Ally slots use unit definition ids (slot 0 = player, typically id <see cref="PlayerUnitId"/>).
/// </summary>
public static class CombatSession
{
    /// <summary>Reserved unit definition id for the player character.</summary>
    public const int PlayerUnitId = 1;

    private static readonly int[] AllyUnitIds = { 1, 0, 0, 0 };

    public static int EncounterId { get; set; }

    /// <summary>Copy of ally slot unit ids [player, companion × 3].</summary>
    public static int[] GetAllyUnitIds() => (int[])AllyUnitIds.Clone();

    public static void SetAllyParty(int slot0PlayerOrLead, int slot1, int slot2, int slot3)
    {
        AllyUnitIds[0] = slot0PlayerOrLead;
        AllyUnitIds[1] = slot1;
        AllyUnitIds[2] = slot2;
        AllyUnitIds[3] = slot3;
    }

    public static void ResetAllyPartyDefaults()
    {
        AllyUnitIds[0] = PlayerUnitId;
        AllyUnitIds[1] = 0;
        AllyUnitIds[2] = 0;
        AllyUnitIds[3] = 0;
    }

    /// <summary>Set when all enemies are defeated; consumed when exploration applies victory loot.</summary>
    private static bool _victoryLootPending;

    public static void MarkVictoryLootPending() => _victoryLootPending = true;

    public static bool PeekVictoryLootPending() => _victoryLootPending;

    public static void ClearVictoryLootPending() => _victoryLootPending = false;

    /// <summary>Fired after the combat scene has fully unloaded.</summary>
    public static event Action CombatEnded;

    /// <summary>Party-wide attack multiplier from mercenary morale (1 = normal).</summary>
    public static float PartyAttackMultiplier { get; set; } = 1f;

    /// <summary>Extra hero mana regen per turn from mercenary morale skills.</summary>
    public static int HeroBonusManaRegen { get; set; }

    /// <summary>Morale handoff for companion ally slots 1–3 (index 0 = slot 1).</summary>
    public static CompanionCombatMoraleHandoff[] CompanionMoraleBySlot { get; set; } =
        new CompanionCombatMoraleHandoff[3];

    /// <summary>When set (risky-ground random fights), spawner uses these enemies instead of encounter table ids.</summary>
    private static RolledWildEncounter _rolledWildEncounter;

    public static bool HasRolledWildEncounter => _rolledWildEncounter != null && _rolledWildEncounter.Count > 0;

    public static RolledWildEncounter ActiveWildEncounter => _rolledWildEncounter;

    public static void SetRolledWildEncounter(RolledWildEncounter encounter) => _rolledWildEncounter = encounter;

    public static void ClearRolledWildEncounter() => _rolledWildEncounter = null;

    /// <summary>Short summary for combat log / HUD.</summary>
    public static string PartyMoraleSummary { get; set; } = string.Empty;

    public static void RaiseCombatEnded()
    {
        CombatEnded?.Invoke();
    }

    public static void Clear()
    {
        EncounterId = 0;
        ResetAllyPartyDefaults();
        _victoryLootPending = false;
        PartyAttackMultiplier = 1f;
        HeroBonusManaRegen = 0;
        PartyMoraleSummary = string.Empty;
        CompanionMoraleBySlot = new CompanionCombatMoraleHandoff[3];
        ClearRolledWildEncounter();
    }
}

/// <summary>
/// Result of rolling a risky-ground wild encounter (1–4 enemies, single type or mix).
/// Stored on <see cref="CombatSession"/> until combat ends.
/// </summary>
public sealed class RolledWildEncounter
{
    public const int MaxEnemies = 4;

    public struct Slot
    {
        public int UnitId;
        public string DisplayName;
        public Sprite BattleSprite;
        public string FlavorHint;
    }

    public int Count { get; private set; }

    private readonly int[] _unitIds = new int[MaxEnemies];
    private readonly string[] _displayNames = new string[MaxEnemies];
    private readonly Sprite[] _battleSprites = new Sprite[MaxEnemies];
    private readonly string[] _flavorHints = new string[MaxEnemies];

    public int GetUnitId(int slot) => slot >= 0 && slot < Count ? _unitIds[slot] : 0;

    public string GetDisplayName(int slot) => slot >= 0 && slot < Count ? _displayNames[slot] : string.Empty;

    public Sprite GetBattleSprite(int slot) => slot >= 0 && slot < Count ? _battleSprites[slot] : null;

    public static RolledWildEncounter Create(IReadOnlyList<Slot> slots)
    {
        if (slots == null || slots.Count == 0)
            return null;

        var rolled = new RolledWildEncounter();
        rolled.Count = Mathf.Clamp(slots.Count, 1, MaxEnemies);

        for (var i = 0; i < rolled.Count; i++)
        {
            var pick = slots[i];
            if (pick.UnitId <= 0)
                continue;

            rolled._unitIds[i] = pick.UnitId;
            rolled._displayNames[i] = pick.DisplayName ?? string.Empty;
            rolled._battleSprites[i] = pick.BattleSprite;
            rolled._flavorHints[i] = pick.FlavorHint ?? string.Empty;
        }

        return rolled;
    }

    public string BuildGroupSummary()
    {
        if (Count <= 0)
            return "hostile creatures";

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Count; i++)
        {
            var name = _displayNames[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            counts.TryGetValue(name, out var n);
            counts[name] = n + 1;
        }

        if (counts.Count == 0)
            return "hostile creatures";

        var parts = new List<string>(counts.Count);
        foreach (var pair in counts)
        {
            parts.Add(pair.Value == 1
                ? $"1 {pair.Key}"
                : $"{pair.Value} {Pluralize(pair.Key)}");
        }

        if (parts.Count == 1)
            return parts[0];

        if (parts.Count == 2)
            return $"{parts[0]} and {parts[1]}";

        var sb = new StringBuilder(parts[0]);
        for (var i = 1; i < parts.Count - 1; i++)
            sb.Append(", ").Append(parts[i]);
        sb.Append(", and ").Append(parts[parts.Count - 1]);
        return sb.ToString();
    }

    public string BuildFlavorContext()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sb = new StringBuilder();

        for (var i = 0; i < Count; i++)
        {
            var hint = _flavorHints[i]?.Trim();
            if (string.IsNullOrEmpty(hint) || !seen.Add(hint))
                continue;

            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(hint);
        }

        return sb.ToString();
    }

    public string PickPrimaryDisplayName()
    {
        for (var i = 0; i < Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(_displayNames[i]))
                return _displayNames[i];
        }

        return "enemies";
    }

    private static string Pluralize(string singular)
    {
        if (string.IsNullOrEmpty(singular))
            return singular;

        if (singular.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            return singular;

        return singular + "s";
    }
}
