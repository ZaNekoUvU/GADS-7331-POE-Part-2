using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Item";

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
}
