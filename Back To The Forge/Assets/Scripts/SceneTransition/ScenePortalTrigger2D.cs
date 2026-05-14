using UnityEngine;

/// <summary>
/// Trigger to load another scene. Forward mode remembers this scene and player position for
/// <see cref="PortalMode.ReturnToBookmark"/> on a portal in the destination scene.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class ScenePortalTrigger2D : MonoBehaviour
{
    public enum PortalMode
    {
        /// <summary>Save current scene + player pose, then load <see cref="targetSceneName"/>.</summary>
        ForwardToScene,
        /// <summary>Load the bookmarked scene and restore the saved pose (requires an earlier forward trip).</summary>
        ReturnToBookmark
    }

    [SerializeField] private PortalMode mode = PortalMode.ForwardToScene;
    [Tooltip("Scene name as in File > Build Settings. Used only in Forward mode.")]
    [SerializeField] private string targetSceneName;

    private bool _used;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"{nameof(ScenePortalTrigger2D)}: Collider2D on '{name}' should be a trigger.", this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        if (IsTransitionBlocked())
            return;

        if (_used)
            return;

        var player = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;

        if (mode == PortalMode.ForwardToScene)
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning($"{nameof(ScenePortalTrigger2D)}: '{name}' forward portal has no target scene.", this);
                return;
            }

            _used = true;
            SceneTransitionStore.BookmarkAndLoadScene(targetSceneName, player);
        }
        else
        {
            if (!SceneTransitionStore.HasBookmark)
            {
                Debug.LogWarning(
                    $"{nameof(ScenePortalTrigger2D)}: '{name}' return portal has nothing to return to yet. Enter through a forward portal in the other scene first.",
                    this);
                return;
            }

            _used = true;
            SceneTransitionStore.ReturnToBookmarkedScene();
        }
    }

    private static bool IsTransitionBlocked()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay)
            return true;

        var coordinator = Object.FindAnyObjectByType<CombatAdditiveCoordinator>();
        return coordinator != null && coordinator.IsCombatActiveOrLoading;
    }
}
