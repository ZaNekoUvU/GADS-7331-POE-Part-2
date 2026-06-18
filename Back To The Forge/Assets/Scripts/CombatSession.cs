using System;

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
    }
}
