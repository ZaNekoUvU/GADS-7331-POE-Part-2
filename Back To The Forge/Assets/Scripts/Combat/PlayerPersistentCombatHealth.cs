using UnityEngine;

/// <summary>
/// Persists the player's HP between combat encounters (exploration object). Assign the same
/// <see cref="UnitDefinition"/> used for the player in combat, or set <see cref="fallbackMaxHp"/>.
/// </summary>
public sealed class PlayerPersistentCombatHealth : MonoBehaviour
{
    [SerializeField] private UnitDefinition playerUnitDefinition;
    [SerializeField] private int fallbackMaxHp = 40;

    private int _currentHp;

    public int MaxHp =>
        playerUnitDefinition != null ? Mathf.Max(1, playerUnitDefinition.MaxHp) : Mathf.Max(1, fallbackMaxHp);

    public int CurrentHp => _currentHp;

    private void Awake()
    {
        _currentHp = MaxHp;
    }

    /// <summary>HP passed into <see cref="CombatUnit.Initialize"/> for slot 0 player.</summary>
    public int GetHpForCombatStart(int definitionMaxHp)
    {
        var max = Mathf.Max(1, definitionMaxHp);
        if (_currentHp <= 0)
            _currentHp = max;

        return Mathf.Clamp(_currentHp, 1, max);
    }

    /// <summary>Call after combat (victory) so the next fight starts at this HP.</summary>
    public void RecordHpAfterCombat(int hp)
    {
        _currentHp = Mathf.Max(0, hp);
    }

    /// <summary>Typically after ending the day at the forge.</summary>
    public void ResetToFullHealth()
    {
        _currentHp = MaxHp;
    }
}
