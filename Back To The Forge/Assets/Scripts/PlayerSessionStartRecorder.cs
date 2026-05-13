using UnityEngine;

/// <summary>
/// Captures the player's position on the first frame of a play session (after other early placement scripts).
/// Used when ending the forging day to send them back to where the run began.
/// </summary>
[DefaultExecutionOrder(0)]
public sealed class PlayerSessionStartRecorder : MonoBehaviour
{
    private Vector3 _startPosition;
    private float _startRotationZ;
    private bool _captured;

    private void Start()
    {
        CaptureIfNeeded();
    }

    private void CaptureIfNeeded()
    {
        if (_captured)
            return;

        _startPosition = transform.position;
        _startRotationZ = transform.eulerAngles.z;
        _captured = true;
    }

    /// <summary>Moves the tagged player back to the pose stored on first <see cref="Start"/>.</summary>
    public static void ResetToRecordedStart(Transform playerTransform, Rigidbody2D rb)
    {
        if (playerTransform == null)
            return;

        var rec = playerTransform.GetComponent<PlayerSessionStartRecorder>();
        if (rec == null || !rec._captured)
            return;

        playerTransform.SetPositionAndRotation(rec._startPosition, Quaternion.Euler(0f, 0f, rec._startRotationZ));
        if (rb != null)
        {
            rb.position = rec._startPosition;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}
