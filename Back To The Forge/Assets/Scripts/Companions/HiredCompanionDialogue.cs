using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lets the player talk to a hired mercenary in the field. Ollama handles replies and morale.
/// </summary>
[DisallowMultipleComponent]
public sealed class HiredCompanionDialogue : MonoBehaviour
{
    private static HiredCompanionDialogue _pendingTalk;

    [SerializeField] private HireableCompanionOffer offer;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private float talkRadius = 1.35f;

    private readonly HashSet<Collider2D> _playerProximity = new();
    private CircleCollider2D _talkCollider;
    private CombatAdditiveCoordinator _combatCoordinator;
    private int _partySlot = -1;
    private bool _busy;

    public void Configure(HireableCompanionOffer configuredOffer, int partySlotIndex, InputActionReference interact = null)
    {
        offer = configuredOffer;
        _partySlot = partySlotIndex;
        if (interact != null)
            interactAction = interact;
        EnsureTalkCollider();
    }

    private void Awake()
    {
        EnsureTalkCollider();
    }

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.Enable();
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.Disable();

        _playerProximity.Clear();
        _busy = false;
        if (_pendingTalk == this)
            _pendingTalk = null;
    }

    private void EnsureTalkCollider()
    {
        if (_talkCollider != null)
            return;

        var child = transform.Find("TalkTrigger");
        if (child == null)
        {
            var go = new GameObject("TalkTrigger");
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        _talkCollider = child.GetComponent<CircleCollider2D>();
        if (_talkCollider == null)
            _talkCollider = child.gameObject.AddComponent<CircleCollider2D>();

        _talkCollider.isTrigger = true;
        _talkCollider.radius = talkRadius;
        child.gameObject.AddComponent<HiredCompanionTalkTrigger>().Initialize(this);
    }

    internal void NotifyPlayerEntered(Collider2D other)
    {
        if (PlayerMovement2D.IsPlayerCharacterCollider(other))
            _playerProximity.Add(other);
    }

    internal void NotifyPlayerExited(Collider2D other)
    {
        if (PlayerMovement2D.IsPlayerCharacterCollider(other))
            _playerProximity.Remove(other);
    }

    private void Update()
    {
        if (_busy || offer == null || _partySlot < 0)
            return;

        if (CompanionConversationUi.IsBlockingGameplay
            || SimpleRpgDialogueUI.IsDialogueOpen
            || ForgeQuestChoiceUI.IsBlockingGameplay
            || PauseMenuController.IsOpen
            || IsCombatBlocking())
            return;

        if (!WasInteractPressedThisFrame())
            return;

        if (!IsPlayerInTalkRange())
            return;

        var mgr = HiredCompanionManager.Instance;
        if (mgr == null || mgr.FindSlotWithUnitId(offer.UnitId) < 0)
            return;

        if (!TryClaimTalkSlot())
            return;

        StartCoroutine(BeginTalkRoutine());
    }

    private bool TryClaimTalkSlot()
    {
        if (_pendingTalk != null && _pendingTalk != this)
            return false;

        _pendingTalk = this;
        return true;
    }

    private IEnumerator BeginTalkRoutine()
    {
        _busy = true;

        var ui = CompanionConversationUi.GetOrCreate();
        if (ui == null)
        {
            _busy = false;
            if (_pendingTalk == this)
                _pendingTalk = null;
            yield break;
        }

        ui.BeginConversation(offer, offer.UnitId, () =>
        {
            _busy = false;
            if (_pendingTalk == this)
                _pendingTalk = null;
        });

        yield return new WaitUntil(() => !CompanionConversationUi.IsBlockingGameplay);

        _busy = false;
        if (_pendingTalk == this)
            _pendingTalk = null;
    }

    private bool IsCombatBlocking()
    {
        if (_combatCoordinator == null)
            _combatCoordinator = FindAnyObjectByType<CombatAdditiveCoordinator>();

        return _combatCoordinator != null && _combatCoordinator.IsCombatActiveOrLoading;
    }

    private bool IsPlayerInTalkRange()
    {
        if (_playerProximity.Count > 0)
            return true;

        var player = PlayerMovement2D.Instance;
        if (player == null)
            return false;

        var dist = Vector2.Distance(transform.position, player.transform.position);
        return dist <= talkRadius * 2.5f;
    }

    private bool WasInteractPressedThisFrame()
    {
        if (SimpleRpgDialogueUI.InteractConsumedByDialogueFrame == Time.frameCount)
            return false;

        if (interactAction != null && interactAction.action != null)
            return interactAction.action.WasPressedThisFrame();

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }
}

/// <summary>Forwards trigger events to <see cref="HiredCompanionDialogue"/> on the parent mercenary.</summary>
[DisallowMultipleComponent]
public sealed class HiredCompanionTalkTrigger : MonoBehaviour
{
    private HiredCompanionDialogue _owner;

    public void Initialize(HiredCompanionDialogue owner) => _owner = owner;

    private void OnTriggerEnter2D(Collider2D other) => _owner?.NotifyPlayerEntered(other);
    private void OnTriggerExit2D(Collider2D other) => _owner?.NotifyPlayerExited(other);
}
