using System;
using UnityEngine;

/// <summary>
/// All hireable mercenaries and where <see cref="MercenaryCampSpawner"/> places them in exploration.
/// </summary>
[CreateAssetMenu(fileName = "MercenaryRosterCatalog", menuName = "Companions/Mercenary Roster Catalog")]
public class MercenaryRosterCatalog : ScriptableObject
{
    [Serializable]
    public struct SpawnEntry
    {
        public HireableCompanionOffer offer;
        public Vector2 worldPosition;
        public Color spriteTint;
    }

    [SerializeField] private CompanionRecruiter recruiterPrefab;
    [SerializeField] private SpawnEntry[] recruits =
    {
        new() { worldPosition = new Vector2(-2.5f, -6.5f), spriteTint = new Color(0.85f, 0.45f, 0.15f) },
        new() { worldPosition = new Vector2(2.5f, -7f), spriteTint = new Color(0.35f, 0.75f, 0.95f) },
        new() { worldPosition = new Vector2(6.5f, -4.5f), spriteTint = new Color(0.75f, 0.78f, 0.82f) },
        new() { worldPosition = new Vector2(11f, -2f), spriteTint = new Color(0.55f, 0.4f, 0.9f) },
        new() { worldPosition = new Vector2(-6f, -3.5f), spriteTint = new Color(0.25f, 0.3f, 0.35f) },
        new() { worldPosition = new Vector2(0f, -9.5f), spriteTint = new Color(0.9f, 0.7f, 0.35f) },
        new() { worldPosition = new Vector2(8f, 1.5f), spriteTint = new Color(0.6f, 0.2f, 0.55f) }
    };

    public CompanionRecruiter RecruiterPrefab => recruiterPrefab;
    public SpawnEntry[] Recruits => recruits;
}
