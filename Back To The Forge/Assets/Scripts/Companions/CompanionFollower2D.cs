using UnityEngine;

/// <summary>
/// JRPG-style party line: slot 0 follows the player; later slots follow the merc ahead so each keeps a visible gap.
/// Pauses during dialogue, forge choices, and combat load.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CompanionFollower2D : MonoBehaviour
{
    private const int MaxTrackedSlots = 3;

    private static readonly CompanionFollower2D[] s_followersBySlot = new CompanionFollower2D[MaxTrackedSlots];

    [SerializeField] private Transform target;
    [SerializeField] private int slotIndex;
    [SerializeField] private float smoothSpeed = 14f;
    [SerializeField] private float gapFromLeader = 1.05f;
    [SerializeField] private float lateralSpread = 0.22f;

    private Rigidbody2D _rb;
    private CombatAdditiveCoordinator _combatCoordinator;
    private PlayerMovement2D _leaderMovement;
    private MercenaryDirectionalAnimator2D _mercenaryAnimator;
    private Animator _unityAnimator;
    private MercenaryWalkAnimatorSetup _walkAnimatorSetup;
    private SpriteRenderer _spriteRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        for (var i = 0; i < s_followersBySlot.Length; i++)
            s_followersBySlot[i] = null;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _combatCoordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.simulated = true;
        _mercenaryAnimator = GetComponent<MercenaryDirectionalAnimator2D>();
        _unityAnimator = GetComponent<Animator>();
        _walkAnimatorSetup = GetComponent<MercenaryWalkAnimatorSetup>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnDisable()
    {
        UnregisterSlot();
    }

    private void OnDestroy()
    {
        UnregisterSlot();
    }

    public void Configure(Transform followTarget, int companionSlotIndex)
    {
        target = followTarget;
        slotIndex = Mathf.Clamp(companionSlotIndex, 0, MaxTrackedSlots - 1);
        _leaderMovement = followTarget != null ? followTarget.GetComponent<PlayerMovement2D>() : null;
        RegisterSlot();
        SnapToFormationImmediate();
    }

    private void RegisterSlot()
    {
        if (slotIndex < 0 || slotIndex >= s_followersBySlot.Length)
            return;

        s_followersBySlot[slotIndex] = this;
    }

    private void UnregisterSlot()
    {
        if (slotIndex < 0 || slotIndex >= s_followersBySlot.Length)
            return;

        if (s_followersBySlot[slotIndex] == this)
            s_followersBySlot[slotIndex] = null;
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

        var desired = ComputeDesiredPosition();
        var next = Vector2.Lerp(_rb.position, desired, 1f - Mathf.Exp(-smoothSpeed * Time.fixedDeltaTime));
        var facing = ResolvePartyFacing();
        UpdateMercenaryAnimation(next, facing);
        _rb.MovePosition(next);
    }

    private void SnapToFormationImmediate()
    {
        if (_rb == null)
            _rb = GetComponent<Rigidbody2D>();

        if (_rb == null || target == null)
            return;

        _rb.position = ComputeDesiredPosition();
    }

    private Vector2 ComputeDesiredPosition()
    {
        var facing = ResolvePartyFacing();
        var behind = -facing;
        var anchor = ResolveChainAnchorPosition();
        var lateralSign = slotIndex % 2 == 0 ? -1f : 1f;
        var lateral = new Vector2(-behind.y, behind.x) * (lateralSpread * lateralSign);
        return anchor + behind * gapFromLeader + lateral;
    }

    private Vector2 ResolveChainAnchorPosition()
    {
        if (slotIndex <= 0)
            return target.position;

        var previous = slotIndex - 1 >= 0 && slotIndex - 1 < s_followersBySlot.Length
            ? s_followersBySlot[slotIndex - 1]
            : null;

        if (previous != null)
            return previous._rb != null ? previous._rb.position : (Vector2)previous.transform.position;

        return target.position;
    }

    private Vector2 ResolvePartyFacing()
    {
        if (_leaderMovement != null && _leaderMovement.LastFacing2D.sqrMagnitude > 0.0001f)
            return _leaderMovement.LastFacing2D.normalized;

        return Vector2.down;
    }

    private void UpdateMercenaryAnimation(Vector2 nextPosition, Vector2 leaderFacing)
    {
        var moveDelta = nextPosition - _rb.position;
        var moving = moveDelta.sqrMagnitude > 0.0002f;
        var facing = leaderFacing.sqrMagnitude > 0.0001f ? leaderFacing : moveDelta;
        var animDirection = moving && moveDelta.sqrMagnitude > 0.0001f ? moveDelta : facing;

        if (_mercenaryAnimator != null)
        {
            if (animDirection.sqrMagnitude > 0.0001f)
                _mercenaryAnimator.SetFacingFromDirection(animDirection);
            _mercenaryAnimator.SetMoving(moving);
            return;
        }

        if (_unityAnimator == null)
            return;

        if (animDirection.sqrMagnitude > 0.0001f)
            PlayDirectionalWalk(animDirection);

        _unityAnimator.speed = moving ? 1f : 0f;
        if (!moving)
            _unityAnimator.Play(_unityAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
    }

    private void PlayDirectionalWalk(Vector2 dir)
    {
        if (_unityAnimator == null)
            return;

        var d = dir.normalized;
        if (d.y > 0.01f)
            _unityAnimator.Play("Up");
        else if (d.y < -0.01f)
            _unityAnimator.Play("Down");
        else if (d.x > 0.01f && _walkAnimatorSetup != null && _walkAnimatorSetup.UseDedicatedRightWalk)
            _unityAnimator.Play("Left");
        else if (d.x < -0.01f && _walkAnimatorSetup != null && _walkAnimatorSetup.UseDedicatedRightWalk)
            _unityAnimator.Play("Right");
        else
            _unityAnimator.Play("Left");

        if (_spriteRenderer != null)
            _spriteRenderer.flipX = false;
    }
}
