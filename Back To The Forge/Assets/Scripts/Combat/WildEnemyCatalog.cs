using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wild enemy types from <c>Assets/Sprites/Enemies</c>: stats, battle sprite, and Ollama flavor.
/// </summary>
[CreateAssetMenu(fileName = "WildEnemyCatalog", menuName = "Combat/Wild Enemy Catalog")]
public class WildEnemyCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Internal key, e.g. Goblin.")]
        public string typeId = "Goblin";

        public UnitDefinition unit;
        public Sprite battleSprite;

        [TextArea(2, 4)]
        public string encounterFlavor =
            "Small raiders who relish ambushes and stolen coin.";

        public string DisplayName =>
            unit != null ? unit.DisplayName : (string.IsNullOrWhiteSpace(typeId) ? "Enemy" : typeId.Trim());

        public string TypeId => typeId;
        public UnitDefinition Unit => unit;
        public Sprite BattleSprite => battleSprite;
        public string EncounterFlavor => encounterFlavor;
    }

    [SerializeField] private Entry[] enemyTypes;

    [Header("Random encounter roll")]
    [SerializeField] private int minEnemies = 1;
    [SerializeField] private int maxEnemies = 4;

    [Range(0f, 1f)]
    [Tooltip("Chance all enemies share one type. Otherwise each slot rolls independently (mixes allowed).")]
    [SerializeField] private float singleTypeChance = 0.45f;

    public IReadOnlyList<Entry> EnemyTypes => enemyTypes;

    public RolledWildEncounter RollEncounter()
    {
        var pool = BuildValidPool();
        if (pool.Count == 0)
        {
            Debug.LogError($"{nameof(WildEnemyCatalog)} '{name}': No valid enemy types configured.", this);
            return null;
        }

        var lo = Mathf.Max(1, minEnemies);
        var hi = Mathf.Max(lo, maxEnemies);
        var count = Random.Range(lo, hi + 1);

        var picks = new List<Entry>(count);
        var useSingleType = count == 1 || Random.value < singleTypeChance;
        if (useSingleType)
        {
            var type = pool[Random.Range(0, pool.Count)];
            for (var i = 0; i < count; i++)
                picks.Add(type);
        }
        else
        {
            for (var i = 0; i < count; i++)
                picks.Add(pool[Random.Range(0, pool.Count)]);
        }

        var slots = new List<RolledWildEncounter.Slot>(picks.Count);
        for (var i = 0; i < picks.Count; i++)
        {
            var entry = picks[i];
            if (entry?.unit == null)
                continue;

            slots.Add(new RolledWildEncounter.Slot
            {
                UnitId = entry.unit.UnitId,
                DisplayName = entry.DisplayName,
                BattleSprite = entry.battleSprite,
                FlavorHint = entry.encounterFlavor
            });
        }

        return RolledWildEncounter.Create(slots);
    }

    public bool TryGetBattleSprite(int unitId, out Sprite sprite)
    {
        sprite = null;
        if (unitId <= 0 || enemyTypes == null)
            return false;

        for (var i = 0; i < enemyTypes.Length; i++)
        {
            var entry = enemyTypes[i];
            if (entry?.unit == null || entry.unit.UnitId != unitId)
                continue;

            sprite = entry.battleSprite;
            return sprite != null;
        }

        return false;
    }

    private List<Entry> BuildValidPool()
    {
        var pool = new List<Entry>();
        if (enemyTypes == null)
            return pool;

        for (var i = 0; i < enemyTypes.Length; i++)
        {
            var entry = enemyTypes[i];
            if (entry?.unit == null || entry.unit.UnitId <= 0)
                continue;

            pool.Add(entry);
        }

        return pool;
    }
}
