using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns <see cref="CompanionRecruiter"/> NPCs from a <see cref="MercenaryRosterCatalog"/> at play start.
/// Prefers ordered child transforms under <see cref="orderedSpawnPointsParent"/> or a child named <c>MercenarySpawnPoints</c>.
/// Optional physics solver nudges anchors only when enabled.
/// </summary>
public class MercenaryCampSpawner : MonoBehaviour
{
    private const string SpawnPointsChildName = "MercenarySpawnPoints";

    [SerializeField] private MercenaryRosterCatalog catalog;
    [SerializeField] private CompanionRecruiter recruiterPrefab;

    [Header("Editor placement")]
    [Tooltip("Optional. Child index must match the roster array index (slot 0 = first recruit entry). World positions define exact spawn spots.")]
    [SerializeField] private Transform orderedSpawnPointsParent;

    [Tooltip("When off (default), anchors are used exactly — match markers in-scene. When on, uses anchors only as seeds for obstacle/separation solver.")]
    [SerializeField] private bool runPhysicsSpawnSolverWhenAnchorsPresent;

    [Tooltip("When using exact anchors, spawn Z uses this Transform's Z instead of each anchor's Z (keeps sorting consistent).")]
    [SerializeField] private bool useSpawnerZForAnchoredSpawns = true;

    [Header("Spawn clearance (solver)")]
    [Tooltip("Layers queried for overlaps (solid + trigger colliders).")]
    [SerializeField] private LayerMask obstacleMask = ~0;

    [SerializeField] private float clearanceRadius = 0.65f;

    [Tooltip("Minimum distance between two spawned mercenary anchors when the solver runs.")]
    [SerializeField] private float minMercenarySeparation = 1.55f;

    [Tooltip("Extra tries spiral outward from the desired position when the preferred spot overlaps obstacles.")]
    [SerializeField] private int maxSpawnResolutionAttempts = 96;

    [SerializeField] private float maxSearchRadius = 10f;

    private Transform _resolvedSpawnPointsParent;

    private readonly Collider2D[] _overlapScratch = new Collider2D[32];

    private void Awake()
    {
        _resolvedSpawnPointsParent = orderedSpawnPointsParent != null
            ? orderedSpawnPointsParent
            : transform.Find(SpawnPointsChildName);
    }

    private Transform AnchorsRoot => _resolvedSpawnPointsParent;

    private void Start()
    {
        if (catalog == null || catalog.Recruits == null || catalog.Recruits.Length == 0)
        {
            Debug.LogWarning($"{nameof(MercenaryCampSpawner)}: No catalog assigned.", this);
            return;
        }

        MercenaryOfferLookup.RegisterCatalog(catalog);

        var template = recruiterPrefab != null ? recruiterPrefab : catalog.RecruiterPrefab;
        if (template == null)
        {
            Debug.LogError($"{nameof(MercenaryCampSpawner)}: Assign a recruiter prefab on the catalog or spawner.", this);
            return;
        }

        DisableLegacyHirePosts();

        var priorSpawnFootprints = new List<Vector2>();
        var recruits = catalog.Recruits;
        for (var rosterSlot = 0; rosterSlot < recruits.Length; rosterSlot++)
        {
            var entry = recruits[rosterSlot];
            if (entry.offer == null)
                continue;

            var desired = GetDesiredSpawnFootprint(rosterSlot, entry);
            Vector3 spawnWorld;
            if (HasSpawnAnchor(rosterSlot) && !runPhysicsSpawnSolverWhenAnchorsPresent)
            {
                var anchor = AnchorsRoot.GetChild(rosterSlot).position;
                var z = useSpawnerZForAnchoredSpawns ? transform.position.z : anchor.z;
                spawnWorld = new Vector3(anchor.x, anchor.y, z);
            }
            else
                spawnWorld = ResolveSpawnWorldPosition(desired, priorSpawnFootprints);

            priorSpawnFootprints.Add(new Vector2(spawnWorld.x, spawnWorld.y));

            var recruiter = Instantiate(template, spawnWorld, Quaternion.identity, transform);

            recruiter.gameObject.name = $"Mercenary — {entry.offer.NpcDisplayName}";
            recruiter.ConfigureFromOffer(entry.offer, entry.spriteTint);
            recruiter.CommitSpawnPoseSnapshot();

            var sprite = recruiter.GetComponentInChildren<SpriteRenderer>();
            if (sprite != null)
                sprite.color = entry.spriteTint;
        }
    }

    private bool HasSpawnAnchor(int rosterSlotIndex)
    {
        return AnchorsRoot != null &&
               rosterSlotIndex >= 0 &&
               rosterSlotIndex < AnchorsRoot.childCount &&
               AnchorsRoot.GetChild(rosterSlotIndex) != null;
    }

    private Vector2 GetDesiredSpawnFootprint(int rosterSlotIndex, MercenaryRosterCatalog.SpawnEntry entry)
    {
        if (HasSpawnAnchor(rosterSlotIndex))
            return AnchorsRoot.GetChild(rosterSlotIndex).position;

        return entry.worldPosition;
    }

    /// <summary>
    /// Catalog positions are 2D design anchors; we keep Z aligned with this spawner for consistent sorting depth.
    /// </summary>
    private Vector3 ResolveSpawnWorldPosition(Vector2 desired, List<Vector2> priorSpawnFootprints)
    {
        var z = transform.position.z;

        if (clearanceRadius <= 0f && minMercenarySeparation <= 0f)
            return new Vector3(desired.x, desired.y, z);

        if (IsSpawnFootprintClear(desired, priorSpawnFootprints))
            return new Vector3(desired.x, desired.y, z);

        var goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (var attempt = 1; attempt < maxSpawnResolutionAttempts; attempt++)
        {
            var t = attempt / (float)maxSpawnResolutionAttempts;
            var radius = Mathf.Min(Mathf.Sqrt(t) * maxSearchRadius, maxSearchRadius);
            var theta = goldenAngle * attempt;
            var offset = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * radius;
            var candidate = desired + offset;

            if (IsSpawnFootprintClear(candidate, priorSpawnFootprints))
                return new Vector3(candidate.x, candidate.y, z);
        }

        return new Vector3(desired.x, desired.y, z);
    }

    /// <summary>
    /// Uses trigger + non-trigger overlaps so props like trees (large trigger + tight poly) still block spawns.
    /// Ignores colliders on <see cref="CompanionRecruiter"/> — separation vs already spawned mercs uses distance checks.
    /// </summary>
    private bool IsSpawnFootprintClear(Vector2 center, List<Vector2> priorSpawnFootprints)
    {
        var filter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = true,
            layerMask = obstacleMask
        };

        var count = Physics2D.OverlapCircle(center, clearanceRadius, filter, _overlapScratch);
        for (var i = 0; i < count; i++)
        {
            var c = _overlapScratch[i];
            if (c == null)
                continue;

            if (c.GetComponentInParent<CompanionRecruiter>() != null)
                continue;

            return false;
        }

        var sepSq = minMercenarySeparation * minMercenarySeparation;
        if (sepSq <= 0f)
            return true;

        for (var i = 0; i < priorSpawnFootprints.Count; i++)
        {
            if ((priorSpawnFootprints[i] - center).sqrMagnitude < sepSq)
                return false;
        }

        return true;
    }

    private static void DisableLegacyHirePosts()
    {
        var legacy = GameObject.Find("Mercenary Hire Post");
        if (legacy != null)
            legacy.SetActive(false);
    }
}
