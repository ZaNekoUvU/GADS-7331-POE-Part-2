using UnityEngine;

/// <summary>
/// Unique NPC identity + persona for local LLM prompts. Create via Assets → Create → Back To The Forge → NPC Dialogue Profile.
/// </summary>
[CreateAssetMenu(fileName = "NpcDialogueProfile", menuName = "Back To The Forge/NPC Dialogue Profile")]
public class NpcDialogueProfile : ScriptableObject
{
    [Tooltip("Shown in the dialogue box header.")]
    [SerializeField] private string characterName = "Villager";

    [Tooltip("Who they are, how they speak, what they care about. Feeds the model system prompt.")]
    [SerializeField] [TextArea(4, 12)] private string personaDescription =
        "A cautious miner who worries about cave-ins and respects the forge.";

    [Tooltip("Optional: where they are, what they know about (keeps answers grounded).")]
    [SerializeField] [TextArea(2, 6)] private string localKnowledge = "";

    [Header("Fallback (no Ollama / errors)")]
    [SerializeField] [TextArea(2, 4)] private string[] fallbackLines =
    {
        "Can't chat right now — something's wrong with the voices in my head.",
        "Sorry, I've got my hands full.",
        "Come back in a bit."
    };

    public string CharacterName => characterName;
    public string PersonaDescription => personaDescription ?? string.Empty;
    public string LocalKnowledge => localKnowledge ?? string.Empty;
    public string[] FallbackLines => fallbackLines;

    public string PickRandomFallback()
    {
        if (fallbackLines == null || fallbackLines.Length == 0)
            return "...";

        for (var tries = 0; tries < fallbackLines.Length; tries++)
        {
            var line = fallbackLines[Random.Range(0, fallbackLines.Length)];
            if (!string.IsNullOrWhiteSpace(line))
                return line.Trim();
        }

        return "...";
    }
}
