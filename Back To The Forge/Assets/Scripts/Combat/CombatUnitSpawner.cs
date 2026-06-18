using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads <see cref="CombatSession"/> + encounter catalog and spawns unit prefabs at anchor transforms.
/// Disables placeholder Ally/Enemy objects in the scene if present by name.
/// </summary>
[DefaultExecutionOrder(-50)]
public class CombatUnitSpawner : MonoBehaviour
{
    [SerializeField] private UnitPrefabRegistry unitPrefabRegistry;
    [SerializeField] private EncounterCatalog encounterCatalog;
    [SerializeField] private MoveRegistry moveRegistry;
    [Tooltip("Optional: assign 4 ally anchors (left to right). If empty, tries names Ally, Ally (1), Ally (2), Ally (3).")]
    [SerializeField] private Transform[] allyAnchors = new Transform[4];
    [Tooltip("Optional: assign 3 enemy anchors. If empty, tries Enemy, Enemy (1), Enemy (2).")]
    [SerializeField] private Transform[] enemyAnchors = new Transform[3];

    private readonly List<CombatUnit> _allies = new();
    private readonly List<CombatUnit> _enemies = new();
    private int _spawnedEnemyCount;

    public IReadOnlyList<CombatUnit> SpawnedAllies => _allies;
    public IReadOnlyList<CombatUnit> SpawnedEnemies => _enemies;
    public MoveRegistry MoveRegistry => moveRegistry;

    private void OnEnable()
    {
        CombatUnit.OnDefeated += HandleUnitDefeated;
    }

    private void OnDisable()
    {
        CombatUnit.OnDefeated -= HandleUnitDefeated;
    }

    private void HandleUnitDefeated(CombatUnit unit)
    {
        if (unit == null)
            return;

        _allies.Remove(unit);
        _enemies.Remove(unit);
    }

    /// <summary>True when this encounter had enemies and none are alive (list may be empty after defeats).</summary>
    public bool AreAllEnemiesDefeated()
    {
        if (_spawnedEnemyCount == 0)
            return false;

        foreach (var e in _enemies)
        {
            if (e != null && e.IsAlive)
                return false;
        }

        return true;
    }

    private void Start()
    {
        FillAnchorsIfNeeded();
        HidePlaceholderSpritesOnly();
        SpawnAll();
    }

    /// <summary>Keeps transforms active so anchors stay valid; hides old placeholder art only.</summary>
    private void HidePlaceholderSpritesOnly()
    {
        foreach (var n in new[] { "Ally", "Ally (1)", "Ally (2)", "Ally (3)", "Enemy", "Enemy (1)", "Enemy (2)" })
        {
            var go = FindInOwnScene(n);
            if (go == null)
                continue;

            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                sr.enabled = false;
        }
    }

    private void FillAnchorsIfNeeded()
    {
        if (allyAnchors == null)
            allyAnchors = new Transform[4];
        else if (allyAnchors.Length < 4)
            System.Array.Resize(ref allyAnchors, 4);

        if (enemyAnchors == null)
            enemyAnchors = new Transform[3];
        else if (enemyAnchors.Length < 3)
            System.Array.Resize(ref enemyAnchors, 3);

        for (var i = 0; i < 4; i++)
        {
            if (allyAnchors[i] == null)
                allyAnchors[i] = FindAnchor($"Ally{(i == 0 ? "" : $" ({i})")}");
            if (i < 3 && enemyAnchors[i] == null)
                enemyAnchors[i] = FindAnchor($"Enemy{(i == 0 ? "" : $" ({i})")}");
        }
    }

    /// <summary>
    /// Resolves anchors in this object's scene first. <see cref="GameObject.Find"/> only searches the
    /// active scene, so additive combat loads would miss "Ally"/"Enemy" roots unless assigned in the inspector.
    /// </summary>
    private Transform FindAnchor(string exactName)
    {
        var go = FindInOwnScene(exactName);
        return go != null ? go.transform : null;
    }

    private GameObject FindInOwnScene(string exactName)
    {
        var scene = gameObject.scene;
        if (scene.IsValid())
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == exactName)
                    return root;
            }
        }

        return GameObject.Find(exactName);
    }

    private void SpawnAll()
    {
        _allies.Clear();
        _enemies.Clear();
        _spawnedEnemyCount = 0;

        if (unitPrefabRegistry == null || encounterCatalog == null)
        {
            Debug.LogError($"{nameof(CombatUnitSpawner)}: Assign {nameof(unitPrefabRegistry)} and {nameof(encounterCatalog)}.", this);
            return;
        }

        var allyIds = CombatSession.GetAllyUnitIds();

        for (var i = 0; i < 4 && i < allyIds.Length; i++)
        {
            var id = allyIds[i];
            if (id <= 0)
                continue;

            var anchor = allyAnchors[i];
            if (anchor == null)
            {
                Debug.LogWarning($"{nameof(CombatUnitSpawner)}: Missing ally anchor slot {i}.");
                continue;
            }

            if (!unitPrefabRegistry.TryGet(id, out var def, out var prefab))
            {
                Debug.LogError($"{nameof(CombatUnitSpawner)}: No registry entry for ally unit id {id}.", this);
                continue;
            }

            var isPlayer = i == 0;
            var unit = SpawnUnit(prefab, anchor, def, moveRegistry, true, isPlayer, i);
            if (unit != null)
                _allies.Add(unit);
        }

        if (!encounterCatalog.TryGet(CombatSession.EncounterId, out var encounter))
        {
            Debug.LogError($"{nameof(CombatUnitSpawner)}: No encounter with id {CombatSession.EncounterId}.", this);
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            var id = encounter.GetEnemyUnitId(i);
            if (id <= 0)
                continue;

            var anchor = enemyAnchors[i];
            if (anchor == null)
            {
                Debug.LogWarning($"{nameof(CombatUnitSpawner)}: Missing enemy anchor slot {i}.");
                continue;
            }

            if (!unitPrefabRegistry.TryGet(id, out var def, out var prefab))
            {
                Debug.LogError($"{nameof(CombatUnitSpawner)}: No registry entry for enemy unit id {id}.", this);
                continue;
            }

            var unit = SpawnUnit(prefab, anchor, def, moveRegistry, false, false, i);
            if (unit != null)
                _enemies.Add(unit);
        }

        _spawnedEnemyCount = _enemies.Count;
    }

    private CombatUnit SpawnUnit(
        GameObject prefab,
        Transform anchor,
        UnitDefinition def,
        MoveRegistry registry,
        bool ally,
        bool isPlayer,
        int slot)
    {
        var instance = Instantiate(prefab, anchor.position, Quaternion.identity, anchor);
        var cu = instance.GetComponent<CombatUnit>();
        if (cu == null)
            cu = instance.AddComponent<CombatUnit>();

        int? startHp = null;
        if (isPlayer)
        {
            var persist = FindAnyObjectByType<PlayerPersistentCombatHealth>();
            if (persist != null && def != null)
                startHp = persist.GetHpForCombatStart(def.MaxHp);
        }

        cu.Initialize(def, registry, ally, isPlayer, slot, startHp);

        if (ally)
        {
            cu.SetAttackDamageMultiplier(CombatSession.PartyAttackMultiplier);

            if (slot >= 1 && slot <= 3)
            {
                var moraleSlots = CombatSession.CompanionMoraleBySlot;
                var moraleIndex = slot - 1;
                if (moraleSlots != null && moraleIndex >= 0 && moraleIndex < moraleSlots.Length)
                {
                    var morale = moraleSlots[moraleIndex];
                    if (def != null && morale.UnitId == def.UnitId)
                    {
                        var selfAtk = morale.SelfAttackMultiplier > 0f ? morale.SelfAttackMultiplier : 1f;
                        cu.SetAttackDamageMultiplier(selfAtk * CombatSession.PartyAttackMultiplier);
                        var maxHpMul = morale.SelfMaxHpMultiplier > 1.001f ? morale.SelfMaxHpMultiplier : 1f;
                        cu.ApplyMoraleModifiers(1f, maxHpMul);
                    }
                }
            }
        }

        return cu;
    }
}
