using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GridWanderNpc2D : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float stepDuration = 0.45f;
    [SerializeField] private float minIdleSeconds = 0.9f;
    [SerializeField] private float maxIdleSeconds = 2.8f;

    [Header("Blocking")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float obstacleCheckRadius = 0.22f;
    [SerializeField] private bool ignoreTriggersForBlocking = true;

    [Header("Sprite")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool flipXWhenFacingLeft = true;

    // ANIMATION:
    [Header("Animation")]
    [SerializeField] private Animator animator;

    private Rigidbody2D _body;
    private Vector2 _cellStart;
    private Vector2 _cellEnd;
    private float _stepT;
    private bool _stepping;
    private float _idleUntil;

    private static readonly Vector2[] Cardinals =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right
    };

    private void Awake()
    {
        _body = GetComponent<Rigidbody2D>();
        _body.bodyType = RigidbodyType2D.Kinematic;
        _body.gravityScale = 0f;
        _body.constraints = RigidbodyConstraints2D.FreezeRotation;
        _body.linearVelocity = Vector2.zero;

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // ANIMATION:
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (_body == null)
            return;

        SnapPositionToGrid();
        _cellStart = _cellEnd = _body.position;
        _stepping = false;
        _stepT = 0f;
        _idleUntil = Time.time + RandomIdleDelay();

        // ANIMATION: Pause on first frame
        if (animator != null)
        {
            animator.speed = 0f;
            animator.Play("down", 0, 0f);
        }
    }

    private void Start()
    {
        SnapPositionToGrid();
        _cellStart = _cellEnd = _body.position;
        _idleUntil = Time.time + RandomIdleDelay();
    }

    private void FixedUpdate()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay || PauseMenuController.IsOpen)
            return;

        if (_stepping)
        {
            _stepT += Time.fixedDeltaTime / Mathf.Max(0.05f, stepDuration);
            var t = Mathf.Clamp01(_stepT);
            var p = Vector2.Lerp(_cellStart, _cellEnd, t);
            _body.MovePosition(p);

            if (_stepT < 1f)
            {
                // ANIMATION: keep playing while stepping
                if (animator != null) animator.speed = 1f;
                return;
            }

            _body.MovePosition(_cellEnd);
            _stepping = false;
            _idleUntil = Time.time + RandomIdleDelay();

            // ANIMATION: pause on frame 0 of same direction
            if (animator != null)
            {
                animator.speed = 0f;
                animator.Play(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, 0f);
            }

            return;
        }

        if (Time.time < _idleUntil)
            return;

        TryBeginRandomStep();
    }

    private void TryBeginRandomStep()
    {
        var origin = _cellEnd;
        var order0 = Random.Range(0, Cardinals.Length);

        for (var i = 0; i < Cardinals.Length; i++)
        {
            var dir = Cardinals[(order0 + i) % Cardinals.Length];
            var next = origin + dir * gridSize;
            if (!IsBlocked(next))
            {
                // -------------------------------------------------
                // ANIMATION SWITCHING
                // -------------------------------------------------
                if (animator != null)
                {
                    if (dir.y > 0.01f)
                        animator.Play("Up");
                    else if (dir.y < -0.01f)
                        animator.Play("Down");
                    else
                        animator.Play("Left"); // use left for both ? flipping handles right

                    animator.speed = 1f;
                }

                // SPRITE FLIP
                if (flipXWhenFacingLeft && spriteRenderer != null && Mathf.Abs(dir.x) > 0.01f)
                    spriteRenderer.flipX = dir.x > 0f; // right should flip

                _cellStart = origin;
                _cellEnd = next;
                _stepping = true;
                _stepT = 0f;
                return;
            }
        }

        _idleUntil = Time.time + RandomIdleDelay();
    }

    private bool IsBlocked(Vector2 worldPoint)
    {
        var hits = Physics2D.OverlapCircleAll(worldPoint, obstacleCheckRadius, obstacleMask);
        for (var i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (ignoreTriggersForBlocking && c.isTrigger) continue;
            return true;
        }
        return false;
    }

    private void SnapPositionToGrid()
    {
        var p = transform.position;
        var g = Mathf.Max(0.01f, gridSize);
        p.x = Mathf.Round(p.x / g) * g;
        p.y = Mathf.Round(p.y / g) * g;
        transform.position = p;
        _body.position = p;
    }

    private float RandomIdleDelay()
    {
        var a = Mathf.Min(minIdleSeconds, maxIdleSeconds);
        var b = Mathf.Max(minIdleSeconds, maxIdleSeconds);
        return Random.Range(a, b);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var g = Mathf.Max(0.01f, gridSize);
        var p = Application.isPlaying && _body != null ? (Vector3)_body.position : transform.position;
        Gizmos.color = Color.cyan * new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(p, obstacleCheckRadius);
        Gizmos.color = Color.green * new Color(1f, 1f, 1f, 0.5f);
        Gizmos.DrawLine(p + Vector3.left * g * 0.2f, p + Vector3.right * g * 0.2f);
        Gizmos.DrawLine(p + Vector3.down * g * 0.2f, p + Vector3.up * g * 0.2f);
    }
#endif
}