using System;

/// <summary>
/// Static handoff between exploration and the additively loaded combat scene.
/// Set values before calling <see cref="CombatAdditiveCoordinator.BeginCombat"/>.
/// </summary>
public static class CombatSession
{
    public static int EncounterId { get; set; }

    /// <summary>Fired after the combat scene has fully unloaded.</summary>
    public static event Action CombatEnded;

    public static void RaiseCombatEnded()
    {
        CombatEnded?.Invoke();
    }

    public static void Clear()
    {
        EncounterId = 0;
    }
}
