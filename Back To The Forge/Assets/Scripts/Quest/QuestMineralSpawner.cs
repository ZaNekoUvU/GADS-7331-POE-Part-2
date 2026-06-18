using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawns a <see cref="QuestMineralPickup"/> at a random point inside <see cref="spawnArea"/> when a forge quest is active.
/// </summary>
public class QuestMineralSpawner : MonoBehaviour
{
    public static QuestMineralSpawner Instance { get; private set; }

    [Tooltip("Empty = any scene. Otherwise only this scene name (case-sensitive).")]
    [SerializeField] private string onlyWhenSceneName = "";

    [SerializeField] private Collider2D spawnArea;

    [SerializeField] private QuestMineralPickup pickupPrefab;

    [SerializeField] private LayerMask obstacleMask = ~0;

    [SerializeField] private float clearanceRadius = 0.28f;

    [SerializeField] private int maxTries = 32;

    private GameObject _spawned;

    /// <summary>World position of the active commission pickup, if one exists in the loaded scene.</summary>
    public static bool TryGetActiveSpawnPosition(out Vector3 world)
    {
        world = default;
        if (Instance == null || Instance._spawned == null)
            return false;

        world = Instance._spawned.transform.position;
        return true;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        var q = ForgeQuestManager.Instance;
        if (q != null)
            q.OnForgeQuestChanged += OnQuestChanged;

        TrySpawn();
    }

    private void OnDisable()
    {
        var q = ForgeQuestManager.Instance;
        if (q != null)
            q.OnForgeQuestChanged -= OnQuestChanged;
    }

    private void OnQuestChanged()
    {
        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive)
        {
            ClearSpawned();
            return;
        }

        TrySpawn();
    }

    private void ClearSpawned()
    {
        if (_spawned != null)
        {
            Destroy(_spawned);
            _spawned = null;
        }
    }

    public void TrySpawn()
    {
        if (pickupPrefab == null || spawnArea == null)
            return;

        if (!string.IsNullOrEmpty(onlyWhenSceneName) &&
            SceneManager.GetActiveScene().name != onlyWhenSceneName)
            return;

        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive || q.OrePickedUp)
            return;

        if (_spawned != null)
            return;

        if (!TryRandomPointInCollider(spawnArea, out var world))
            return;

        _spawned = Instantiate(pickupPrefab.gameObject, world, Quaternion.identity);
        _spawned.name = $"QuestMineralPickup_{q.QuestMaterialName}";
    }

    private bool TryRandomPointInCollider(Collider2D zone, out Vector3 world)
    {
        var b = zone.bounds;
        world = b.center;

        for (var t = 0; t < maxTries; t++)
        {
            var x = Random.Range(b.min.x, b.max.x);
            var y = Random.Range(b.min.y, b.max.y);
            var p = new Vector2(x, y);
            if (!zone.OverlapPoint(p))
                continue;

            if (Physics2D.OverlapCircle(p, clearanceRadius, obstacleMask) != null)
                continue;

            world = new Vector3(p.x, p.y, 0f);
            return true;
        }

        world = b.center;
        return true;
    }
}
