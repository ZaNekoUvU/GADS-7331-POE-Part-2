using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] private int itemId;
    [SerializeField] private string displayName = "Item";
    [Tooltip("Gold per unit when selling to the blacksmith. The daily special and forge-commission ore use the blacksmith's bonus multiplier on top of this.")]
    [SerializeField] private int baseSellPrice = 1;

    public int ItemId => itemId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
    public int BaseSellPrice => baseSellPrice;
}
