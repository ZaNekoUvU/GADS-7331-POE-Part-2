using UnityEngine;

/// <summary>
/// Trigger pickup that grants the active forge-quest item (one unit) and notifies <see cref="ForgeQuestManager"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class QuestMineralPickup : MonoBehaviour
{
    private bool _pickedUp;

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_pickedUp)
            return;

        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        _pickedUp = true;

        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive || q.QuestItemAsset == null)
            return;

        var inv = ResolvePlayerInventory(other);
        if (inv == null)
        {
            Debug.LogWarning($"{nameof(QuestMineralPickup)}: No player {nameof(Inventory)} found.", this);
            return;
        }

        var leftover = inv.TryAdd(q.QuestItemAsset, 1, Inventory.ItemAddContext.Pickup, "forge commission");
        if (leftover > 0)
        {
            Debug.LogWarning($"{nameof(QuestMineralPickup)}: Inventory full — could not add commission ore.", this);
            return;
        }

        q.MarkOrePickedUp();
        Destroy(gameObject);
    }

    private static Inventory ResolvePlayerInventory(Collider2D other)
    {
        var pm = PlayerMovement2D.Instance;
        if (pm != null)
        {
            if (pm.TryGetComponent<Inventory>(out var onPlayer))
                return onPlayer;

            var onHierarchy = pm.GetComponentInChildren<Inventory>(true);
            if (onHierarchy == null)
                onHierarchy = pm.GetComponentInParent<Inventory>();
            if (onHierarchy != null)
                return onHierarchy;
        }

        var rb = other.attachedRigidbody;
        if (rb != null)
        {
            if (rb.TryGetComponent<Inventory>(out var onRb))
                return onRb;

            var onRbHierarchy = rb.GetComponentInChildren<Inventory>(true);
            if (onRbHierarchy != null)
                return onRbHierarchy;
        }

        return other.GetComponent<Inventory>();
    }
}
