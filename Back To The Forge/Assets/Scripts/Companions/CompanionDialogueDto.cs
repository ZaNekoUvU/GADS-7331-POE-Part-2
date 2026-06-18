using System;

/// <summary>Strict JSON from Ollama when the player talks to a hired mercenary.</summary>
[Serializable]
public class CompanionDialogueDto
{
    public string replyLine;
    /// <summary>positive, neutral, or negative — how the traveler’s words landed given this merc’s persona.</summary>
    public string sentiment;
    /// <summary>party_attack_up, party_attack_down, or none.</summary>
    public string combatEffect;
    /// <summary>Short name for UI, e.g. "Rallying Words" or "Crushed Spirit".</summary>
    public string effectLabel;
}
