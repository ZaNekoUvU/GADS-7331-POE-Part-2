using System;
using UnityEngine;

/// <summary>
/// Maps unit id → prefab + <see cref="UnitDefinition"/> for spawning.
/// </summary>
[CreateAssetMenu(fileName = "UnitPrefabRegistry", menuName = "Combat/Unit Prefab Registry")]
public class UnitPrefabRegistry : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public int unitId;
        public UnitDefinition definition;
        public GameObject prefab;
    }

    [SerializeField] private Entry[] entries;

    public bool TryGet(int unitId, out UnitDefinition definition, out GameObject prefab)
    {
        if (entries != null)
        {
            foreach (var e in entries)
            {
                if (e != null && e.unitId == unitId && e.definition != null && e.prefab != null)
                {
                    definition = e.definition;
                    prefab = e.prefab;
                    return true;
                }
            }
        }

        definition = null;
        prefab = null;
        return false;
    }
}
