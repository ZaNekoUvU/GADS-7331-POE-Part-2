using UnityEngine;

[CreateAssetMenu(fileName = "NewMove", menuName = "Combat/Move Definition")]
public class MoveDefinition : ScriptableObject
{
    [SerializeField] private int moveId;
    [SerializeField] private string displayName = "Strike";
    [Tooltip("Base damage before unit stats.")]
    [SerializeField] private int baseDamage = 5;

    public int MoveId => moveId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public int BaseDamage => baseDamage;
}
