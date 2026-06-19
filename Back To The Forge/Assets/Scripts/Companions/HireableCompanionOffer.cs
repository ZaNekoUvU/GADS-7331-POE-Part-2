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

    [Tooltip("Optional fuller voice for the LLM. Empty uses Personality Trait.")]
    [SerializeField] [TextArea(3, 8)] private string personalityVoice;
    [SerializeField] private string npcDisplayName;
    [TextArea(2, 5)] [SerializeField] private string openingLine;
    [TextArea(2, 3)] [SerializeField] private string cannotAffordLine =
        "Your purse is too light. Come back when you can pay.";
    [TextArea(2, 3)] [SerializeField] private string companionJoinLine =
        "I'm with you. Point me at the trouble.";
    [TextArea(2, 3)] [SerializeField] private string partyFullLine =
        "You've already got three blades with you. Dismiss one before hiring another.";

    [Header("Morale battle skills (from dialogue)")]
    [SerializeField] private MercenaryMoraleSkill positiveMoraleSkill;
    [SerializeField] private MercenaryMoraleSkill negativeMoraleSkill;

    [Header("Visuals")]
    [Tooltip("Walk sheet: columns = Right, Down, Left. Rows = walk frames (top to bottom). Ignored when Walk Animator is set.")]
    [SerializeField] private Texture2D walkSpritesheet;
    [SerializeField] private Sprite battleReadySprite;
    [Tooltip("Optional sliced-sheet walk clips (Down, Right, Left, Up). Preferred when assigned.")]
    [SerializeField] private RuntimeAnimatorController walkAnimatorController;
    [SerializeField] private int walkSheetColumns = 3;
    [SerializeField] private int walkSheetRows = 4;
    [SerializeField] private float spritePixelsPerUnit = 100f;

    public UnitDefinition Unit => unit;
    public Texture2D WalkSpritesheet => walkSpritesheet;
    public Sprite BattleReadySprite => battleReadySprite;
    public RuntimeAnimatorController WalkAnimatorController => walkAnimatorController;
    public int WalkSheetColumns => Mathf.Max(1, walkSheetColumns);
    public int WalkSheetRows => Mathf.Max(1, walkSheetRows);
    public float SpritePixelsPerUnit => spritePixelsPerUnit > 0f ? spritePixelsPerUnit : 100f;
    public bool HasWalkVisuals => walkAnimatorController != null || walkSpritesheet != null;
    public bool HasCombatVisual => battleReadySprite != null;
    public int HireCost => Mathf.Max(0, hireCost);
    public int UnitId => unit != null ? unit.UnitId : 0;
    public string PersonalityTrait => personalityTrait;

    /// <summary>Feeds Ollama: designer-authored persona when set; otherwise <see cref="personalityTrait"/>.</summary>
    public string PersonaForLlm
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(personalityVoice))
                return personalityVoice.Trim();
            if (!string.IsNullOrWhiteSpace(personalityTrait))
                return personalityTrait.Trim();

            return "A sellsword at a roadside hiring pitch — practical about coin and danger.";
        }
    }

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
    public MercenaryMoraleSkill PositiveMoraleSkill => positiveMoraleSkill;
    public MercenaryMoraleSkill NegativeMoraleSkill => negativeMoraleSkill;
}
