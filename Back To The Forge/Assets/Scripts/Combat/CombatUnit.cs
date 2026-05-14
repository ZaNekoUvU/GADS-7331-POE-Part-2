using System;
using UnityEngine;

/// <summary>
/// Runtime combatant instance spawned from a <see cref="UnitDefinition"/>.
/// </summary>
public class CombatUnit : MonoBehaviour
{
    /// <summary>Fired once when this unit is removed from combat (before <see cref="Object.Destroy"/> on enemies).</summary>
    public static event System.Action<CombatUnit> OnDefeated;

    [SerializeField] private SpriteRenderer spriteRenderer;

    private UnitDefinition _definition;
    private MoveRegistry _moveRegistry;
    private bool _isAlly;
    private bool _isPlayerCharacter;
    private int _slotIndex;
    private int _currentHp;
    private int _runtimeMaxHp;
    private int _runtimeEnemyStrikeDamage;
    private bool _enemyDifficultyApplied;

    public UnitDefinition Definition => _definition;
    public int CurrentHp => _currentHp;
    public int MaxHp
    {
        get
        {
            if (_definition == null)
                return 0;
            if (_enemyDifficultyApplied)
                return _runtimeMaxHp;
            return _definition.MaxHp;
        }
    }
    public bool IsAlive => _currentHp > 0;
    public bool IsAlly => _isAlly;
    public bool IsPlayerCharacter => _isPlayerCharacter;
    public int SlotIndex => _slotIndex;

    /// <summary>Args: current HP, max HP. Fired after <see cref="Initialize"/> and after <see cref="TakeDamage"/>.</summary>
    public event Action<int, int> HpChanged;

    public void Initialize(
        UnitDefinition definition,
        MoveRegistry moveRegistry,
        bool isAlly,
        bool isPlayerCharacter,
        int slotIndex,
        int? startingHpOverride = null)
    {
        _definition = definition;
        _moveRegistry = moveRegistry;
        _isAlly = isAlly;
        _isPlayerCharacter = isPlayerCharacter;
        _slotIndex = slotIndex;
        _enemyDifficultyApplied = false;
        _runtimeMaxHp = 0;
        _runtimeEnemyStrikeDamage = 0;

        if (definition == null)
        {
            _currentHp = 0;
        }
        else
        {
            var maxHpCap = definition.MaxHp;
            if (!isAlly)
            {
                var mul = CombatEnemyDifficulty.GetEnemyStatMultiplier();
                _runtimeMaxHp = Mathf.Max(1, Mathf.RoundToInt(definition.MaxHp * mul));
                var baseStrike = definition.GetBasicStrikeDamage(moveRegistry);
                _runtimeEnemyStrikeDamage = Mathf.Max(1, Mathf.RoundToInt(baseStrike * mul));
                _enemyDifficultyApplied = true;
                maxHpCap = _runtimeMaxHp;
            }
            else
            {
                _runtimeMaxHp = definition.MaxHp;
            }

            if (startingHpOverride.HasValue)
                _currentHp = Mathf.Clamp(startingHpOverride.Value, 1, maxHpCap);
            else
                _currentHp = maxHpCap;
        }

        var label = definition != null ? definition.DisplayName : "?";
        gameObject.name = $"{(isAlly ? "Ally" : "Enemy")}_{label}_{slotIndex}";
        HpChanged?.Invoke(_currentHp, MaxHp);
    }

    public int GetBasicStrikeDamage()
    {
        if (_definition == null)
            return 0;

        if (_enemyDifficultyApplied)
            return _runtimeEnemyStrikeDamage;

        return _definition.GetBasicStrikeDamage(_moveRegistry);
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0)
            return;

        _currentHp = Mathf.Max(0, _currentHp - amount);
        CombatDamagePopup.SpawnAt(transform.position + Vector3.up * 0.55f, amount, _isAlly);
        HpChanged?.Invoke(_currentHp, MaxHp);
        if (!IsAlive)
            ApplyDefeatPresentation();
    }

    private void ApplyDefeatPresentation()
    {
        if (_isAlly)
            return;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = false;

        OnDefeated?.Invoke(this);
        Destroy(gameObject);
    }

    public SpriteRenderer SpriteRenderer => spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
}
