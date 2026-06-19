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
    /// True when <paramref name="other"/> belongs to the active local player leader (not hired companions).
    /// Prefer this over CompareTag("Player") so NPCs and mercenary followers are not mistaken for the player.
    /// </summary>
    public static bool IsPlayerCharacterCollider(Collider2D other)
    {
        if (other == null || IsCompanionWorldCollider(other))
            return false;

        if (!TryGetLeaderRigidbody(out var leaderBody))
            return false;

        var body = other.attachedRigidbody;
        return body != null && body == leaderBody;
    }

    /// <summary>True when <paramref name="other"/> belongs to a hired companion follower (not the leader).</summary>
    public static bool IsCompanionWorldCollider(Collider2D other)
    {
        if (other == null)
            return false;

        return other.GetComponentInParent<CompanionFollower2D>() != null;
    }

    /// <summary>Rigidbody2D for the active player leader, if any.</summary>
    public static bool TryGetLeaderRigidbody(out Rigidbody2D leaderBody)
    {
        leaderBody = null;
        var leader = Instance;
        if (leader == null)
            return false;

        leaderBody = leader.GetComponent<Rigidbody2D>();
        return leaderBody != null;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        Instance = this;
        EnsureInputReady();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        if (moveAction != null)
            moveAction.action.Disable();
    }

    private void Start()
    {
        EnsureInputReady();
    }

    /// <summary>Re-enables the move action map after main-menu UI or scene transitions.</summary>
    public void EnsureInputReady()
    {
        if (moveAction?.action == null)
            return;

        var map = moveAction.action.actionMap;
        if (map != null && !map.enabled)
            map.Enable();

        if (!moveAction.action.enabled)
            moveAction.action.Enable();
    }

    private void FixedUpdate()
    {
        if (IsMovementBlocked())
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        var input = ReadMoveInput();
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        if (input.sqrMagnitude > 0.01f)
            LastFacing2D = input;

        rb.linearVelocity = input * moveSpeed;
    }

    private static bool IsMovementBlocked()
    {
        return SimpleRpgDialogueUI.IsDialogueOpen
               || CompanionConversationUi.IsBlockingGameplay
               || ForgeQuestChoiceUI.IsBlockingGameplay
               || PauseMenuController.IsOpen;
    }

    private Vector2 ReadMoveInput()
    {
        if (moveAction?.action != null && moveAction.action.enabled)
        {
            var fromAction = moveAction.action.ReadValue<Vector2>();
            if (fromAction.sqrMagnitude > 0.01f)
                return fromAction;
        }

        return ReadKeyboardMoveInput();
    }

    private static Vector2 ReadKeyboardMoveInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        float x = 0f;
        float y = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            y += 1f;

        return new Vector2(x, y);
    }
}

