using System;

/// <summary>Serializable payloads exchanged with the hosted AI gateway.</summary>
[Serializable]
public class AiNpcLineRequestDto
{
    public string characterName;
    public string personaDescription;
    public string localKnowledge;
}

[Serializable]
public class ForgeQuestOfferRequestDto
{
    public string blacksmithName;
    public string personaSummary;
}

[Serializable]
public class BlacksmithRoleplayRequestDto
{
    public string mode;
    public string blacksmithName;
    public string personaDescription;
    public string localKnowledge;
    public string questMaterialName;
    public int questMineralUnits;
    public int ironUnits;
    public int goldPaid;
}

public static class BlacksmithRoleplayModes
{
    public const string SmallTalk = "smallTalk";
    public const string TurnIn = "turnIn";
}

[Serializable]
public class AiTextResponseDto
{
    public string text;
}

[Serializable]
public class AiErrorResponseDto
{
    public string error;
}
