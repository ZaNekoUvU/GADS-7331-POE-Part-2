using UnityEngine;

/// <summary>
/// Exploration-side party ids passed into combat. Slot 0 is always the player unit id (<see cref="CombatSession.PlayerUnitId"/>).
/// Companion slots use 0 for empty.
/// </summary>
public class ExplorationCombatParty : MonoBehaviour
{
    [Tooltip("Companion unit definition id for ally slot 1. 0 = empty.")]
    [SerializeField] private int companionSlot1UnitId;
    [Tooltip("Companion unit definition id for ally slot 2. 0 = empty.")]
    [SerializeField] private int companionSlot2UnitId;

    /// <summary>Writes ally ids into <see cref="CombatSession"/> before a fight loads.</summary>
    public void ApplyToCombatSession()
    {
        CombatSession.SetAllyParty(
            CombatSession.PlayerUnitId,
            companionSlot1UnitId,
            companionSlot2UnitId);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (companionSlot1UnitId < 0)
            companionSlot1UnitId = 0;
        if (companionSlot2UnitId < 0)
            companionSlot2UnitId = 0;
    }
#endif
}
