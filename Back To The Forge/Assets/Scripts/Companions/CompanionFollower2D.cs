using UnityEngine;

/// <summary>
/// JRPG-style line formation: followers stay behind the leader based on last move facing, with depth and slight echelon.
/// Pauses during dialogue, forge choices, and combat load.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CompanionFollower2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private int slotIndex;
    [SerializeField] private float smoothSpeed = 14f;
    [SerializeField] private float baseDistanceBehind = 0.95f;
    [SerializeField] private float depthPerSlot = 0.5f;
    [SerializeField] private float lateralSpread = 0.14f;

    private Rigidbody2D _rb;
    private CombatAdditiveCoordinator _combatCoordinator;
    private PlayerMovement2D _leaderMovement;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _combatCoordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.simulated = true;
    }

    public void Configure(Transform followTarget, int companionSlotIndex)
    {
        target = followTarget;
        slotIndex = companionSlotIndex;
        _leaderMovement = followTarget != null ? followTarget.GetComponent<PlayerMovement2D>() : null;
    }

    private void FixedUpdate()
    {
        if (target == null && PlayerMovement2D.Instance != null)
        {
            target = PlayerMovement2D.Instance.transform;
            _leaderMovement = PlayerMovement2D.Instance;
        }

        if (target == null)
            return;

        if (SimpleRpgDialogueUI.IsDialogueOpen || CompanionConversationUi.IsBlockingGameplay || ForgeQuestChoiceUI.IsBlockingGameplay || PauseMenuController.IsOpen)
            return;

        if (_combatCoordinator == null)
            _combatCoordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();

        if (_combatCoordinator != null && _combatCoordinator.IsCombatActiveOrLoading)
            return;

        if (_leaderMovement == null)
            _leaderMovement = target.GetComponent<PlayerMovement2D>();

        var facing = _leaderMovement != null && _leaderMovement.LastFacing2D.sqrMagnitude > 0.0001f
            ? _leaderMovement.LastFacing2D.normalized
            : Vector2.down;

        var behind = -facing;
        var depth = baseDistanceBehind + slotIndex * depthPerSlot;
        var lateral = new Vector2(-behind.y, behind.x) * (lateralSpread + slotIndex * 0.06f);
        var desired = (Vector2)target.position + behind * depth + lateral;

        var next = Vector2.Lerp(_rb.position, desired, 1f - Mathf.Exp(-smoothSpeed * Time.fixedDeltaTime));
        _rb.MovePosition(next);
    }
}
