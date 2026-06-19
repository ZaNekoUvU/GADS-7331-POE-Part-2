using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Keeps Ollama dialogue lines aligned with the speaker name shown in the UI header.
/// </summary>
public static class DialogueSpeakerNameUtil
{
    private static readonly Regex[] SelfIntroductionPatterns =
    {
        new(@"(?i)\b(I'?m|I am)\s+([A-Za-z][A-Za-z'-]{0,24})\b", RegexOptions.Compiled),
        new(@"(?i)\b(my name is|my name'?s|name'?s|call me|they call me|folks call me)\s+([A-Za-z][A-Za-z'-]{0,24})\b", RegexOptions.Compiled),
        new(@"(?i)\bthis is\s+([A-Za-z][A-Za-z'-]{0,24})\b", RegexOptions.Compiled)
    };

    public static string IdentityRules(string characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return string.Empty;

        var name = characterName.Trim();
        return
            $"Your name is exactly \"{name}\". Never introduce yourself by any other name. " +
            $"If you say your name aloud, it must be \"{name}\" — not a nickname, alias, or another mercenary's name.";
    }

    public static void AppendIdentityRules(StringBuilder sb, string characterName)
    {
        if (sb == null || string.IsNullOrWhiteSpace(characterName))
            return;

        sb.AppendLine();
        sb.AppendLine(IdentityRules(characterName));
    }

    /// <summary>Rewrites common self-introduction patterns that use the wrong name.</summary>
    public static string Enforce(string line, string canonicalName)
    {
        if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(canonicalName))
            return line;

        var canonical = canonicalName.Trim();
        var result = line;

        for (var i = 0; i < SelfIntroductionPatterns.Length; i++)
        {
            result = SelfIntroductionPatterns[i].Replace(result, match =>
            {
                var nameGroup = match.Groups[match.Groups.Count - 1];
                var spokenName = nameGroup.Value;
                if (NamesMatch(spokenName, canonical))
                    return match.Value;

                var prefix = match.Groups[1].Value;
                var suffixStart = nameGroup.Index - match.Index + nameGroup.Length;
                var suffix = match.Value.Substring(suffixStart);
                return prefix + " " + canonical + suffix;
            });
        }

        return result;
    }

    private static bool NamesMatch(string spoken, string canonical)
    {
        if (string.IsNullOrWhiteSpace(spoken) || string.IsNullOrWhiteSpace(canonical))
            return false;

        return string.Equals(spoken.Trim(), canonical.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}
