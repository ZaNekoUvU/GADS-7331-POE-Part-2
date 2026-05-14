using UnityEngine;

/// <summary>
/// Cost and unit identity for <see cref="CompanionRecruiter"/>. Create one asset per hire option.
/// </summary>
[CreateAssetMenu(fileName = "HireableCompanionOffer", menuName = "Companions/Hireable Companion Offer")]
public class HireableCompanionOffer : ScriptableObject
{
    [SerializeField] private UnitDefinition unit;
    [SerializeField] private int hireCost = 50;
    [Tooltip("Shown in hire UI; empty uses unit display name.")]
    [SerializeField] private string recruitLabel;

    public UnitDefinition Unit => unit;
    public int HireCost => Mathf.Max(0, hireCost);

    public int UnitId => unit != null ? unit.UnitId : 0;

    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(recruitLabel)
            ? (unit != null ? unit.DisplayName : "Companion")
            : recruitLabel.Trim();
}
