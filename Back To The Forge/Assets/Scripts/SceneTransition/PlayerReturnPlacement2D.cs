using UnityEngine;

/// <summary>
/// After a return portal loads the previous scene, moves this transform (and optional Rigidbody2D)
/// to the pose that was saved when the player used the matching forward portal.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class PlayerReturnPlacement2D : MonoBehaviour
{
    private void Start()
    {
        var rb = GetComponent<Rigidbody2D>();
        SceneTransitionStore.TryApplyBookmarkedPlacement(transform, rb);
    }
}
