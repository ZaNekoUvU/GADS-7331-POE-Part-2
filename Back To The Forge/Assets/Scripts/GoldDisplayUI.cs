using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

/// <summary>
/// Shows <see cref="BlacksmithMaster.PlayerGold"/> in an FF-style HUD panel.
/// </summary>
public class GoldDisplayUI : MonoBehaviour
{
    [Tooltip("Optional; leave empty to use BlacksmithMaster.ResolveEconomy().")]
    [FormerlySerializedAs("blacksmith")]
    [SerializeField] private BlacksmithMaster blacksmithOverride;
    [SerializeField] private string format = "Gold: {0}";

    private UIDocument _document;
    private Label _goldLabel;
    private BlacksmithMaster _subscribed;

    private void Awake()
    {
        BuildUi();
        Refresh();
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

    private void BuildUi()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
            _document = gameObject.AddComponent<UIDocument>();

        // Above Inventory panel toggle (4500) so gold stays visible while Tab-held inventory is open.
        FfStyleMenuUi.ConfigureDocument(_document, 4550);

        var root = _document.rootVisualElement;
        root.Clear();
        root.style.flexGrow = 1f;
        root.pickingMode = PickingMode.Ignore;

        FfStyleMenuUi.BuildAnchoredHudPanel(root, "gold-panel", 12f, 12f, 140f, out _goldLabel);
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
        if (_goldLabel == null)
            return;

        RebindEconomySubscription();

        var amount = _subscribed != null ? _subscribed.PlayerGold : 0;
        _goldLabel.text = string.Format(format, amount);
    }
}
