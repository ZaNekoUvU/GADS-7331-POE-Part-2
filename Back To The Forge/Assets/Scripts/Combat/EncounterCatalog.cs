using UnityEngine;

/// <summary>
/// Lookup encounter id → <see cref="EncounterDefinition"/>.
/// </summary>
[CreateAssetMenu(fileName = "EncounterCatalog", menuName = "Combat/Encounter Catalog")]
public class EncounterCatalog : ScriptableObject
{
    [SerializeField] private EncounterDefinition[] encounters;

    public bool TryGet(int encounterId, out EncounterDefinition encounter)
    {
        if (encounters != null)
        {
            foreach (var e in encounters)
            {
                if (e != null && e.EncounterId == encounterId)
                {
                    encounter = e;
                    return true;
                }
            }
        }

        encounter = null;
        return false;
    }
}
