using TMPro;
using UnityEngine;

/// <summary>
/// Shows <see cref="BlacksmithMaster.PlayerGold"/> on a TMP label whenever economy updates (sell, quest roll, etc.).
/// </summary>
public class GoldDisplayUI : MonoBehaviour
{
    [SerializeField] private BlacksmithMaster blacksmith;
    [SerializeField] private TMP_Text goldLabel;
    [SerializeField] private string format = "Gold: {0}";

    private void Awake()
    {
        if (goldLabel == null)
            goldLabel = GetComponent<TMP_Text>();

        if (blacksmith == null)
            blacksmith = FindAnyObjectByType<BlacksmithMaster>();
    }

    private void OnEnable()
    {
        if (blacksmith != null)
            blacksmith.OnEconomyChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (blacksmith != null)
            blacksmith.OnEconomyChanged -= Refresh;
    }

    private void Refresh()
    {
        if (goldLabel == null)
            return;

        if (blacksmith == null)
            blacksmith = FindAnyObjectByType<BlacksmithMaster>();

        if (blacksmith == null)
        {
            goldLabel.text = string.Format(format, 0);
            return;
        }

        goldLabel.text = string.Format(format, blacksmith.PlayerGold);
    }
}
