using UnityEngine;

/// <summary>
/// Remembers the player's session start pose for death respawn and end-of-day return.
/// Prefer <see cref="PlayerStartLocation"/> when present; otherwise captures on first gameplay frame.
/// </summary>
[DefaultExecutionOrder(-40)]
public sealed class PlayerSessionStartRecorder : MonoBehaviour
{
    private Vector3 _startPosition;
    private float _startRotationZ;
    private bool _captured;

    private static Vector3 s_fallbackStartPosition;
    private static float s_fallbackStartRotationZ;
    private static bool s_hasFallbackStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_fallbackStartPosition = default;
        s_fallbackStartRotationZ = 0f;
        s_hasFallbackStart = false;
    }

    private void Awake()
    {
        TryCaptureFromAuthoritativeStart();
    }

    /// <summary>Captures session start if not recorded yet (safe to call from gameplay bootstrap).</summary>
    public static void CaptureSessionStartIfNeeded(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        var rec = playerTransform.GetComponent<PlayerSessionStartRecorder>();
        if (rec == null)
            rec = playerTransform.gameObject.AddComponent<PlayerSessionStartRecorder>();

        rec.TryCaptureFromAuthoritativeStart();
    }

    private void TryCaptureFromAuthoritativeStart()
    {
        if (_captured)
            return;

        if (PlayerStartLocation.TryGetWorldPose(out var markerPos, out var markerRotZ))
        {
            _startPosition = markerPos;
            _startRotationZ = markerRotZ;
        }
        else
        {
            _startPosition = transform.position;
            _startRotationZ = transform.eulerAngles.z;
        }

        _captured = true;
        s_fallbackStartPosition = _startPosition;
        s_fallbackStartRotationZ = _startRotationZ;
        s_hasFallbackStart = true;
    }

    /// <summary>Moves the player back to the recorded session start pose.</summary>
    public static void ResetToRecordedStart(Transform playerTransform, Rigidbody2D rb)
    {
        if (playerTransform == null)
            return;

        if (!TryGetSessionStartPose(playerTransform, out var pos, out var rotZ))
        {
            Debug.LogWarning("[PlayerSessionStart] No session start pose recorded — player was not moved.");
            return;
        }

        playerTransform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, rotZ));
        if (rb != null)
        {
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private static bool TryGetSessionStartPose(Transform playerTransform, out Vector3 position, out float rotationZ)
    {
        if (PlayerStartLocation.TryGetWorldPose(out position, out rotationZ))
            return true;

        var rec = playerTransform.GetComponent<PlayerSessionStartRecorder>();
        if (rec != null && rec._captured)
        {
            position = rec._startPosition;
            rotationZ = rec._startRotationZ;
            return true;
        }

        if (s_hasFallbackStart)
        {
            position = s_fallbackStartPosition;
            rotationZ = s_fallbackStartRotationZ;
            return true;
        }

        position = default;
        rotationZ = 0f;
        return false;
    }
}
