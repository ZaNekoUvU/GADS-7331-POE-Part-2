using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D trigger: while the player stays inside, every <see cref="rollIntervalSeconds"/> there is
/// a <see cref="chancePerRoll"/> probability to start combat via <see cref="CombatStarter"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RiskyGroundEncounter2D : MonoBehaviour
{
    [SerializeField] private CombatStarter combatStarter;
    [SerializeField] private CombatAdditiveCoordinator coordinator;
    [SerializeField] private int encounterId;
    [SerializeField] private float rollIntervalSeconds = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float chancePerRoll = 0.1f;

    private readonly HashSet<Collider2D> _playerProximity = new();
    private Coroutine _riskRoutine;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (!col.isTrigger)
            Debug.LogWarning($"{nameof(RiskyGroundEncounter2D)}: Collider2D on '{name}' should be a trigger.", this);
    }

    private void OnDisable()
    {
        _playerProximity.Clear();
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

        if (_playerProximity.Add(other) && _playerProximity.Count == 1 && _riskRoutine == null)
            _riskRoutine = StartCoroutine(RiskLoop());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        _playerProximity.Remove(other);

        if (_playerProximity.Count == 0 && _riskRoutine != null)
        {
            StopCoroutine(_riskRoutine);
            _riskRoutine = null;
        }
    }

    private IEnumerator RiskLoop()
    {
        var wait = new WaitForSeconds(rollIntervalSeconds);

        while (_playerProximity.Count > 0)
        {
            yield return wait;

            if (_playerProximity.Count <= 0)
                break;

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

            if (Random.value < chancePerRoll)
                combatStarter.StartRandomEncounterWithLlmIntro(encounterId);
        }

        _riskRoutine = null;
    }
}
