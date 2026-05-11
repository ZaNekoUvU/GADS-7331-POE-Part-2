using System;

/// <summary>
/// Static handoff between exploration and the additively loaded combat scene.
/// Ally slots use unit definition ids (slot 0 = player, typically id <see cref="PlayerUnitId"/>).
/// </summary>
public static class CombatSession
{
    /// <summary>Reserved unit definition id for the player character.</summary>
    public const int PlayerUnitId = 1;

    private static readonly int[] AllyUnitIds = { 1, 0, 0 };

    public static int EncounterId { get; set; }

    /// <summary>Copy of ally slot unit ids [player, companion, companion].</summary>
    public static int[] GetAllyUnitIds() => (int[])AllyUnitIds.Clone();

    public static void SetAllyParty(int slot0PlayerOrLead, int slot1, int slot2)
    {
        AllyUnitIds[0] = slot0PlayerOrLead;
        AllyUnitIds[1] = slot1;
        AllyUnitIds[2] = slot2;
    }

    public static void ResetAllyPartyDefaults()
    {
        AllyUnitIds[0] = PlayerUnitId;
        AllyUnitIds[1] = 0;
        AllyUnitIds[2] = 0;
    }

    /// <summary>Fired after the combat scene has fully unloaded.</summary>
    public static event Action CombatEnded;

    public static void RaiseCombatEnded()
    {
        CombatEnded?.Invoke();
    }

    public static void Clear()
    {
        EncounterId = 0;
        ResetAllyPartyDefaults();
    }
}
