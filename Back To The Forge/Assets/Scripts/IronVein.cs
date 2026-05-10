using UnityEngine;

/// <summary>
/// Place on an iron vein with a trigger <see cref="Collider2D"/>. Player mines by holding Interact (E) while in range.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class IronVein : MonoBehaviour
{
    [SerializeField] private ItemDefinition oreDefinition;
    [SerializeField] private int orePerTick = 1;
    [Tooltip("-1 = infinite ore.")]
    [SerializeField] private int totalOreAvailable = -1;

    public ItemDefinition OreDefinition => oreDefinition;

    public int OrePerTick => orePerTick;

    public bool HasOreLeft => oreDefinition != null && (totalOreAvailable < 0 || totalOreAvailable > 0);

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
            Debug.LogWarning($"{nameof(IronVein)} on '{name}': Collider2D should be a trigger so the player can enter.", this);
    }
}
