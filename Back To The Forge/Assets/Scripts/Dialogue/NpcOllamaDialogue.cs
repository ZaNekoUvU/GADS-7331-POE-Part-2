using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Talks to local Ollama using a <see cref="NpcDialogueProfile"/> (unique name + persona). Falls back to profile lines if the model is offline or busy.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NpcOllamaDialogue : MonoBehaviour
{
    [SerializeField] private NpcDialogueProfile profile;
    [SerializeField] private OllamaDialogueService ollamaService;
    [SerializeField] private SimpleRpgDialogueUI dialogueUi;

    [Header("UI")]
    [SerializeField] private string waitingEllipsis = "…";

    [Header("Proximity & interact")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private InputActionReference interactAction;

    private int _playerOverlap;
    private bool _requestRunning;

    private void Awake()
    {
        var c = GetComponent<Collider2D>();
        if (c != null && !c.isTrigger)
            Debug.LogWarning($"{nameof(NpcOllamaDialogue)}: Collider on '{name}' should be a trigger.", this);
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
    }

    private void Update()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay)
            return;

        if (_playerOverlap <= 0 || _requestRunning)
            return;

        if (!WasInteractPressedThisFrame())
            return;

        if (profile == null)
        {
            Debug.LogWarning($"{nameof(NpcOllamaDialogue)} on '{name}': assign a {nameof(NpcDialogueProfile)}.", this);
            return;
        }

        var service = ollamaService != null ? ollamaService : OllamaDialogueService.GetOrCreate();
        var ui = dialogueUi != null ? dialogueUi : SimpleRpgDialogueUI.GetOrCreate();

        if (service.IsBusy)
        {
            ui.Show(profile.CharacterName, profile.PickRandomFallback());
            return;
        }

        StartCoroutine(TalkRoutine(ui, service));
    }

    private IEnumerator TalkRoutine(SimpleRpgDialogueUI ui, OllamaDialogueService service)
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
            Debug.LogWarning($"[Ollama] {profile.CharacterName}: {err}", this);

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
        if (!other.CompareTag(playerTag))
            return;

        _playerOverlap++;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        _playerOverlap = Mathf.Max(0, _playerOverlap - 1);
    }
}
