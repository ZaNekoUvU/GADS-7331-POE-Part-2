using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    public enum ResourcePriceTier
    {
        [Tooltip("Fixed baseSellPrice (quest items, specials).")]
        UseBasePrice = 0,
        Scrap = 1,
        Common = 2,
        Standard = 3,
        Industrial = 4,
        Valuable = 5,
        Premium = 6
    }

    [SerializeField] private int itemId;
    [SerializeField] private string displayName = "Item";
    [Tooltip("Fallback gold/unit when no market roll applies.")]
    [SerializeField] private int baseSellPrice = 1;
    [Tooltip("Daily sell range for mined / combat loot. UseBasePrice keeps baseSellPrice fixed.")]
    [SerializeField] private ResourcePriceTier priceTier = ResourcePriceTier.Standard;

    public int ItemId => itemId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public int BaseSellPrice => baseSellPrice;
    public ResourcePriceTier PriceTier => priceTier;

    /// <summary>Min/max gold per unit before daily roll (tier table or fixed base).</summary>
    public void GetSellPriceRange(out int min, out int max)
    {
        if (priceTier == ResourcePriceTier.UseBasePrice)
        {
            min = max = Mathf.Max(1, baseSellPrice);
            return;
        }

        switch (priceTier)
        {
            case ResourcePriceTier.Scrap:
                min = 2;
                max = 4;
                break;
            case ResourcePriceTier.Common:
                min = 3;
                max = 5;
                break;
            case ResourcePriceTier.Standard:
                min = 4;
                max = 7;
                break;
            case ResourcePriceTier.Industrial:
                min = 6;
                max = 9;
                break;
            case ResourcePriceTier.Valuable:
                min = 8;
                max = 12;
                break;
            case ResourcePriceTier.Premium:
                min = 11;
                max = 16;
                break;
            default:
                min = max = Mathf.Max(1, baseSellPrice);
                break;
        }
    }
}
