using UnityEngine;

/// <summary>
/// Spawns exploration visuals for hired units (kinematic followers). Place in exploration scenes.
/// </summary>
public sealed class HiredCompanionWorldPresenter : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private UnitPrefabRegistry unitPrefabRegistry;
    [SerializeField] private Transform companionParent;

    private GameObject _spawn1;
    private GameObject _spawn2;
    private GameObject _spawn3;

    private void Awake()
    {
        if (companionParent == null)
            companionParent = transform;

        ResolvePlayerTransform();
    }

    private void OnEnable()
    {
        var mgr = HiredCompanionManager.GetOrCreate();
        mgr.OnRosterChanged -= Rebuild;
        mgr.OnRosterChanged += Rebuild;
        Rebuild();
    }

    private void OnDisable()
    {
        var mgr = HiredCompanionManager.Instance;
        if (mgr != null)
            mgr.OnRosterChanged -= Rebuild;

        ClearSpawns();
    }

    private void ResolvePlayerTransform()
    {
        if (PlayerMovement2D.Instance != null)
        {
            player = PlayerMovement2D.Instance.transform;
            return;
        }

        if (player != null && player.gameObject.activeInHierarchy)
            return;

        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
            player = tagged.transform;
    }

    private void Rebuild()
    {
        ClearSpawns();
        ResolvePlayerTransform();

        var mgr = HiredCompanionManager.Instance;
        if (mgr == null || player == null)
        {
            if (mgr != null && player == null)
                Debug.LogWarning($"{nameof(HiredCompanionWorldPresenter)}: No player — add {nameof(PlayerMovement2D)} to the player.", this);
            return;
        }

        var anySlotNeedsPrefabSpawn =
            (mgr.Slot1UnitId > 0 && mgr.GetPhysicalFollowerRoot(0) == null)
            || (mgr.Slot2UnitId > 0 && mgr.GetPhysicalFollowerRoot(1) == null)
            || (mgr.Slot3UnitId > 0 && mgr.GetPhysicalFollowerRoot(2) == null);

        if (anySlotNeedsPrefabSpawn && unitPrefabRegistry == null)
        {
            Debug.LogWarning($"{nameof(HiredCompanionWorldPresenter)}: Assign {nameof(unitPrefabRegistry)} for hired units without a physical follower.", this);
            return;
        }

        if (mgr.Slot1UnitId > 0)
        {
            var physical = mgr.GetPhysicalFollowerRoot(0);
            if (physical != null)
                RefreshPhysicalFollower(physical, 0);
            else
                _spawn1 = SpawnFollower(mgr.Slot1UnitId, 0);
        }

        if (mgr.Slot2UnitId > 0)
        {
            var physical = mgr.GetPhysicalFollowerRoot(1);
            if (physical != null)
                RefreshPhysicalFollower(physical, 1);
            else
                _spawn2 = SpawnFollower(mgr.Slot2UnitId, 1);
        }

        if (mgr.Slot3UnitId > 0)
        {
            var physical = mgr.GetPhysicalFollowerRoot(2);
            if (physical != null)
                RefreshPhysicalFollower(physical, 2);
            else
                _spawn3 = SpawnFollower(mgr.Slot3UnitId, 2);
        }
    }

    private static void RefreshPhysicalFollower(GameObject root, int slotIndex)
    {
        if (root == null)
            return;

        var rb = root.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = root.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.simulated = true;

        var follower = root.GetComponent<CompanionFollower2D>();
        if (follower == null)
            follower = root.AddComponent<CompanionFollower2D>();

        var playerT = PlayerMovement2D.Instance != null
            ? PlayerMovement2D.Instance.transform
            : null;
        if (playerT == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                playerT = tagged.transform;
        }

        follower.Configure(playerT, slotIndex);
        follower.enabled = true;
    }

    private GameObject SpawnFollower(int unitId, int slotIndex)
    {
        if (!unitPrefabRegistry.TryGet(unitId, out _, out var prefab) || prefab == null)
        {
            Debug.LogWarning($"{nameof(HiredCompanionWorldPresenter)}: No registry prefab for unit id {unitId}.", this);
            return null;
        }

        var go = Instantiate(prefab, companionParent);
        go.name = $"HiredCompanionVisual_{unitId}_slot{slotIndex}";

        foreach (var cu in go.GetComponentsInChildren<CombatUnit>(true))
            cu.enabled = false;

        foreach (var hb in go.GetComponentsInChildren<CombatUnitHealthBar>(true))
            hb.enabled = false;

        var hbRoot = go.transform.Find("HealthBarRoot");
        if (hbRoot != null)
            hbRoot.gameObject.SetActive(false);

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = go.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.simulated = true;

        foreach (var col in go.GetComponentsInChildren<Collider2D>(true))
            col.isTrigger = true;

        var follower = go.GetComponent<CompanionFollower2D>();
        if (follower == null)
            follower = go.AddComponent<CompanionFollower2D>();

        follower.Configure(player, slotIndex);
        go.transform.position = player.position;
        return go;
    }

    private void ClearSpawns()
    {
        DestroySpawn(ref _spawn1);
        DestroySpawn(ref _spawn2);
        DestroySpawn(ref _spawn3);
    }

    private static void DestroySpawn(ref GameObject go)
    {
        if (go != null)
        {
            Destroy(go);
            go = null;
        }
    }
}
