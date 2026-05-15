using UnityEngine;

[CreateAssetMenu(fileName = "NewMove", menuName = "Combat/Move Definition")]
public class MoveDefinition : ScriptableObject
{
    [SerializeField] private int moveId;
    [SerializeField] private string displayName = "Strike";
    [Tooltip("Base damage before unit stats. 0 = use the unit's attack stat.")]
    [SerializeField] private int baseDamage = 5;
    [Tooltip("Multiplier applied after base damage is resolved (1 = normal, 1.5 = Power Strike).")]
    [SerializeField] private float damageMultiplier = 1f;

    public int MoveId => moveId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public int BaseDamage => baseDamage;
    public float DamageMultiplier => damageMultiplier > 0f ? damageMultiplier : 1f;
}
