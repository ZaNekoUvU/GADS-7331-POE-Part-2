using UnityEngine;

/// <summary>
/// Exploration-side party ids passed into combat. Slot 0 is always the player unit id (<see cref="CombatSession.PlayerUnitId"/>).
/// Companion slots use 0 for empty. Up to three companions (party size 4).
/// </summary>
public class ExplorationCombatParty : MonoBehaviour
{
    [Tooltip("Companion unit definition id for ally slot 1. 0 = empty.")]
    [SerializeField] private int companionSlot1UnitId;
    [Tooltip("Companion unit definition id for ally slot 2. 0 = empty.")]
    [SerializeField] private int companionSlot2UnitId;
    [Tooltip("Companion unit definition id for ally slot 3. 0 = empty.")]
    [SerializeField] private int companionSlot3UnitId;

    /// <summary>Writes ally ids into <see cref="CombatSession"/> before a fight loads.</summary>
    public void ApplyToCombatSession()
    {
        var s1 = companionSlot1UnitId;
        var s2 = companionSlot2UnitId;
        var s3 = companionSlot3UnitId;
        var hired = HiredCompanionManager.Instance;
        if (hired != null)
        {
            if (hired.Slot1UnitId > 0)
                s1 = hired.Slot1UnitId;
            if (hired.Slot2UnitId > 0)
                s2 = hired.Slot2UnitId;
            if (hired.Slot3UnitId > 0)
                s3 = hired.Slot3UnitId;
        }

        CombatSession.SetAllyParty(
            CombatSession.PlayerUnitId,
            s1,
            s2,
            s3);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (companionSlot1UnitId < 0)
            companionSlot1UnitId = 0;
        if (companionSlot2UnitId < 0)
            companionSlot2UnitId = 0;
        if (companionSlot3UnitId < 0)
            companionSlot3UnitId = 0;
    }
#endif
}
