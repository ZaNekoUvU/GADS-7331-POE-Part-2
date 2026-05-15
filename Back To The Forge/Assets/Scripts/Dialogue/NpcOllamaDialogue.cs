using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Requests server-backed NPC dialogue using a <see cref="NpcDialogueProfile"/>.
/// Falls back to profile lines if the AI gateway is offline or busy.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NpcOllamaDialogue : MonoBehaviour
{
    [SerializeField] private NpcDialogueProfile profile;
    [FormerlySerializedAs("ollamaService")]
    [SerializeField] private AiDialogueService aiService;
    [SerializeField] private SimpleRpgDialogueUI dialogueUi;

    [Header("UI")]
    [SerializeField] private string waitingEllipsis = "…";

    [Header("Proximity & interact")]
    [SerializeField] private InputActionReference interactAction;

    private readonly HashSet<Collider2D> _playerProximity = new();
    private bool _requestRunning;

    private void Awake()
    {
        Collider2DTriggerUtil.WarnIfNoTalkTrigger(gameObject, nameof(NpcOllamaDialogue));
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

        StopAllCoroutines();
        _requestRunning = false;
        _playerProximity.Clear();
    }

    private void Update()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay || PauseMenuController.IsOpen)
            return;

        if (_playerProximity.Count <= 0 || _requestRunning)
            return;

        if (!WasInteractPressedThisFrame())
            return;

        if (profile == null)
        {
            Debug.LogWarning($"{nameof(NpcOllamaDialogue)} on '{name}': assign a {nameof(NpcDialogueProfile)}.", this);
            return;
        }

        var service = aiService != null ? aiService : AiDialogueService.GetOrCreate();
        var ui = dialogueUi != null ? dialogueUi : SimpleRpgDialogueUI.GetOrCreate();

        if (service.IsBusy)
        {
            ui.Show(profile.CharacterName, profile.PickRandomFallback());
            return;
        }

        StartCoroutine(TalkRoutine(ui, service));
    }

    private IEnumerator TalkRoutine(SimpleRpgDialogueUI ui, AiDialogueService service)
    {
        _requestRunning = true;

        ui.ShowAwaitingLine(profile.CharacterName, waitingEllipsis);

        string ok = null;
        string err = null;

        // Must use StartCoroutine: yielding the IEnumerator alone does not wait for nested yields (HTTP).
        yield return StartCoroutine(service.RequestNpcLineCoroutine(
            profile,
            s => ok = s,
            e => err = e));

        if (!string.IsNullOrWhiteSpace(err))
            Debug.LogWarning($"[AI Gateway] {profile.CharacterName}: {err}", this);

        if (!string.IsNullOrWhiteSpace(ok))
            ui.SetDialogueLineAndAllowAdvance(ok);
        else
            ui.SetDialogueLineAndAllowAdvance(profile.PickRandomFallback());

        _requestRunning = false;
    }

    private bool WasInteractPressedThisFrame()
    {
        if (SimpleRpgDialogueUI.InteractConsumedByDialogueFrame == Time.frameCount)
            return false;

        if (interactAction != null && interactAction.action != null)
            return interactAction.action.WasPressedThisFrame();

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        _playerProximity.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        _playerProximity.Remove(other);
    }
}
