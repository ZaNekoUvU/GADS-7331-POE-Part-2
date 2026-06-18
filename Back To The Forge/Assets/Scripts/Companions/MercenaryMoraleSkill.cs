using System;
using UnityEngine;

/// <summary>One battle passive unlocked by companion dialogue morale.</summary>
[Serializable]
public struct MercenaryMoraleSkill
{
    [Tooltip("Shown in conversation UI and combat log.")]
    public string skillName;

    [TextArea(1, 3)]
    public string description;

    public MercenaryMoraleEffectKind effectKind;

    [Range(0.05f, 0.5f)]
    public float magnitude;
}

public enum MercenaryMoraleEffectKind
{
    None = 0,
    PartyAttackUp = 1,
    PartyAttackDown = 2,
    SelfAttackUp = 3,
    SelfAttackDown = 4,
    SelfMaxHpUp = 5,
    HeroManaRegenUp = 6
}
