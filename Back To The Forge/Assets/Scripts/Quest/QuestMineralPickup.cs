using UnityEngine;

/// <summary>
/// Trigger pickup that grants the active forge-quest item (one unit) and notifies <see cref="ForgeQuestManager"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class QuestMineralPickup : MonoBehaviour
{
    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive || q.QuestItemAsset == null)
            return;

        var rb = other.attachedRigidbody;
        var inv = rb != null ? rb.GetComponent<Inventory>() : other.GetComponent<Inventory>();
        if (inv == null)
            return;

        var leftover = inv.TryAdd(q.QuestItemAsset, 1);
        if (leftover > 0)
            return;

        q.MarkOrePickedUp();
        Destroy(gameObject);
    }
}
