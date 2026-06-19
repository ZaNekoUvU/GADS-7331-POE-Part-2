using UnityEngine;

/// <summary>
/// Marks the authoritative spawn point for a play session (exploration scene start).
/// Created automatically on first gameplay load if the scene has no marker yet.
/// </summary>
public sealed class PlayerStartLocation : MonoBehaviour
{
    private static PlayerStartLocation _instance;

    public static bool TryGetWorldPose(out Vector3 position, out float rotationZ)
    {
        if (_instance == null)
            _instance = FindAnyObjectByType<PlayerStartLocation>();

        if (_instance == null)
        {
            position = default;
            rotationZ = 0f;
            return false;
        }

        position = _instance.transform.position;
        rotationZ = _instance.transform.eulerAngles.z;
        return true;
    }

    /// <summary>
    /// Ensures a start marker exists at the player's current pose (call on the first gameplay frame).
    /// </summary>
    public static void EnsureFromPlayerPoseIfMissing(Transform playerTransform)
    {
        if (playerTransform == null || _instance != null || FindAnyObjectByType<PlayerStartLocation>() != null)
            return;

        var go = new GameObject(nameof(PlayerStartLocation));
        go.transform.SetPositionAndRotation(playerTransform.position, playerTransform.rotation);
        go.AddComponent<PlayerStartLocation>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }
}
