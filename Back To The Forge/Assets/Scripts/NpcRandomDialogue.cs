using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// When the player stands in this object's trigger and presses Interact (tap, not hold), shows a random line
/// through <see cref="SimpleRpgDialogueUI"/>. Matches <see cref="BlacksmithMaster"/> input fallbacks.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NpcRandomDialogue : MonoBehaviour
{
    [Header("Dialogue")]
    [SerializeField] private string speakerDisplayName = "Villager";
    [SerializeField] [TextArea(2, 6)] private string[] randomLines =
    {
        "The mines are restless today.",
        "Did you hear the anvil last night?",
        "Mind your step on the risky ground."
    };

    [SerializeField] private SimpleRpgDialogueUI dialogueUi;

    [Header("Proximity & interact")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private InputActionReference interactAction;

    private int _playerOverlapCount;
    private Collider2D _collider2D;

    private void Awake()
    {
        _collider2D = GetComponent<Collider2D>();
        if (_collider2D != null && !_collider2D.isTrigger)
            Debug.LogWarning($"{nameof(NpcRandomDialogue)}: Collider on '{name}' should be a trigger for range detection.", this);
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
    }

    private void Update()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen)
            return;

        if (_playerOverlapCount <= 0)
            return;

        if (!WasInteractPressedThisFrame())
            return;

        if (randomLines == null || randomLines.Length == 0)
            return;

        var ui = dialogueUi != null ? dialogueUi : SimpleRpgDialogueUI.GetOrCreate();

        var line = randomLines[Random.Range(0, randomLines.Length)];
        if (string.IsNullOrWhiteSpace(line))
            return;

        ui.Show(speakerDisplayName, line.Trim());
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

        _playerOverlapCount++;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        _playerOverlapCount = Mathf.Max(0, _playerOverlapCount - 1);
    }
}
