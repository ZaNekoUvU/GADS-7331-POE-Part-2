using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World resource node (iron vein, tree, stone pile, etc.): trigger <see cref="Collider2D"/>,
/// assign a <see cref="ItemDefinition"/>. Player gathers by holding Interact while in range (see <see cref="PlayerMiningController"/>).
/// Ore is finite; depleted nodes restore when the calendar day advances.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IronVein : MonoBehaviour
{
    private static readonly HashSet<IronVein> PlayerOverlapping = new();

    public static event Action OnPlayerOverlapChanged;

    [SerializeField] private ItemDefinition oreDefinition;
    [SerializeField] private int orePerTick = 1;
    [SerializeField] private int maxOreCapacity = 10;
    [SerializeField] private int totalOreAvailable = 10;

    [Tooltip("Extra reach beyond collider bounds for gather prompts / targeting.")]
    [SerializeField] private float gatherReachPadding = 0.35f;

    private readonly Dictionary<SpriteRenderer, float> _defaultSpriteAlphas = new();
    private bool _isDepleted;

    public ItemDefinition OreDefinition => oreDefinition;

    public int OrePerTick => orePerTick;

    public int MaxOreCapacity => maxOreCapacity;

    public int RemainingOre => totalOreAvailable;

    public bool HasOreLeft => oreDefinition != null && totalOreAvailable > 0;

    public bool IsInGatherRange(Vector2 worldPosition) => HasOreLeft && ContainsGatherPoint(worldPosition);

    public float GetRemainingFraction()
    {
        if (maxOreCapacity <= 0)
            return 0f;

        return Mathf.Clamp01((float)totalOreAvailable / maxOreCapacity);
    }

    /// <summary>Smooth bar drain between ore ticks while mining.</summary>
    public float GetDisplayedRemainingFraction(bool isMiningThisVein, float miningTickProgress01)
    {
        if (maxOreCapacity <= 0)
            return 0f;

        var displayed = (float)totalOreAvailable;
        if (isMiningThisVein && orePerTick > 0)
            displayed -= (1f - Mathf.Clamp01(miningTickProgress01)) * orePerTick;

        return Mathf.Clamp01(displayed / maxOreCapacity);
    }

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

    public static void RestoreAllForNewDay()
    {
        var veins = UnityEngine.Object.FindObjectsByType<IronVein>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var vein in veins)
        {
            if (vein != null)
                vein.RestoreForNewDay();
        }
    }

    private void Awake()
    {
        NormalizeCapacityFields();
        CacheDefaultSpriteAlphas();

        if (GetComponent<IronVeinResourceBar>() == null)
            gameObject.AddComponent<IronVeinResourceBar>();
    }

    private void NormalizeCapacityFields()
    {
        if (maxOreCapacity < 1)
            maxOreCapacity = totalOreAvailable > 0 ? totalOreAvailable : 10;

        if (totalOreAvailable < 0)
            totalOreAvailable = maxOreCapacity;

        totalOreAvailable = Mathf.Clamp(totalOreAvailable, 0, maxOreCapacity);

        if (totalOreAvailable <= 0)
            ApplyDepletedState();
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
        if (!HasOreLeft)
            return;

        totalOreAvailable = Mathf.Max(0, totalOreAvailable - orePerTick);
        if (totalOreAvailable <= 0)
            ApplyDepletedState();
    }

    public void RestoreForNewDay()
    {
        totalOreAvailable = maxOreCapacity;
        RestoreActiveState();
    }

    private void ApplyDepletedState()
    {
        if (_isDepleted)
            return;

        _isDepleted = true;

        if (PlayerOverlapping.Remove(this))
            OnPlayerOverlapChanged?.Invoke();

        foreach (var c in GetComponentsInChildren<Collider2D>())
            c.enabled = false;

        foreach (var r in GetComponentsInChildren<SpriteRenderer>())
        {
            if (r == null)
                continue;

            var color = r.color;
            color.a = 0.35f;
            r.color = color;
        }
    }

    private void RestoreActiveState()
    {
        _isDepleted = false;

        foreach (var c in GetComponentsInChildren<Collider2D>())
            c.enabled = true;

        foreach (var r in GetComponentsInChildren<SpriteRenderer>())
        {
            if (r == null)
                continue;

            var color = r.color;
            color.a = _defaultSpriteAlphas.TryGetValue(r, out var alpha) ? alpha : 1f;
            r.color = color;
        }
    }

    private void CacheDefaultSpriteAlphas()
    {
        _defaultSpriteAlphas.Clear();
        foreach (var r in GetComponentsInChildren<SpriteRenderer>())
        {
            if (r != null)
                _defaultSpriteAlphas[r] = r.color.a;
        }
    }

    private void OnValidate()
    {
        if (maxOreCapacity < 1)
            maxOreCapacity = 10;

        if (totalOreAvailable < 0)
            totalOreAvailable = maxOreCapacity;

        totalOreAvailable = Mathf.Clamp(totalOreAvailable, 0, maxOreCapacity);

        var col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"{nameof(IronVein)} on '{name}': Collider2D should be a trigger so the player can overlap.", this);
    }
}
