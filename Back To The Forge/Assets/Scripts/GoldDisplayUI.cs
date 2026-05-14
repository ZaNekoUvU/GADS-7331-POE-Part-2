using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Shows <see cref="BlacksmithMaster.PlayerGold"/> on a TMP label whenever economy updates (sell, quest roll, etc.).
/// </summary>
public class GoldDisplayUI : MonoBehaviour
{
    [Tooltip("Optional; leave empty to use BlacksmithMaster.ResolveEconomy() (same ledger as BlacksmithQuestGiver).")]
    [FormerlySerializedAs("blacksmith")]
    [SerializeField] private BlacksmithMaster blacksmithOverride;
    [SerializeField] private TMP_Text goldLabel;
    [SerializeField] private string format = "Gold: {0}";

    private BlacksmithMaster _subscribed;

    private void Awake()
    {
        if (goldLabel == null)
            goldLabel = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        RebindEconomySubscription();
        Refresh();
    }

    private void OnDisable()
    {
        if (_subscribed != null)
            _subscribed.OnEconomyChanged -= Refresh;
        _subscribed = null;
    }

    private void RebindEconomySubscription()
    {
        var target = blacksmithOverride != null ? blacksmithOverride : BlacksmithMaster.ResolveEconomy();
        if (_subscribed == target)
            return;

        if (_subscribed != null)
            _subscribed.OnEconomyChanged -= Refresh;

        _subscribed = target;
        if (_subscribed != null)
            _subscribed.OnEconomyChanged += Refresh;
    }

    private void Refresh()
    {
        if (goldLabel == null)
            return;

        RebindEconomySubscription();

        if (_subscribed == null)
        {
            goldLabel.text = string.Format(format, 0);
            return;
        }

        goldLabel.text = string.Format(format, _subscribed.PlayerGold);
    }
}
