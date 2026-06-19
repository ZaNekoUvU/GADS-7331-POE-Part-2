using UnityEngine;

/// <summary>
/// Static identity for a combatant: stats and move ids. Referenced by <see cref="CombatSession"/> party ids and encounter tables.
/// </summary>
[CreateAssetMenu(fileName = "NewUnit", menuName = "Combat/Unit Definition")]
public class UnitDefinition : ScriptableObject
{
    [SerializeField] private int unitId;
    [SerializeField] private string displayName = "Unit";
    [SerializeField] private int maxHp = 30;
    [Tooltip("Default physical attack power when using a basic strike.")]
    [SerializeField] private int attackDamage = 8;
    [Tooltip("Move ids this unit can use (resolve via MoveRegistry).")]
    [SerializeField] private int[] moveIds = { 100 };
    [SerializeField] private MoveDefinition[] moves;
    [Tooltip("Optional battle sprite for allies that are not mercenary offers (e.g. the hero).")]
    [SerializeField] private Sprite battleSprite;

    public int UnitId => unitId;
    public Sprite BattleSprite => battleSprite;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public int MaxHp => maxHp;
    public int AttackDamage => attackDamage;
    public int[] MoveIds => moveIds;
    public MoveDefinition[] Moves => moves;

    /// <summary>Effective damage for a basic strike using the registry when move ids are set.</summary>
    public int GetBasicStrikeDamage(MoveRegistry registry)
    {
        var best = attackDamage;
        if (registry != null && moveIds != null)
        {
            foreach (var mid in moveIds)
            {
                if (registry.TryGet(mid, out var mv))
                    best = Mathf.Max(best, mv.BaseDamage);
            }
        }

        return best;
    }
}
