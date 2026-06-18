using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D trigger: while the player stays inside, every <see cref="rollIntervalSeconds"/> there is
/// a <see cref="chancePerRoll"/> probability to start combat via <see cref="CombatStarter"/>.
/// Chance is per roll tick (not once per visit). Use cooldown + movement gate so 10% does not chain fights.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RiskyGroundEncounter2D : MonoBehaviour
{
    private const string LogPrefix = "[RiskyGround]";

    [SerializeField] private CombatStarter combatStarter;
    [SerializeField] private CombatAdditiveCoordinator coordinator;
    [SerializeField] private int encounterId;

    [Tooltip("Seconds between encounter chance rolls while the player remains inside this zone.")]
    [SerializeField] private float rollIntervalSeconds = 3f;

    [Range(0f, 1f)]
    [Tooltip("Probability each roll tick triggers combat (e.g. 0.1 = 10% per tick, not 10% total for the whole visit).")]
    [SerializeField] private float chancePerRoll = 0.1f;

    [Tooltip("After any risky-ground fight starts, no new rolls anywhere until this many seconds pass.")]
    [SerializeField] private float cooldownAfterEncounterSeconds = 45f;

    [Tooltip("When on, a roll only happens if the player moved since the last roll (avoids spam while standing still).")]
    [SerializeField] private bool requireMovementBetweenRolls = true;

    [SerializeField] private float minMoveDistanceBetweenRolls = 1.1f;

    [SerializeField] private bool logRollResults;

    private readonly HashSet<Rigidbody2D> _playerLeaderBodies = new();
    private Coroutine _riskRoutine;
    private Vector2 _lastRollAnchor;
    private bool _hasRollAnchor;

    /// <summary>Shared cooldown across all risky-ground zones in the loaded world.</summary>
    private static float _nextRollAllowedAt;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (!col.isTrigger)
            Debug.LogWarning($"{nameof(RiskyGroundEncounter2D)}: Collider2D on '{name}' should be a trigger.", this);
    }

    private void OnDisable()
    {
        _playerLeaderBodies.Clear();
        _hasRollAnchor = false;
        if (_riskRoutine != null)
        {
            StopCoroutine(_riskRoutine);
            _riskRoutine = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        if (!PlayerMovement2D.TryGetLeaderRigidbody(out var leaderBody))
            return;

        if (_playerLeaderBodies.Add(leaderBody) && _playerLeaderBodies.Count == 1)
        {
            _hasRollAnchor = false;
            if (_riskRoutine == null)
                _riskRoutine = StartCoroutine(RiskLoop());
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        if (!PlayerMovement2D.TryGetLeaderRigidbody(out var leaderBody))
            return;

        _playerLeaderBodies.Remove(leaderBody);

        if (_playerLeaderBodies.Count == 0)
        {
            _hasRollAnchor = false;
            if (_riskRoutine != null)
            {
                StopCoroutine(_riskRoutine);
                _riskRoutine = null;
            }
        }
    }

    private IEnumerator RiskLoop()
    {
        var wait = new WaitForSeconds(rollIntervalSeconds);

        while (_playerLeaderBodies.Count > 0)
        {
            yield return wait;

            if (_playerLeaderBodies.Count <= 0)
                break;

            if (Time.time < _nextRollAllowedAt)
                continue;

            if (coordinator == null)
                coordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();

            if (coordinator != null && coordinator.IsCombatActiveOrLoading)
                continue;

            if (combatStarter == null)
                combatStarter = FindAnyObjectByType<CombatStarter>();

            if (combatStarter == null)
            {
                Debug.LogError($"{nameof(RiskyGroundEncounter2D)}: No {nameof(CombatStarter)} in scene.", this);
                yield break;
            }

            if (combatStarter.IsRandomEncounterIntroPlaying)
                continue;

            if (requireMovementBetweenRolls && !HasMovedEnoughSinceLastRoll())
                continue;

            var roll = Random.value;
            if (roll >= chancePerRoll)
            {
                if (logRollResults)
                    Debug.Log($"{LogPrefix} Safe tick on '{name}' — roll {roll:F3} >= {chancePerRoll:F3}.", this);
                continue;
            }

            _nextRollAllowedAt = Time.time + cooldownAfterEncounterSeconds;
            Debug.Log(
                $"{LogPrefix} Encounter triggered on '{name}' — roll {roll:F3} < {chancePerRoll:F3}. " +
                $"Next roll allowed in {cooldownAfterEncounterSeconds:F0}s.",
                this);
            combatStarter.StartRandomEncounterWithLlmIntro(encounterId);
        }

        _riskRoutine = null;
    }

    private bool HasMovedEnoughSinceLastRoll()
    {
        var player = PlayerMovement2D.Instance;
        if (player == null)
            return false;

        var pos = (Vector2)player.transform.position;
        if (!_hasRollAnchor)
        {
            _lastRollAnchor = pos;
            _hasRollAnchor = true;
            return false;
        }

        var minDist = Mathf.Max(0.05f, minMoveDistanceBetweenRolls);
        if ((pos - _lastRollAnchor).sqrMagnitude < minDist * minDist)
            return false;

        _lastRollAnchor = pos;
        return true;
    }
}
