using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rolls per-day sell prices for gatherable resources (mining + combat drops). Brass costs more than wood, with variance inside each tier.
/// </summary>
public sealed class ResourceMarketPricing : MonoBehaviour
{
    public static ResourceMarketPricing Instance { get; private set; }

    private readonly Dictionary<int, int> _priceByItemId = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static ResourceMarketPricing GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<ResourceMarketPricing>();
        if (existing != null)
            return existing;

        var go = new GameObject($"[{nameof(ResourceMarketPricing)}]");
        return go.AddComponent<ResourceMarketPricing>();
    }

    /// <summary>Today's rolled sell price, or <see cref="ItemDefinition.BaseSellPrice"/> if not in the table.</summary>
    public int GetTodayPrice(ItemDefinition item)
    {
        if (item == null)
            return 0;

        if (item.ItemId > 0 && _priceByItemId.TryGetValue(item.ItemId, out var rolled))
            return rolled;

        return Mathf.Max(1, item.BaseSellPrice);
    }

    public void RollPricesForDay(int day, IEnumerable<ItemDefinition> catalog)
    {
        _priceByItemId.Clear();
        if (catalog == null)
            return;

        foreach (var item in catalog)
        {
            if (item == null || item.ItemId <= 0)
                continue;

            item.GetSellPriceRange(out var min, out var max);
            if (max < min)
                max = min;

            int price;
            if (min == max)
                price = min;
            else
            {
                var seed = unchecked(day * 7919 + item.ItemId * 104729);
                var rng = new System.Random(seed);
                price = rng.Next(min, max + 1);
            }

            _priceByItemId[item.ItemId] = price;
        }
    }
}
