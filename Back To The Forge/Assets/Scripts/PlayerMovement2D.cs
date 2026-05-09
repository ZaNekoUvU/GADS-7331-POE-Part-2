using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (moveAction != null)
            moveAction.action.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null)
            moveAction.action.Disable();
    }

    private void FixedUpdate()
    {
        if (moveAction == null)
            return;

        Vector2 input = moveAction.action.ReadValue<Vector2>();

        if (input.sqrMagnitude > 1f)
            input.Normalize();

        rb.linearVelocity = input * moveSpeed;
    }
}
