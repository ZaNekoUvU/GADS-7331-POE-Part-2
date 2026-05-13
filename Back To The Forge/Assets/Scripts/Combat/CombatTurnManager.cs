using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple round-robin turn order across living allies and enemies (ally slots then enemy slots interleaved by index).
/// Extend later with speed stats or initiative.
/// </summary>
[DefaultExecutionOrder(-40)]
public class CombatTurnManager : MonoBehaviour
{
    [SerializeField] private CombatUnitSpawner spawner;

    [Tooltip("Real-time wait before an enemy uses Basic Attack (exploration may use timeScale 0).")]
    [SerializeField] private float enemyAttackDelaySeconds = 1f;

    private CombatSceneController _sceneController;

    private readonly List<CombatUnit> _order = new();
    private int _turnIndex;
    private bool _enemyTurnRoutineActive;

    public CombatUnit CurrentActor =>
        _order.Count > 0 && _turnIndex >= 0 && _turnIndex < _order.Count ? _order[_turnIndex] : null;

    private void OnEnable()
    {
        CombatUnit.OnDefeated += HandleUnitDefeated;
    }

    private void OnDisable()
    {
        CombatUnit.OnDefeated -= HandleUnitDefeated;
    }

    private void HandleUnitDefeated(CombatUnit unit)
    {
        if (unit == null)
            return;

        var i = _order.IndexOf(unit);
        if (i < 0)
            return;

        _order.RemoveAt(i);
        if (_turnIndex > i)
            _turnIndex--;
        else if (_turnIndex >= _order.Count)
            _turnIndex = _order.Count > 0 ? _order.Count - 1 : -1;

        if (_order.Count == 0)
            _turnIndex = -1;
    }

    private void Start()
    {
        if (spawner == null)
            spawner = FindAnyObjectByType<CombatUnitSpawner>();

        _sceneController = FindAnyObjectByType<CombatSceneController>();

        BuildTurnOrder();
        LogTurnState();
        TryScheduleEnemyTurn();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        _enemyTurnRoutineActive = false;
    }

    /// <summary>
    /// Allies only: performs Basic Attack then advances. Enemies act automatically after a delay.
    /// </summary>
    public void OnBasicAttackButtonPressed()
    {
        var actor = CurrentActor;
        if (actor == null || !actor.IsAlive)
        {
            Debug.LogWarning("[Combat] Basic Attack: no active fighter.", this);
            return;
        }

        if (!actor.IsAlly)
            return;

        var victory = PerformBasicAttackAndAdvanceTurn();
        if (!victory)
            TryScheduleEnemyTurn();
    }

    /// <summary>If the current fighter is an enemy, wait <see cref="enemyAttackDelaySeconds"/> then Basic Attack and advance.</summary>
    private void TryScheduleEnemyTurn()
    {
        if (_enemyTurnRoutineActive)
            return;

        var a = CurrentActor;
        if (a == null || !a.IsAlive || a.IsAlly)
            return;

        StartCoroutine(EnemyTurnSequence());
    }

    private IEnumerator EnemyTurnSequence()
    {
        _enemyTurnRoutineActive = true;
        yield return new WaitForSecondsRealtime(enemyAttackDelaySeconds);

        var actor = CurrentActor;
        if (actor == null || !actor.IsAlive || actor.IsAlly)
        {
            _enemyTurnRoutineActive = false;
            TryScheduleEnemyTurn();
            yield break;
        }

        var victory = PerformBasicAttackCurrentActor();
        if (victory)
        {
            _enemyTurnRoutineActive = false;
            yield break;
        }

        AdvanceTurn();
        _enemyTurnRoutineActive = false;
        TryScheduleEnemyTurn();
    }

    private void BuildTurnOrder()
    {
        _order.Clear();

        if (spawner == null)
            return;

        for (var i = 0; i < 3; i++)
        {
            if (i < spawner.SpawnedAllies.Count)
                TryAddLiving(spawner.SpawnedAllies[i]);
            if (i < spawner.SpawnedEnemies.Count)
                TryAddLiving(spawner.SpawnedEnemies[i]);
        }

        for (var i = 3; i < spawner.SpawnedAllies.Count; i++)
            TryAddLiving(spawner.SpawnedAllies[i]);

        for (var i = 3; i < spawner.SpawnedEnemies.Count; i++)
            TryAddLiving(spawner.SpawnedEnemies[i]);

        _turnIndex = _order.Count > 0 ? 0 : -1;
    }

    private void TryAddLiving(CombatUnit u)
    {
        if (u != null && u.IsAlive)
            _order.Add(u);
    }

    /// <summary>Advance to next living unit in order.</summary>
    public void AdvanceTurn()
    {
        if (_order.Count == 0)
            return;

        var steps = 0;
        do
        {
            _turnIndex = (_turnIndex + 1) % _order.Count;
            steps++;
            if (steps > _order.Count)
                break;
        } while (!CurrentActor.IsAlive && _order.Count > 0);

        LogTurnState();
    }

    /// <summary>Strike with the current actor, then advance turn. Returns true if combat ended (victory unload).</summary>
    public bool PerformBasicAttackAndAdvanceTurn()
    {
        var victory = PerformBasicAttackCurrentActor();
        if (!victory)
            AdvanceTurn();
        return victory;
    }

    /// <summary>Basic attack from current actor toward first living opponent. Returns true if combat ended (victory unload started).</summary>
    public bool PerformBasicAttackCurrentActor()
    {
        var actor = CurrentActor;
        if (actor == null || !actor.IsAlive)
        {
            Debug.LogWarning("[Combat] Basic strike skipped: no living current actor.", this);
            return false;
        }

        if (spawner == null)
        {
            Debug.LogError("[Combat] Basic strike skipped: CombatUnitSpawner is null.", this);
            return false;
        }

        CombatUnit target = null;
        if (actor.IsAlly)
        {
            foreach (var e in spawner.SpawnedEnemies)
            {
                if (e != null && e.IsAlive)
                {
                    target = e;
                    break;
                }
            }
        }
        else
        {
            foreach (var a in spawner.SpawnedAllies)
            {
                if (a != null && a.IsAlive)
                {
                    target = a;
                    break;
                }
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[Combat] Basic strike skipped: {actor.gameObject.name} has no living opponent.", this);
            return false;
        }

        var dmg = actor.GetBasicStrikeDamage();
        var hpBefore = target.CurrentHp;
        target.TakeDamage(dmg);
        Debug.Log(
            $"[Combat] STRIKE: {actor.gameObject.name} → {target.gameObject.name} | damage={dmg} | target HP {hpBefore} → {target.CurrentHp}/{target.MaxHp}",
            this);

        return TryEndCombatIfAllEnemiesDefeated();
    }

    private bool TryEndCombatIfAllEnemiesDefeated()
    {
        if (spawner == null || !spawner.AreAllEnemiesDefeated())
            return false;

        if (_sceneController == null)
            _sceneController = FindAnyObjectByType<CombatSceneController>();

        if (_sceneController == null)
        {
            Debug.LogError("[Combat] All enemies defeated but no CombatSceneController in scene — cannot unload combat.", this);
            return false;
        }

        Debug.Log("[Combat] All enemies defeated — unloading combat scene.", this);
<<<<<<< Updated upstream
        CombatSession.MarkVictoryLootPending();
=======

        if (spawner != null)
        {
            foreach (var a in spawner.SpawnedAllies)
            {
                if (a == null || !a.IsPlayerCharacter)
                    continue;

                var persist = FindAnyObjectByType<PlayerPersistentCombatHealth>();
                if (persist != null)
                    persist.RecordHpAfterCombat(a.CurrentHp);
                break;
            }
        }

>>>>>>> Stashed changes
        _sceneController.EndCombat();
        return true;
    }

    private void LogTurnState()
    {
        var a = CurrentActor;
        if (a == null)
            Debug.Log("[Combat] Turn manager: no actors.", this);
        else
            Debug.Log($"[Combat] Active: {a.gameObject.name} (HP {a.CurrentHp}/{a.MaxHp}).", this);
    }
}
