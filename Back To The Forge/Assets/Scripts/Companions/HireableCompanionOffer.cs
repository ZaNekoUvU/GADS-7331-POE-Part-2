using UnityEngine;

/// <summary>
/// Cost, combat unit, and dialogue for <see cref="CompanionRecruiter"/>.
/// </summary>
[CreateAssetMenu(fileName = "HireableCompanionOffer", menuName = "Companions/Hireable Companion Offer")]
public class HireableCompanionOffer : ScriptableObject
{
    [SerializeField] private UnitDefinition unit;
    [SerializeField] private int hireCost = 50;
    [Tooltip("Shown in hire UI; empty uses unit display name.")]
    [SerializeField] private string recruitLabel;

    [Header("Personality & dialogue")]
    [Tooltip("Short trait shown in logs / optional UI.")]
    [SerializeField] private string personalityTrait;
    [SerializeField] private string npcDisplayName;
    [TextArea(2, 5)] [SerializeField] private string openingLine;
    [TextArea(2, 3)] [SerializeField] private string cannotAffordLine =
        "Your purse is too light. Come back when you can pay.";
    [TextArea(2, 3)] [SerializeField] private string companionJoinLine =
        "I'm with you. Point me at the trouble.";
    [TextArea(2, 3)] [SerializeField] private string partyFullLine =
        "You've already got three blades with you. Dismiss one before hiring another.";

    public UnitDefinition Unit => unit;
    public int HireCost => Mathf.Max(0, hireCost);
    public int UnitId => unit != null ? unit.UnitId : 0;
    public string PersonalityTrait => personalityTrait;

    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(recruitLabel)
            ? (unit != null ? unit.DisplayName : "Companion")
            : recruitLabel.Trim();

    public string NpcDisplayName =>
        string.IsNullOrWhiteSpace(npcDisplayName) ? DisplayLabel : npcDisplayName.Trim();

    public string OpeningLine => openingLine;
    public string CannotAffordLine => cannotAffordLine;
    public string CompanionJoinLine => companionJoinLine;
    public string PartyFullLine => partyFullLine;
}
