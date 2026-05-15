using UnityEngine;

/// <summary>
/// Default item pool for post-combat random drops. Loaded from Resources when the scene pool is empty.
/// </summary>
[CreateAssetMenu(fileName = "VictoryDropPool", menuName = "Combat/Victory Drop Pool")]
public class CombatVictoryDropPool : ScriptableObject
{
    [SerializeField] private ItemDefinition[] items;

    public ItemDefinition[] Items => items;
}
