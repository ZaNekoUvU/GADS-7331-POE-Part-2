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

    public UnitDefinition Definition => _definition;
    public int CurrentHp => _currentHp;
    public int MaxHp => _definition != null ? _definition.MaxHp : 0;
    public bool IsAlive => _currentHp > 0;
    public bool IsAlly => _isAlly;
    public bool IsPlayerCharacter => _isPlayerCharacter;
    public int SlotIndex => _slotIndex;

    public void Initialize(
        UnitDefinition definition,
        MoveRegistry moveRegistry,
        bool isAlly,
        bool isPlayerCharacter,
        int slotIndex)
    {
        _definition = definition;
        _moveRegistry = moveRegistry;
        _isAlly = isAlly;
        _isPlayerCharacter = isPlayerCharacter;
        _slotIndex = slotIndex;
        _currentHp = definition != null ? definition.MaxHp : 0;

        gameObject.name = $"{(isAlly ? "Ally" : "Enemy")}_{definition.DisplayName}_{slotIndex}";
    }

    public int GetBasicStrikeDamage()
    {
        if (_definition == null)
            return 0;

        return _definition.GetBasicStrikeDamage(_moveRegistry);
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0)
            return;

        _currentHp = Mathf.Max(0, _currentHp - amount);
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
