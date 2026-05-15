using UnityEngine;

/// <summary>
/// Spawns <see cref="CompanionRecruiter"/> NPCs from a <see cref="MercenaryRosterCatalog"/> at play start.
/// </summary>
public class MercenaryCampSpawner : MonoBehaviour
{
    [SerializeField] private MercenaryRosterCatalog catalog;
    [SerializeField] private CompanionRecruiter recruiterPrefab;

    private void Start()
    {
        if (catalog == null || catalog.Recruits == null || catalog.Recruits.Length == 0)
        {
            Debug.LogWarning($"{nameof(MercenaryCampSpawner)}: No catalog assigned.", this);
            return;
        }

        var template = recruiterPrefab != null ? recruiterPrefab : catalog.RecruiterPrefab;
        if (template == null)
        {
            Debug.LogError($"{nameof(MercenaryCampSpawner)}: Assign a recruiter prefab on the catalog or spawner.", this);
            return;
        }

        DisableLegacyHirePosts();

        var recruits = catalog.Recruits;
        for (var i = 0; i < recruits.Length; i++)
        {
            var entry = recruits[i];
            if (entry.offer == null)
                continue;

            var recruiter = Instantiate(
                template,
                new Vector3(entry.worldPosition.x, entry.worldPosition.y, 0f),
                Quaternion.identity,
                transform);

            recruiter.gameObject.name = $"Mercenary — {entry.offer.NpcDisplayName}";
            recruiter.ConfigureFromOffer(entry.offer, entry.spriteTint);

            var sprite = recruiter.GetComponentInChildren<SpriteRenderer>();
            if (sprite != null)
                sprite.color = entry.spriteTint;
        }
    }

    private static void DisableLegacyHirePosts()
    {
        var legacy = GameObject.Find("Mercenary Hire Post");
        if (legacy != null)
            legacy.SetActive(false);
    }
}
