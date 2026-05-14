using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float moveSpeed = 5f;

    /// <summary>Last movement direction while input was held (for party followers behind the leader).</summary>
    public Vector2 LastFacing2D { get; private set; } = Vector2.down;

    private Rigidbody2D rb;

    /// <summary>Active player movement (single local player).</summary>
    public static PlayerMovement2D Instance { get; private set; }

    /// <summary>
    /// True when <paramref name="other"/> is tied to the real player rigidbody (has <see cref="PlayerMovement2D"/>).
    /// Prefer this over CompareTag("Player") so NPCs are not mistaken for the player.
    /// </summary>
    public static bool IsPlayerCharacterCollider(Collider2D other)
    {
        if (other == null)
            return false;

        var body = other.attachedRigidbody;
        if (body == null)
            return false;

        return body.GetComponent<PlayerMovement2D>() != null;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        Instance = this;

        if (moveAction != null)
            moveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        if (moveAction != null)
            moveAction.action.Disable();
    }

    private void FixedUpdate()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay || PauseMenuController.IsOpen)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (moveAction == null)
            return;

        Vector2 input = moveAction.action.ReadValue<Vector2>();

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        if (input.sqrMagnitude > 0.01f)
            LastFacing2D = input;

        rb.linearVelocity = input * moveSpeed;
    }
}
