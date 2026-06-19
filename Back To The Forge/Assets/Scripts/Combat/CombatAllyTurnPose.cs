using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// During an ally's turn, steps them partway toward the enemy side, then returns home when the turn ends.
/// </summary>
[DefaultExecutionOrder(-45)]
public class CombatAllyTurnPose : MonoBehaviour
{
    [SerializeField] private CombatTurnManager turnManager;
    [SerializeField] private CombatUnitSpawner spawner;

    [Tooltip("How far toward the enemy line to step (0.35 = 35% of the distance).")]
    [SerializeField] [Range(0.1f, 0.75f)] private float stepTowardOpponentFraction = 0.35f;

    [SerializeField] private float moveDurationSeconds = 0.18f;

    private readonly Dictionary<CombatUnit, Vector3> _homeByUnit = new();
    private readonly Dictionary<CombatUnit, Coroutine> _motionByUnit = new();
    private CombatUnit _posedAlly;

    private void Awake()
    {
        if (turnManager == null)
            turnManager = FindAnyObjectByType<CombatTurnManager>();

        if (spawner == null)
            spawner = FindAnyObjectByType<CombatUnitSpawner>();
    }

    private void OnEnable()
    {
        if (turnManager != null)
            turnManager.TurnChanged += OnTurnChanged;
    }

    private void OnDisable()
    {
        if (turnManager != null)
            turnManager.TurnChanged -= OnTurnChanged;

        foreach (var motion in _motionByUnit.Values)
        {
            if (motion != null)
                StopCoroutine(motion);
        }

        _motionByUnit.Clear();
        _posedAlly = null;
    }

    private void Start()
    {
        CacheHomePositions();
    }

    private void CacheHomePositions()
    {
        _homeByUnit.Clear();
        if (spawner == null)
            return;

        foreach (var ally in spawner.SpawnedAllies)
        {
            if (ally != null)
                _homeByUnit[ally] = ally.transform.position;
        }
    }

    private void OnTurnChanged()
    {
        var current = turnManager != null ? turnManager.CurrentActor : null;

        if (_posedAlly != null && _posedAlly != current)
        {
            ReturnHome(_posedAlly);
            _posedAlly = null;
        }

        if (current == null || !current.IsAlly || !current.IsAlive)
            return;

        if (!_homeByUnit.ContainsKey(current))
            _homeByUnit[current] = current.transform.position;

        StepTowardOpponents(current);
        _posedAlly = current;
    }

    private void ReturnHome(CombatUnit ally)
    {
        if (ally == null)
            return;

        if (_homeByUnit.TryGetValue(ally, out var home))
            AnimateTo(ally, home);
    }

    private void StepTowardOpponents(CombatUnit ally)
    {
        if (ally == null)
            return;

        var home = _homeByUnit.TryGetValue(ally, out var cachedHome) ? cachedHome : ally.transform.position;
        AnimateTo(ally, ComputeForwardPosition(home));
    }

    private Vector3 ComputeForwardPosition(Vector3 home)
    {
        if (spawner == null)
            return home + Vector3.right * 1.2f;

        var sumX = 0f;
        var count = 0;
        foreach (var enemy in spawner.SpawnedEnemies)
        {
            if (enemy == null || !enemy.IsAlive)
                continue;

            sumX += enemy.transform.position.x;
            count++;
        }

        if (count == 0)
            return home + Vector3.right * 1.2f;

        var enemyCenterX = sumX / count;
        var targetX = Mathf.Lerp(home.x, enemyCenterX, stepTowardOpponentFraction);
        return new Vector3(targetX, home.y, home.z);
    }

    private void AnimateTo(CombatUnit unit, Vector3 worldPosition)
    {
        if (unit == null)
            return;

        if (_motionByUnit.TryGetValue(unit, out var running) && running != null)
            StopCoroutine(running);

        _motionByUnit[unit] = StartCoroutine(AnimatePositionRoutine(unit, worldPosition));
    }

    private IEnumerator AnimatePositionRoutine(CombatUnit unit, Vector3 target)
    {
        var transform = unit.transform;
        var start = transform.position;
        var elapsed = 0f;
        var duration = Mathf.Max(0.05f, moveDurationSeconds);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        transform.position = target;
        _motionByUnit.Remove(unit);
    }
}
