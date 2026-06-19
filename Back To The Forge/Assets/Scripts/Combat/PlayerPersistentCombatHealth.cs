using UnityEngine;

/// <summary>
/// Persists the player's HP between combat encounters for the current day.
/// Resets to full when the calendar day advances (forge end-of-day or death penalty).
/// </summary>
public sealed class PlayerPersistentCombatHealth : MonoBehaviour
{
    public static PlayerPersistentCombatHealth Instance { get; private set; }

    [SerializeField] private UnitDefinition playerUnitDefinition;
    [SerializeField] private int fallbackMaxHp = 45;

    private int _currentHp;
    private bool _hpInitialized;

    public int MaxHp =>
        playerUnitDefinition != null ? Mathf.Max(1, playerUnitDefinition.MaxHp) : Mathf.Max(1, fallbackMaxHp);

    public int CurrentHp => _currentHp;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<PlayerPersistentCombatHealth>() != null)
            return;

        var go = new GameObject($"[{nameof(PlayerPersistentCombatHealth)}]");
        go.AddComponent<PlayerPersistentCombatHealth>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static PlayerPersistentCombatHealth GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<PlayerPersistentCombatHealth>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(PlayerPersistentCombatHealth)}]");
        return go.AddComponent<PlayerPersistentCombatHealth>();
    }

    private void EnsureInitialized()
    {
        if (_hpInitialized)
            return;

        _currentHp = MaxHp;
        _hpInitialized = true;
    }

    /// <summary>HP passed into <see cref="CombatUnit.Initialize"/> for slot 0 player.</summary>
    public int GetHpForCombatStart(int definitionMaxHp)
    {
        EnsureInitialized();

        var max = Mathf.Max(1, definitionMaxHp);
        if (_currentHp <= 0)
            _currentHp = max;

        return Mathf.Clamp(_currentHp, 1, max);
    }

    /// <summary>Call when leaving combat so the next fight starts at this HP.</summary>
    public void RecordHpAfterCombat(int hp)
    {
        EnsureInitialized();
        _currentHp = Mathf.Max(0, hp);
    }

    /// <summary>After ending the day at the forge or a death day advance.</summary>
    public void ResetToFullHealth()
    {
        EnsureInitialized();
        _currentHp = MaxHp;
    }

    /// <summary>Reads the combat scene player unit and stores HP before the combat scene unloads.</summary>
    public static void PersistFromCombatScene()
    {
        var persist = GetOrCreate();
        var turnManager = Object.FindAnyObjectByType<CombatTurnManager>();
        var hero = turnManager != null ? turnManager.GetPlayerHero() : null;

        if (hero == null || !hero.IsPlayerCharacter)
            return;

        persist.RecordHpAfterCombat(hero.CurrentHp);
    }
}
