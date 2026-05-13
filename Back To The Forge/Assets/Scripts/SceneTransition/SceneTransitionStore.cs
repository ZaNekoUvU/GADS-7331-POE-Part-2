using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Remembers which scene the player left and where to put them when that scene loads again
/// via <see cref="ReturnToBookmarkedScene"/>. Used by <see cref="ScenePortalTrigger2D"/> and
/// <see cref="PlayerReturnPlacement2D"/>.
/// </summary>
public static class SceneTransitionStore
{
    static bool _hasBookmark;
    static string _bookmarkSceneName;
    static Vector3 _bookmarkPosition;
    static float _bookmarkRotationZ;
    static bool _wantsPlacementAfterLoad;

    public static bool HasBookmark => _hasBookmark;

    /// <summary>Saves the current scene and player pose, then loads <paramref name="targetSceneName"/>.</summary>
    public static void BookmarkAndLoadScene(string targetSceneName, Transform player)
    {
        if (player == null)
        {
            Debug.LogWarning($"{nameof(SceneTransitionStore)}: Player transform is null; not loading scene.");
            return;
        }

        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning($"{nameof(SceneTransitionStore)}: Target scene name is empty.");
            return;
        }

        var scene = player.gameObject.scene;
        if (!scene.IsValid())
        {
            Debug.LogWarning($"{nameof(SceneTransitionStore)}: Player is not in a loaded scene.");
            return;
        }

        _bookmarkSceneName = scene.name;
        _bookmarkPosition = player.position;
        _bookmarkRotationZ = player.eulerAngles.z;
        _hasBookmark = true;
        _wantsPlacementAfterLoad = false;
        SceneManager.LoadScene(targetSceneName);
    }

    /// <summary>Loads the bookmarked scene and marks pending placement at the saved position.</summary>
    public static void ReturnToBookmarkedScene()
    {
        if (!_hasBookmark || string.IsNullOrEmpty(_bookmarkSceneName))
        {
            Debug.LogWarning(
                $"{nameof(SceneTransitionStore)}: No bookmarked scene. Use a forward portal first or assign the return portal after exiting through the matching forward portal.");
            return;
        }

        _wantsPlacementAfterLoad = true;
        SceneManager.LoadScene(_bookmarkSceneName);
    }

    /// <summary>If the scene just loaded from a return portal, applies pose and clears the bookmark.</summary>
    public static bool TryApplyBookmarkedPlacement(Transform t, Rigidbody2D rb)
    {
        if (!_wantsPlacementAfterLoad || !_hasBookmark || t == null)
            return false;

        var pos = _bookmarkPosition;
        var rotZ = _bookmarkRotationZ;
        _wantsPlacementAfterLoad = false;
        _hasBookmark = false;

        t.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, rotZ));
        if (rb != null)
        {
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        return true;
    }
}
