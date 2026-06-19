using System;
using UnityEngine;

/// <summary>
/// Runtime combatant instance spawned from a <see cref="UnitDefinition"/>.
/// </summary>
public class CombatUnit : MonoBehaviour
{
    /// <summary>Fired once when this unit is removed from combat (before <see cref="Object.Destroy"/> on enemies).</summary>
    public static event System.Action<CombatUnit> OnDefeated;

    /// <summary>Fired when an ally (player or mercenary) reaches 0 HP in combat.</summary>
    public static event System.Action<CombatUnit> OnAllyDefeated;

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
    private bool _allyMoraleHpApplied;
    private float _attackDamageMultiplier = 1f;

    public UnitDefinition Definition => _definition;
    public int CurrentHp => _currentHp;
    public int MaxHp
    {
        get
        {
            if (_definition == null)
                return 0;
            if (_enemyDifficultyApplied || _allyMoraleHpApplied)
                return _runtimeMaxHp;
            return _definition.MaxHp;
        }
    }
    public bool IsAlive => _currentHp > 0;
    public bool IsAlly => _isAlly;
    public bool IsPlayerCharacter => _isPlayerCharacter;
    public int SlotIndex => _slotIndex;

    public float AttackDamageMultiplier => _attackDamageMultiplier;

    public void SetAttackDamageMultiplier(float multiplier)
    {
        _attackDamageMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ApplyMoraleModifiers(float attackMultiplier, float maxHpMultiplier)
    {
        if (attackMultiplier > 0f)
            SetAttackDamageMultiplier(attackMultiplier);

        if (!_isAlly || _enemyDifficultyApplied || _definition == null)
            return;

        if (maxHpMultiplier <= 1.001f)
            return;

        _runtimeMaxHp = Mathf.Max(1, Mathf.RoundToInt(_definition.MaxHp * maxHpMultiplier));
        _allyMoraleHpApplied = true;
        _currentHp = _runtimeMaxHp;
        HpChanged?.Invoke(_currentHp, MaxHp);
    }

    public const int HeroMaxMana = 10;

    private bool _usesMana;
    private int _currentMana;
    private int _maxMana;

    public bool UsesMana => _usesMana;
    public int CurrentMana => _usesMana ? _currentMana : 0;
    public int MaxMana => _usesMana ? _maxMana : 0;

    /// <summary>Args: current HP, max HP. Fired after <see cref="Initialize"/> and after <see cref="TakeDamage"/>.</summary>
    public event Action<int, int> HpChanged;

    /// <summary>Args: current MP, max MP. Hero only.</summary>
    public event Action<int, int> ManaChanged;

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
        _attackDamageMultiplier = 1f;
        _allyMoraleHpApplied = false;

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

        _usesMana = isAlly && isPlayerCharacter;
        if (_usesMana)
        {
            _maxMana = HeroMaxMana;
            _currentMana = HeroMaxMana;
        }
        else
        {
            _maxMana = 0;
            _currentMana = 0;
        }

        var label = definition != null ? definition.DisplayName : "?";
        gameObject.name = $"{(isAlly ? "Ally" : "Enemy")}_{label}_{slotIndex}";
        HpChanged?.Invoke(_currentHp, MaxHp);
        if (_usesMana)
            ManaChanged?.Invoke(_currentMana, _maxMana);
    }

    public bool CanSpendMana(int cost) => _usesMana && cost > 0 && _currentMana >= cost;

    public bool TrySpendMana(int cost)
    {
        if (!CanSpendMana(cost))
            return false;

        _currentMana -= cost;
        ManaChanged?.Invoke(_currentMana, _maxMana);
        return true;
    }

    public void RegenerateMana(int amount)
    {
        if (!_usesMana || amount <= 0)
            return;

        var before = _currentMana;
        _currentMana = Mathf.Min(_maxMana, _currentMana + amount);
        if (_currentMana != before)
            ManaChanged?.Invoke(_currentMana, _maxMana);
    }

    public int GetBasicStrikeDamage() => GetStrikeDamageForMove(0);

    /// <summary>Damage for a specific move id (0 = basic strike / best default).</summary>
    public int GetStrikeDamageForMove(int moveId)
    {
        if (_definition == null)
            return 0;

        if (_enemyDifficultyApplied)
        {
            if (moveId <= 0)
                return _runtimeEnemyStrikeDamage;

            if (_moveRegistry != null && _moveRegistry.TryGet(moveId, out var enemyMove))
            {
                var scaled = enemyMove.BaseDamage > 0
                    ? enemyMove.BaseDamage
                    : _runtimeEnemyStrikeDamage;
                return Mathf.Max(1, Mathf.RoundToInt(scaled * enemyMove.DamageMultiplier));
            }

            return _runtimeEnemyStrikeDamage;
        }

        var basePower = _definition.GetBasicStrikeDamage(_moveRegistry);
        if (moveId <= 0)
            return ApplyAllyMultiplier(basePower);

        if (_moveRegistry == null || !_moveRegistry.TryGet(moveId, out var move))
            return ApplyAllyMultiplier(basePower);

        var raw = move.BaseDamage > 0 ? move.BaseDamage : basePower;
        return ApplyAllyMultiplier(Mathf.Max(1, Mathf.RoundToInt(raw * move.DamageMultiplier)));
    }

    private int ApplyAllyMultiplier(int damage)
    {
        if (!_isAlly || Mathf.Approximately(_attackDamageMultiplier, 1f))
            return damage;

        return Mathf.Max(1, Mathf.RoundToInt(damage * _attackDamageMultiplier));
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
        {
            OnAllyDefeated?.Invoke(this);
            return;
        }

        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = false;

        OnDefeated?.Invoke(this);
        Destroy(gameObject);
    }

    public SpriteRenderer SpriteRenderer => spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();
}
