using UnityEngine;

/// <summary>
/// Defines which enemy unit ids appear for a given encounter id (from exploration / risky ground).
/// </summary>
[CreateAssetMenu(fileName = "NewEncounter", menuName = "Combat/Encounter Definition")]
public class EncounterDefinition : ScriptableObject
{
    [SerializeField] private int encounterId;
    [SerializeField] private int enemyUnitId0 = 10;
    [SerializeField] private int enemyUnitId1 = 11;
    [SerializeField] private int enemyUnitId2 = 12;

    public int EncounterId => encounterId;

    public int GetEnemyUnitId(int slotIndex)
    {
        return slotIndex switch
        {
            0 => enemyUnitId0,
            1 => enemyUnitId1,
            2 => enemyUnitId2,
            _ => 0
        };
    }
}
