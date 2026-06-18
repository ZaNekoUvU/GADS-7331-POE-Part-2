using UnityEngine;

/// <summary>Per-mercenary mood from player dialogue; drives context-specific combat passives.</summary>
public sealed class CompanionMoraleState
{
    public int UnitId { get; }
    public float Affinity { get; private set; }
    public MercenaryMoraleSkill ActiveSkill { get; private set; }
    public string ActiveSkillLabel { get; private set; } = string.Empty;
    public CompanionSentiment LastSentiment { get; private set; }

    public CompanionMoraleState(int unitId) => UnitId = unitId;

    public bool HasActiveSkill =>
        ActiveSkill.effectKind != MercenaryMoraleEffectKind.None
        && !string.IsNullOrWhiteSpace(ActiveSkill.skillName);

    public void ApplyDialogueResult(CompanionDialogueDto dto, HireableCompanionOffer offer)
    {
        if (dto == null)
            return;

        LastSentiment = NormalizeSentiment(dto.sentiment);

        switch (LastSentiment)
        {
            case CompanionSentiment.Positive:
                Affinity = Mathf.Clamp(Affinity + 0.2f, -1f, 1f);
                ActiveSkill = offer != null ? offer.PositiveMoraleSkill : default;
                break;
            case CompanionSentiment.Negative:
                Affinity = Mathf.Clamp(Affinity - 0.25f, -1f, 1f);
                ActiveSkill = offer != null ? offer.NegativeMoraleSkill : default;
                break;
            default:
                Affinity = Mathf.Clamp(Affinity + 0.05f, -1f, 1f);
                ActiveSkill = default;
                break;
        }

        ActiveSkillLabel = !string.IsNullOrWhiteSpace(dto.effectLabel)
            ? dto.effectLabel.Trim()
            : ActiveSkill.skillName ?? string.Empty;
    }

    public string DescribeActiveSkillForUi()
    {
        if (!HasActiveSkill)
            return "Neutral — words haven't unlocked a battle skill yet.";

        var pct = Mathf.RoundToInt(ActiveSkill.magnitude * 100f);
        var target = DescribeEffectTarget(ActiveSkill.effectKind);
        return $"{ActiveSkillLabel}: {target} ({FormatSign(ActiveSkill.effectKind)}{pct}%).";
    }

    private static string DescribeEffectTarget(MercenaryMoraleEffectKind kind)
    {
        return kind switch
        {
            MercenaryMoraleEffectKind.PartyAttackUp or MercenaryMoraleEffectKind.PartyAttackDown => "party attack",
            MercenaryMoraleEffectKind.SelfAttackUp or MercenaryMoraleEffectKind.SelfAttackDown => "their strikes",
            MercenaryMoraleEffectKind.SelfMaxHpUp => "their endurance",
            MercenaryMoraleEffectKind.HeroManaRegenUp => "hero mana recovery",
            _ => "battle spirit"
        };
    }

    private static string FormatSign(MercenaryMoraleEffectKind kind)
    {
        return kind switch
        {
            MercenaryMoraleEffectKind.PartyAttackDown or MercenaryMoraleEffectKind.SelfAttackDown => "-",
            _ => "+"
        };
    }

    private static CompanionSentiment NormalizeSentiment(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return CompanionSentiment.Neutral;

        var t = raw.Trim().ToLowerInvariant();
        if (t.Contains("positive") || t.Contains("happy") || t.Contains("encourag"))
            return CompanionSentiment.Positive;
        if (t.Contains("negative") || t.Contains("angry") || t.Contains("hurt") || t.Contains("insult"))
            return CompanionSentiment.Negative;
        return CompanionSentiment.Neutral;
    }
}

public enum CompanionSentiment
{
    Neutral,
    Positive,
    Negative
}
