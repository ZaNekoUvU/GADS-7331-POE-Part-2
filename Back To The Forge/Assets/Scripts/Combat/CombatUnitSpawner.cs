using System;
using System.Collections.Generic;

using UnityEngine;



/// <summary>

/// Reads <see cref="CombatSession"/> + encounter catalog and spawns unit prefabs at anchor transforms.

/// Disables placeholder Ally/Enemy objects in the scene if present by name.

/// </summary>

[DefaultExecutionOrder(-50)]

public class CombatUnitSpawner : MonoBehaviour

{

    public const int MaxEnemySlots = 4;



    [SerializeField] private UnitPrefabRegistry unitPrefabRegistry;

    [SerializeField] private EncounterCatalog encounterCatalog;

    [SerializeField] private WildEnemyCatalog wildEnemyCatalog;

    [SerializeField] private MercenaryRosterCatalog mercenaryCatalog;

    [SerializeField] private MoveRegistry moveRegistry;

    [Tooltip("Optional: assign 4 ally anchors (left to right). If empty, tries names Ally, Ally (1), Ally (2), Ally (3).")]

    [SerializeField] private Transform[] allyAnchors = new Transform[4];

    [Tooltip("Optional: assign up to 4 enemy anchors. If empty, tries Enemy … Enemy (3).")]

    [SerializeField] private Transform[] enemyAnchors = new Transform[MaxEnemySlots];

    [Header("Enemy visuals (fallback when no rolled wild encounter)")]

    [Tooltip("Battle-ready sprites per enemy anchor slot when not using a rolled wild encounter.")]

    [SerializeField] private Sprite[] enemyBattleSpritesBySlot = new Sprite[MaxEnemySlots];



    private readonly List<CombatUnit> _allies = new();

    private readonly List<CombatUnit> _enemies = new();

    private int _spawnedEnemyCount;

    private Transform _syntheticFourthEnemyAnchor;



    public IReadOnlyList<CombatUnit> SpawnedAllies => _allies;

    public IReadOnlyList<CombatUnit> SpawnedEnemies => _enemies;

    public MoveRegistry MoveRegistry => moveRegistry;

    /// <summary>Fired after allies and enemies are spawned for the current fight.</summary>
    public static event Action<IReadOnlyList<CombatUnit>> EnemiesSpawned;

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

        if (mercenaryCatalog != null)

            MercenaryOfferLookup.RegisterCatalog(mercenaryCatalog);



        FillAnchorsIfNeeded();

        HidePlaceholderSpritesOnly();

        SpawnAll();

    }



    /// <summary>Keeps transforms active so anchors stay valid; hides old placeholder art only.</summary>

    private void HidePlaceholderSpritesOnly()

    {

        foreach (var n in new[]

                 {

                     "Ally", "Ally (1)", "Ally (2)", "Ally (3)",

                     "Enemy", "Enemy (1)", "Enemy (2)", "Enemy (3)"

                 })

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

            enemyAnchors = new Transform[MaxEnemySlots];

        else if (enemyAnchors.Length < MaxEnemySlots)

            System.Array.Resize(ref enemyAnchors, MaxEnemySlots);



        for (var i = 0; i < 4; i++)

        {

            if (allyAnchors[i] == null)

                allyAnchors[i] = FindAnchor($"Ally{(i == 0 ? "" : $" ({i})")}");

        }



        for (var i = 0; i < MaxEnemySlots; i++)

        {

            if (enemyAnchors[i] == null)

                enemyAnchors[i] = FindAnchor($"Enemy{(i == 0 ? "" : $" ({i})")}");

        }



        EnsureFourthEnemyAnchor();

    }



    private void EnsureFourthEnemyAnchor()

    {

        if (enemyAnchors[3] != null)

            return;



        var a1 = enemyAnchors[1];

        var a2 = enemyAnchors[2];

        if (a1 == null || a2 == null)

            return;



        var go = new GameObject("Enemy (3)");

        go.transform.SetParent(transform);

        var pos = (a1.position + a2.position) * 0.5f;

        pos.y = Mathf.Min(a1.position.y, a2.position.y) - 0.15f;

        go.transform.position = pos;

        _syntheticFourthEnemyAnchor = go.transform;

        enemyAnchors[3] = _syntheticFourthEnemyAnchor;

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



        if (unitPrefabRegistry == null)

        {

            Debug.LogError($"{nameof(CombatUnitSpawner)}: Assign {nameof(unitPrefabRegistry)}.", this);

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

            var unit = SpawnUnit(prefab, anchor, def, moveRegistry, true, isPlayer, i, null);

            if (unit != null)

                _allies.Add(unit);

        }



        if (CombatSession.HasRolledWildEncounter)

            SpawnRolledWildEnemies();

        else

            SpawnCatalogEncounterEnemies();



        _spawnedEnemyCount = _enemies.Count;

        EnemiesSpawned?.Invoke(_enemies);

    }



    private void SpawnRolledWildEnemies()

    {

        var rolled = CombatSession.ActiveWildEncounter;

        if (rolled == null)

            return;



        for (var i = 0; i < rolled.Count && i < MaxEnemySlots; i++)

        {

            var id = rolled.GetUnitId(i);

            if (id <= 0)

                continue;



            SpawnEnemyAtSlot(i, id, rolled.GetBattleSprite(i));

        }

    }



    private void SpawnCatalogEncounterEnemies()

    {

        if (encounterCatalog == null)

        {

            Debug.LogError($"{nameof(CombatUnitSpawner)}: Assign {nameof(encounterCatalog)}.", this);

            return;

        }



        if (!encounterCatalog.TryGet(CombatSession.EncounterId, out var encounter))

        {

            Debug.LogError($"{nameof(CombatUnitSpawner)}: No encounter with id {CombatSession.EncounterId}.", this);

            return;

        }



        for (var i = 0; i < MaxEnemySlots; i++)

        {

            var id = encounter.GetEnemyUnitId(i);

            if (id <= 0)

                continue;



            Sprite sprite = null;

            if (enemyBattleSpritesBySlot != null && i < enemyBattleSpritesBySlot.Length)

                sprite = enemyBattleSpritesBySlot[i];



            SpawnEnemyAtSlot(i, id, sprite);

        }

    }



    private void SpawnEnemyAtSlot(int slot, int unitId, Sprite battleSprite)

    {

        var anchor = enemyAnchors[slot];

        if (anchor == null)

        {

            Debug.LogWarning($"{nameof(CombatUnitSpawner)}: Missing enemy anchor slot {slot}.");

            return;

        }



        if (!unitPrefabRegistry.TryGet(unitId, out var def, out var prefab))

        {

            Debug.LogError($"{nameof(CombatUnitSpawner)}: No registry entry for enemy unit id {unitId}.", this);

            return;

        }



        if (battleSprite == null && wildEnemyCatalog != null)

            wildEnemyCatalog.TryGetBattleSprite(unitId, out battleSprite);



        if (battleSprite == null && enemyBattleSpritesBySlot != null && slot < enemyBattleSpritesBySlot.Length)

            battleSprite = enemyBattleSpritesBySlot[slot];



        var unit = SpawnUnit(prefab, anchor, def, moveRegistry, false, false, slot, battleSprite);

        if (unit != null)

            _enemies.Add(unit);

    }



    private CombatUnit SpawnUnit(

        GameObject prefab,

        Transform anchor,

        UnitDefinition def,

        MoveRegistry registry,

        bool ally,

        bool isPlayer,

        int slot,

        Sprite enemyBattleSprite)

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
            if (isPlayer && def != null && def.BattleSprite != null)
                MercenaryVisualApplier.ApplyAllyCombatVisual(cu, def.BattleSprite, faceRight: true);
            else if (def != null && MercenaryOfferLookup.TryGet(def.UnitId, out var offer))
                MercenaryVisualApplier.ApplyCombatVisual(cu, offer);
        }
        else if (!ally && enemyBattleSprite != null)

            MercenaryVisualApplier.ApplyEnemyCombatVisual(cu, enemyBattleSprite);



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


