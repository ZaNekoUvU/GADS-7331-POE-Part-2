using System.Collections.Generic;

/// <summary>
/// Copy shown on the exploration tutorial screen at game start.
/// </summary>
public static class TutorialIntroReference
{
    public static readonly IReadOnlyList<string> Paragraphs = new[]
    {
        "Explore the village and wilds. Mine ore, gather materials, and sell them for gold.",
        "Visit the blacksmith and press E to take forge commissions. The blue arrow points the way.",
        "Hold Tab to open your inventory, sell prices, and current objective.",
        "Hold E at iron veins, trees, stone, coal, gold, and emerald deposits to gather resources. Nodes run dry until the day resets.",
        "Hire mercenaries at the camp. Press C on the road to talk and boost their battle spirit.",
        "Stepping on bright risky ground can trigger bandit fights. Use Flee if you need to escape.",
        "Press Esc to pause. Check Controls there anytime."
    };

    public const string BeginLabel = "Begin adventure";
}
