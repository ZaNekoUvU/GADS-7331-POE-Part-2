using System.Collections.Generic;

/// <summary>
/// Keyboard reference shown in the pause menu Controls screen.
/// Keep in sync with Input System bindings and script fallbacks.
/// </summary>
public static class GameControlsReference
{
    public readonly struct Entry
    {
        public readonly string Keys;
        public readonly string Description;

        public Entry(string keys, string description)
        {
            Keys = keys;
            Description = description;
        }
    }

    private static readonly Entry[] All =
    {
        new("W A S D / Arrows", "Move in exploration"),
        new("E", "Interact — talk, advance dialogue, hold to mine"),
        new("C", "Talk to a hired mercenary (pick from party list)"),
        new("Tab (hold)", "Inventory and sell prices"),
        new("Enter / Z", "Confirm menu or dialogue choice"),
        new("Esc", "Pause / unpause"),
        new("W / S or ↑ / ↓", "Highlight menu options"),
        new("— Combat —", ""),
        new("W / S or ↑ / ↓", "Select attack / skill / item"),
        new("Enter / Z", "Confirm command or target"),
        new("Esc / X", "Cancel target selection")
    };

    public static IReadOnlyList<Entry> Entries => All;
}
