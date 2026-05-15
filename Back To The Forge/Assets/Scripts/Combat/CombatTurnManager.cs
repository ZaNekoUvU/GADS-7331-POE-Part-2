using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turn order: living allies that spawned (hero, then companions), then enemies. Empty companion slots are omitted.
/// </summary>
[DefaultExecutionOrder(-40)]
public class CombatTurnManager : MonoBehaviour
{
    public const int MoveIdStrike = 100;
    public const int MoveIdPowerStrike = 101;

    public const int PlayerMaxMana = CombatUnit.HeroMaxMana;
    public const int PowerStrikeManaCost = 10;
    public const int PlayerManaRegenPerTurn = 2;

    [SerializeField] private CombatUnitSpawner spawner;

    [Tooltip("Real-time wait before a companion or enemy uses an attack (exploration may use timeScale 0).")]
    [SerializeField] private float autoAttackDelaySeconds = 1f;

    [Tooltip("Chance to escape combat when the player chooses Flee (0.3 = 30%).")]
    [SerializeField] [Range(0f, 1f)] private float fleeSuccessChance = 0.3f;

    private CombatSceneController _sceneController;

    private readonly List<CombatUnit> _order = new();
    private int _turnIndex;
    private bool _autoTurnRoutineActive;

    public CombatUnit CurrentActor =>
        _order.Count > 0 && _turnIndex >= 0 && _turnIndex < _order.Count ? _order[_turnIndex] : null;

    public bool IsAutoTurnRoutineActive => _autoTurnRoutineActive;

    /// <summary>True when it is the player hero's turn and they may pick a command.</summary>
    public bool IsAwaitingPlayerCommand
    {
        get
        {
            if (_autoTurnRoutineActive)
                return false;

            return IsPlayerCommandActor(CurrentActor);
        }
    }

    public bool IsAwaitingAllyCommand => IsAwaitingPlayerCommand;

    public event Action TurnChanged;

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

        if (IsPlayerCommandActor(CurrentActor))
            _autoTurnRoutineActive = false;

        NotifyTurnChanged();
        TryScheduleAutoTurn();
    }

    private void Start()
    {
        if (spawner == null)
            spawner = FindAnyObjectByType<CombatUnitSpawner>();

        _sceneController = FindAnyObjectByType<CombatSceneController>();

        BuildTurnOrder();
        _autoTurnRoutineActive = false;
        LogTurnState();
        NotifyTurnChanged();
        TryScheduleAutoTurn();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        _autoTurnRoutineActive = false;
    }

    public void OnBasicAttackButtonPressed()
    {
        var target = GetFirstLivingOpponentFor(CurrentActor);
        if (target != null)
            PerformPlayerStrike(target, MoveIdStrike);
    }

    /// <summary>30% chance to leave combat; on failure the hero takes a hit and the turn ends.</summary>
    public void TryFlee()
    {
        if (!IsAwaitingPlayerCommand)
            return;

        var hero = GetPlayerHero();
        if (hero == null || !hero.IsAlive)
            return;

        if (UnityEngine.Random.value < fleeSuccessChance)
        {
            Debug.Log("[Combat] Flee succeeded — leaving combat.", this);
            EndCombatAfterFlee();
            return;
        }

        var attacker = GetFirstLivingOpponentFor(hero);
        var dmg = attacker != null
            ? attacker.GetStrikeDamageForMove(MoveIdStrike)
            : Mathf.Max(1, hero.MaxHp / 10);

        Debug.Log($"[Combat] Flee failed! {hero.gameObject.name} takes {dmg} damage.", this);
        hero.TakeDamage(dmg);

        if (hero.IsAlive && !TryEndCombatIfAllEnemiesDefeated())
            FinishTurnAfterAction();
    }

    public void PerformPlayerStrike(CombatUnit target, int moveId)
    {
        if (!IsAwaitingPlayerCommand)
            return;

        var player = CurrentActor;
        if (!IsPlayerCommandActor(player))
            return;

        if (!CanActorUseMove(player, moveId))
            return;

        if (!TryStrike(player, target, moveId, out var victory))
            return;

        if (!victory)
            FinishTurnAfterAction();
    }

    public CombatUnit GetPlayerHero()
    {
        if (spawner == null)
            return null;

        foreach (var ally in spawner.SpawnedAllies)
        {
            if (IsPlayerCommandActor(ally))
                return ally;
        }

        return null;
    }

    public bool CanPlayerUsePowerStrike()
    {
        var hero = GetPlayerHero();
        return hero != null && hero.IsAlive && hero.CanSpendMana(PowerStrikeManaCost);
    }

    public bool CanActorUseMove(CombatUnit actor, int moveId)
    {
        if (actor == null || !actor.IsAlive)
            return false;

        if (moveId == MoveIdPowerStrike)
        {
            if (!IsPlayerCommandActor(actor))
                return false;

            return actor.CanSpendMana(PowerStrikeManaCost);
        }

        return moveId == MoveIdStrike || moveId <= 0;
    }

    public IReadOnlyList<CombatUnit> GetLivingOpponentsFor(CombatUnit actor)
    {
        var list = new List<CombatUnit>();
        if (actor == null || spawner == null)
            return list;

        if (actor.IsAlly)
        {
            foreach (var e in spawner.SpawnedEnemies)
            {
                if (e != null && e.IsAlive)
                    list.Add(e);
            }
        }
        else
        {
            foreach (var a in spawner.SpawnedAllies)
            {
                if (a != null && a.IsAlive)
                    list.Add(a);
            }
        }

        return list;
    }

    public CombatUnit GetFirstLivingOpponentFor(CombatUnit actor)
    {
        var opponents = GetLivingOpponentsFor(actor);
        return opponents.Count > 0 ? opponents[0] : null;
    }

    /// <summary>Party leader in ally slot 0 — receives menu commands.</summary>
    public bool IsPlayerCommandActor(CombatUnit unit)
    {
        if (unit == null || !unit.IsAlive || !unit.IsAlly)
            return false;

        if (unit.IsPlayerCharacter)
            return true;

        if (spawner != null && spawner.SpawnedAllies.Count > 0)
            return spawner.SpawnedAllies[0] == unit;

        return unit.SlotIndex == 0;
    }

    private void TryScheduleAutoTurn()
    {
        if (_autoTurnRoutineActive)
            return;

        var a = CurrentActor;
        if (a == null || !a.IsAlive)
            return;

        if (IsPlayerCommandActor(a))
            return;

        StartCoroutine(AutoTurnSequence());
    }

    private IEnumerator AutoTurnSequence()
    {
        _autoTurnRoutineActive = true;
        yield return new WaitForSecondsRealtime(autoAttackDelaySeconds);

        var actor = CurrentActor;
        if (actor == null || !actor.IsAlive || IsPlayerCommandActor(actor))
        {
            _autoTurnRoutineActive = false;
            NotifyTurnChanged();
            TryScheduleAutoTurn();
            yield break;
        }

        var target = GetFirstLivingOpponentFor(actor);
        if (target == null)
        {
            Debug.LogWarning($"[Combat] Auto attack skipped: {actor.gameObject.name} has no living opponent.", this);
            _autoTurnRoutineActive = false;
            FinishTurnAfterAction();
            yield break;
        }

        if (!TryStrike(actor, target, MoveIdStrike, out var victory))
        {
            _autoTurnRoutineActive = false;
            FinishTurnAfterAction();
            yield break;
        }

        if (victory)
        {
            _autoTurnRoutineActive = false;
            yield break;
        }

        // Clear before AdvanceTurn so TurnChanged listeners see a ready player turn.
        _autoTurnRoutineActive = false;
        AdvanceTurn();
        TryScheduleAutoTurn();
    }

    private void FinishTurnAfterAction()
    {
        _autoTurnRoutineActive = false;
        AdvanceTurn();
        TryScheduleAutoTurn();
    }

    private void BuildTurnOrder()
    {
        _order.Clear();

        if (spawner == null)
            return;

        foreach (var ally in spawner.SpawnedAllies)
            TryAddLiving(ally);

        foreach (var enemy in spawner.SpawnedEnemies)
            TryAddLiving(enemy);

        _turnIndex = _order.Count > 0 ? 0 : -1;
    }

    private void TryAddLiving(CombatUnit u)
    {
        if (u != null && u.IsAlive)
            _order.Add(u);
    }

    public void AdvanceTurn()
    {
        if (_order.Count == 0)
            return;

        var previousActor = CurrentActor;

        var steps = 0;
        do
        {
            _turnIndex = (_turnIndex + 1) % _order.Count;
            steps++;
            if (steps > _order.Count)
                break;
        } while (!CurrentActor.IsAlive && _order.Count > 0);

        if (IsPlayerCommandActor(CurrentActor))
        {
            _autoTurnRoutineActive = false;

            if (previousActor != null
                && !IsPlayerCommandActor(previousActor)
                && GetPlayerHero() is { } hero)
            {
                hero.RegenerateMana(PlayerManaRegenPerTurn);
            }
        }

        LogTurnState();
        NotifyTurnChanged();
    }

    private bool TryStrike(CombatUnit actor, CombatUnit target, int moveId, out bool victory)
    {
        victory = false;

        if (actor == null || !actor.IsAlive)
        {
            Debug.LogWarning("[Combat] Strike skipped: attacker is not alive.", this);
            return false;
        }

        if (target == null || !target.IsAlive || target.IsAlly == actor.IsAlly)
        {
            Debug.LogWarning($"[Combat] Strike skipped: invalid target for {actor.gameObject.name}.", this);
            return false;
        }

        if (!CanActorUseMove(actor, moveId))
        {
            Debug.LogWarning($"[Combat] Strike skipped: {actor.gameObject.name} cannot use move {moveId}.", this);
            return false;
        }

        if (moveId == MoveIdPowerStrike && !actor.TrySpendMana(PowerStrikeManaCost))
        {
            Debug.LogWarning($"[Combat] Strike skipped: {actor.gameObject.name} lacks mana for Power Strike.", this);
            return false;
        }

        var dmg = actor.GetStrikeDamageForMove(moveId);
        var hpBefore = target.CurrentHp;
        target.TakeDamage(dmg);
        Debug.Log(
            $"[Combat] STRIKE: {actor.gameObject.name} → {target.gameObject.name} | move={moveId} damage={dmg} | target HP {hpBefore} → {target.CurrentHp}/{target.MaxHp}",
            this);

        victory = TryEndCombatIfAllEnemiesDefeated();
        return true;
    }

    private void EndCombatAfterFlee()
    {
        if (_sceneController == null)
            _sceneController = FindAnyObjectByType<CombatSceneController>();

        if (_sceneController == null)
        {
            Debug.LogError("[Combat] Flee succeeded but no CombatSceneController in scene.", this);
            return;
        }

        _sceneController.EndCombat();
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

        CombatSession.MarkVictoryLootPending();

        if (spawner != null)
        {
            foreach (var a in spawner.SpawnedAllies)
            {
                if (a == null || !IsPlayerCommandActor(a))
                    continue;

                var persist = FindAnyObjectByType<PlayerPersistentCombatHealth>();
                if (persist != null)
                    persist.RecordHpAfterCombat(a.CurrentHp);
                break;
            }
        }

        _sceneController.EndCombat();
        return true;
    }

    private void LogTurnState()
    {
        var a = CurrentActor;
        if (a == null)
        {
            Debug.Log("[Combat] Turn manager: no actors.", this);
            return;
        }

        var tag = IsPlayerCommandActor(a) ? " [PLAYER TURN]" : a.IsAlly ? " [COMPANION]" : " [ENEMY]";
        var mp = a.UsesMana ? $" MP {a.CurrentMana}/{a.MaxMana}" : "";
        Debug.Log($"[Combat] Active: {a.gameObject.name} (HP {a.CurrentHp}/{a.MaxHp}){mp}{tag}.", this);
    }

    private void NotifyTurnChanged() => TurnChanged?.Invoke();
}
