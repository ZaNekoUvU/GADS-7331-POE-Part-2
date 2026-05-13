using UnityEngine;

/// <summary>
/// Trigger pickup that grants the active forge-quest item (one unit) and notifies <see cref="ForgeQuestManager"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class QuestMineralPickup : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        var c = GetComponent<Collider2D>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
            return;

        var q = ForgeQuestManager.Instance;
        if (q == null || !q.QuestActive || q.QuestItemAsset == null)
            return;

        var inv = other.GetComponent<Inventory>();
        if (inv == null)
            return;

        var leftover = inv.TryAdd(q.QuestItemAsset, 1);
        if (leftover > 0)
            return;

        q.MarkOrePickedUp();
        Destroy(gameObject);
    }
}
