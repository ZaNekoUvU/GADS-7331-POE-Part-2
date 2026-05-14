using System.Collections.Generic;
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
    [SerializeField] private InputActionReference interactAction;

    private readonly HashSet<Collider2D> _playerProximity = new();
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

        _playerProximity.Clear();
    }

    private void Update()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay)
            return;

        if (_playerProximity.Count <= 0)
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
