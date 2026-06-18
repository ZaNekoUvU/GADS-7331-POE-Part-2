using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Points a floating <see cref="QuestWaypointArrow"/> at the blacksmith until a forge quest is accepted,
/// then at the spawned commission ore until it is picked up, then at supplementary veins (iron, wood, etc.)
/// if still needed, otherwise back at the blacksmith for turn-in.
/// </summary>
[DisallowMultipleComponent]
public class QuestWaypointDirector : MonoBehaviour
{
    public static QuestWaypointDirector Instance { get; private set; }

    [Tooltip("Scene where the waypoint is shown. Empty = Exploration Scene.")]
    [SerializeField] private string explorationSceneName = "Exploration Scene";

    private QuestWaypointArrow _arrow;
    private Transform _blacksmithTransform;
    private CombatAdditiveCoordinator _combatCoordinator;
    private Inventory _inventory;
    private bool _subscribedToForgeQuest;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfMissing()
    {
        if (Instance != null || FindAnyObjectByType<QuestWaypointDirector>() != null)
            return;

        var go = new GameObject($"[{nameof(QuestWaypointDirector)}]");
        go.AddComponent<QuestWaypointDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        var arrowGo = new GameObject("QuestWaypointArrow");
        arrowGo.transform.SetParent(transform, false);
        _arrow = arrowGo.AddComponent<QuestWaypointArrow>();
        _arrow.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureForgeQuestSubscription();
        RebindInventorySubscription();
        InvalidateSceneCaches();
        RefreshWaypoint();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeForgeQuest();
        UnsubscribeInventory();
        _arrow?.SetWorldTarget(null);
    }

    private void EnsureForgeQuestSubscription()
    {
        if (_subscribedToForgeQuest)
            return;

        var q = ForgeQuestManager.GetOrCreate();
        if (q == null)
            return;

        q.OnForgeQuestChanged += OnForgeQuestChanged;
        _subscribedToForgeQuest = true;
    }

    private void UnsubscribeForgeQuest()
    {
        if (!_subscribedToForgeQuest)
            return;

        var q = ForgeQuestManager.Instance;
        if (q != null)
            q.OnForgeQuestChanged -= OnForgeQuestChanged;

        _subscribedToForgeQuest = false;
    }

    private void InvalidateSceneCaches()
    {
        _blacksmithTransform = null;
        _combatCoordinator = null;
    }

    private void LateUpdate()
    {
        EnsureForgeQuestSubscription();
        RefreshWaypoint();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _inventory = null;
        InvalidateSceneCaches();
        RebindInventorySubscription();
        RefreshWaypoint();
    }

    private void OnForgeQuestChanged()
    {
        RefreshWaypoint();
    }

    private void OnInventoryChanged()
    {
        RefreshWaypoint();
    }

    private void RebindInventorySubscription()
    {
        var target = FindAnyObjectByType<Inventory>();
        if (_inventory == target)
            return;

        UnsubscribeInventory();
        _inventory = target;

        if (_inventory != null)
            _inventory.OnChanged += OnInventoryChanged;
    }

    private void UnsubscribeInventory()
    {
        if (_inventory != null)
            _inventory.OnChanged -= OnInventoryChanged;
        _inventory = null;
    }

    private void RefreshWaypoint()
    {
        if (_arrow == null)
            return;

        if (!ShouldShowWaypoint())
        {
            _arrow.SetWorldTarget(null);
            return;
        }

        var player = PlayerMovement2D.Instance;
        if (player == null)
        {
            _arrow.SetWorldTarget(null);
            return;
        }

        _arrow.SetFollow(player.transform);

        if (!TryResolveWaypointTarget(player.transform.position, out var target))
        {
            _arrow.SetWorldTarget(null);
            return;
        }

        _arrow.SetWorldTarget(target);
    }

    private bool ShouldShowWaypoint()
    {
        if (PauseMenuController.IsOpen
            || SimpleRpgDialogueUI.IsDialogueOpen
            || ForgeQuestChoiceUI.IsBlockingGameplay)
            return false;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return false;

        if (scene.name == PauseMenuController.DefaultMainMenuSceneName)
            return false;

        if (!string.IsNullOrEmpty(explorationSceneName) && scene.name != explorationSceneName)
            return false;

        var coordinator = _combatCoordinator != null ? _combatCoordinator : FindAnyObjectByType<CombatAdditiveCoordinator>();
        if (coordinator != null)
            _combatCoordinator = coordinator;

        if (coordinator != null && coordinator.IsCombatActiveOrLoading)
            return false;

        return true;
    }

    private bool TryResolveWaypointTarget(Vector3 playerPosition, out Vector3 target)
    {
        target = default;

        var forge = ForgeQuestManager.Instance;
        if (forge != null && forge.QuestActive)
        {
            if (!HasCollectedMainCommission(forge))
            {
                if (QuestMineralSpawner.TryGetActiveSpawnPosition(out target))
                    return true;
            }
            else if (forge.IsMissingSupplementaryTurnIn(_inventory)
                     && forge.ForgeIronTurnInItem != null
                     && IronVein.TryGetNearestWorldPosition(forge.ForgeIronTurnInItem, playerPosition, out target))
            {
                return true;
            }
            else
            {
                var blacksmith = ResolveBlacksmithTransform();
                if (blacksmith != null)
                {
                    target = blacksmith.position;
                    return true;
                }
            }
        }

        var smith = ResolveBlacksmithTransform();
        if (smith == null)
            return false;

        target = smith.position;
        return true;
    }

    private bool HasCollectedMainCommission(ForgeQuestManager forge)
    {
        if (forge == null || !forge.QuestActive)
            return false;

        return forge.OrePickedUp || forge.HasCommissionOreInInventory(_inventory);
    }

    private Transform ResolveBlacksmithTransform()
    {
        if (_blacksmithTransform != null)
            return _blacksmithTransform;

        var giver = FindAnyObjectByType<BlacksmithQuestGiver>();
        if (giver != null)
        {
            _blacksmithTransform = giver.transform;
            return _blacksmithTransform;
        }

        var smith = BlacksmithMaster.ResolveEconomy();
        if (smith != null)
            _blacksmithTransform = smith.transform;

        return _blacksmithTransform;
    }
}
