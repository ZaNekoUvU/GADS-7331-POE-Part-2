using UnityEngine;

/// <summary>
/// Increases enemy combat stats based on exploration day (<see cref="BlacksmithMaster.CurrentDay"/>), which advances
/// only when the player ends the day through <see cref="BlacksmithQuestGiver"/> dialogue (when <see cref="BlacksmithMaster.SellAllAndEndDay"/> runs).
/// Add to your exploration Game Manager (or any active scene object); tune growth in the inspector.
/// </summary>
public class CombatEnemyDifficulty : MonoBehaviour
{
    private static CombatEnemyDifficulty _instance;

    [Tooltip("Per day after day 1, add this fraction to enemy HP and strike damage (e.g. 0.12 = +12% per day). Day 1 = 1×, day 2 = 1.12×, day 3 = 1.24×.")]
    [SerializeField] [Range(0f, 1f)] private float statBonusPerDayAfterFirst = 0.12f;

    [Tooltip("When no BlacksmithMaster is loaded, use this day index (minimum 1).")]
    [SerializeField] private int fallbackDay = 1;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"{nameof(CombatEnemyDifficulty)}: multiple instances — keeping '{_instance.name}', ignoring '{name}'.", this);
            return;
        }

        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>Multiplier applied to enemy max HP and basic strike damage (minimum 1).</summary>
    public static float GetEnemyStatMultiplier()
    {
        var day = ResolveDayIndex();
        var bonus = _instance != null ? _instance.statBonusPerDayAfterFirst : 0.12f;
        return Mathf.Max(1f, 1f + (day - 1) * bonus);
    }

    private static int ResolveDayIndex()
    {
        var blacksmith = BlacksmithMaster.ResolveEconomy();
        if (blacksmith != null)
            return Mathf.Max(1, blacksmith.CurrentDay);

        if (_instance != null)
            return Mathf.Max(1, _instance.fallbackDay);

        return 1;
    }
}
