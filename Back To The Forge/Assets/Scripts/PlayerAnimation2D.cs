using UnityEngine;

public class PlayerAnimator2D : MonoBehaviour
{
    private PlayerMovement2D mover;
    private Animator anim;
    private SpriteRenderer sr;
    private Rigidbody2D rb;

    private void Awake()
    {
        mover = GetComponent<PlayerMovement2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        Vector2 vel = rb.linearVelocity;
        bool isMoving = vel.sqrMagnitude > 0.01f;

        // Use actual movement direction OR last facing
        Vector2 dir = isMoving ? vel : mover.LastFacing2D;

        // --- Horizontal / Vertical split ---
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            // Side animation
            SetAnimation("PlayerWalkSide", isMoving);

            // FIX: If your sprite sheet faces LEFT by default:
            // Right movement ? flip
            // Left movement ? no flip
            sr.flipX = dir.x > 0;
        }
        else if (dir.y > 0)
        {
            // Up animation
            sr.flipX = false;
            SetAnimation("PlayerWalkUp", isMoving);
        }
        else
        {
            // Down animation
            sr.flipX = false;
            SetAnimation("PlayerWalkDown", isMoving);
        }
    }

    private void SetAnimation(string clipName, bool isMoving)
    {
        anim.Play(clipName);

        if (!isMoving)
        {
            anim.speed = 0f;                 // pause
            anim.Play(clipName, 0, 0f);      // frame 0
        }
        else
        {
            anim.speed = 1f;
        }
    }
}