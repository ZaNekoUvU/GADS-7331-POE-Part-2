using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World resource node (iron vein, tree, stone pile, etc.): trigger <see cref="Collider2D"/>,
/// assign a <see cref="ItemDefinition"/>. Player gathers by holding Interact while in range (see <see cref="PlayerMiningController"/>).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IronVein : MonoBehaviour
{
    private static readonly HashSet<IronVein> PlayerOverlapping = new();

    public static event Action OnPlayerOverlapChanged;

    [SerializeField] private ItemDefinition oreDefinition;
    [SerializeField] private int orePerTick = 1;
    [Tooltip("-1 = infinite ore.")]
    [SerializeField] private int totalOreAvailable = -1;

    [Tooltip("Extra reach beyond collider bounds for gather prompts / targeting.")]
    [SerializeField] private float gatherReachPadding = 0.35f;

    public ItemDefinition OreDefinition => oreDefinition;

    public int OrePerTick => orePerTick;

    public bool HasOreLeft => oreDefinition != null && (totalOreAvailable < 0 || totalOreAvailable > 0);

    /// <summary>Closest gather node at <paramref name="playerPosition"/> (triggers, overlap, or padded reach).</summary>
    public static bool TryGetGatherNodeAtPosition(Vector2 playerPosition, out IronVein vein)
    {
        if (TryGetClosestPlayerOverlap(playerPosition, out vein))
            return true;

        vein = null;
        var bestSqr = float.PositiveInfinity;

        var veins = UnityEngine.Object.FindObjectsByType<IronVein>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var candidate in veins)
        {
            if (candidate == null || !candidate.HasOreLeft || !candidate.ContainsGatherPoint(playerPosition))
                continue;

            var d = ((Vector2)candidate.transform.position - playerPosition).sqrMagnitude;
            if (d >= bestSqr)
                continue;

            bestSqr = d;
            vein = candidate;
        }

        return vein != null;
    }

    /// <summary>Closest gather node the player is currently inside (trigger overlap).</summary>
    public static bool TryGetClosestPlayerOverlap(Vector3 playerPosition, out IronVein vein)
    {
        vein = null;
        var bestSqr = float.PositiveInfinity;

        foreach (var candidate in PlayerOverlapping)
        {
            if (candidate == null || !candidate.HasOreLeft)
                continue;

            var d = (candidate.transform.position - playerPosition).sqrMagnitude;
            if (d >= bestSqr)
                continue;

            bestSqr = d;
            vein = candidate;
        }

        return vein != null;
    }

    private bool ContainsGatherPoint(Vector2 worldPosition)
    {
        var cols = GetComponentsInChildren<Collider2D>();
        for (var i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null || !col.enabled)
                continue;

            if (col.OverlapPoint(worldPosition))
                return true;

            if (gatherReachPadding > 0f)
            {
                var closest = col.ClosestPoint(worldPosition);
                if ((closest - worldPosition).sqrMagnitude <= gatherReachPadding * gatherReachPadding)
                    return true;
            }
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        if (PlayerOverlapping.Add(this))
            OnPlayerOverlapChanged?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        if (PlayerOverlapping.Remove(this))
            OnPlayerOverlapChanged?.Invoke();
    }

    private void OnDisable()
    {
        if (PlayerOverlapping.Remove(this))
            OnPlayerOverlapChanged?.Invoke();
    }

    /// <summary>Nearest active world node that yields <paramref name="ore"/>.</summary>
    public static bool TryGetNearestWorldPosition(ItemDefinition ore, Vector3 fromWorld, out Vector3 position)
    {
        position = default;
        if (ore == null)
            return false;

        IronVein nearest = null;
        var bestSqr = float.PositiveInfinity;

        var veins = UnityEngine.Object.FindObjectsByType<IronVein>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var vein in veins)
        {
            if (vein == null || !vein.HasOreLeft || !MatchesOre(vein.OreDefinition, ore))
                continue;

            var d = (vein.transform.position - fromWorld).sqrMagnitude;
            if (d >= bestSqr)
                continue;

            bestSqr = d;
            nearest = vein;
        }

        if (nearest == null)
            return false;

        position = nearest.transform.position;
        return true;
    }

    private static bool MatchesOre(ItemDefinition veinOre, ItemDefinition target)
    {
        if (veinOre == null || target == null)
            return false;

        if (veinOre.ItemId > 0 && target.ItemId > 0)
            return veinOre.ItemId == target.ItemId;

        return veinOre == target;
    }

    /// <summary>Call only after ore was successfully placed in inventory (finite veins decrement).</summary>
    public void RegisterSuccessfulMine()
    {
        if (totalOreAvailable < 0)
            return;

        totalOreAvailable = Mathf.Max(0, totalOreAvailable - orePerTick);
        if (totalOreAvailable <= 0)
            OnDepleted();
    }

    private void OnDepleted()
    {
        if (PlayerOverlapping.Remove(this))
            OnPlayerOverlapChanged?.Invoke();

        foreach (var c in GetComponentsInChildren<Collider2D>())
            c.enabled = false;

        foreach (var r in GetComponentsInChildren<SpriteRenderer>())
        {
            var color = r.color;
            color.a = 0.35f;
            r.color = color;
        }
    }

    private void OnValidate()
    {
        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"{nameof(IronVein)} on '{name}': Collider2D should be a trigger so the player can overlap.", this);
    }
}
