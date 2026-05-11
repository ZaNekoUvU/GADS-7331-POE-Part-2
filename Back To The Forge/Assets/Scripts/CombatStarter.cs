using UnityEngine;

/// <summary>
/// Optional helper: sets <see cref="CombatSession"/> data and calls <see cref="CombatAdditiveCoordinator.BeginCombat"/>.
/// Add to any GameObject and wire the coordinator (or leave empty to find one in the scene).
/// </summary>
public class CombatStarter : MonoBehaviour
{
    [SerializeField] private CombatAdditiveCoordinator coordinator;
    [SerializeField] private int defaultEncounterId;

    /// <summary>Use from UnityEvent (e.g. UI Button) with no argument.</summary>
    public void StartFight()
    {
        StartFightWithId(defaultEncounterId);
    }

    /// <summary>Use from code or UnityEvent with int payload if you use a custom caller.</summary>
    public void StartFightWithId(int encounterId)
    {
        CombatSession.EncounterId = encounterId;

        var party = FindAnyObjectByType<ExplorationCombatParty>();
        if (party != null)
            party.ApplyToCombatSession();
        else
            CombatSession.ResetAllyPartyDefaults();

        if (coordinator == null)
            coordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();

        if (coordinator == null)
        {
            Debug.LogError($"{nameof(CombatStarter)}: No {nameof(CombatAdditiveCoordinator)} in scene.", this);
            return;
        }

        coordinator.BeginCombat();
    }
}
