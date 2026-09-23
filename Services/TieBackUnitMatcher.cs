using System.Text.RegularExpressions;

namespace EmpactRecipeOnline.Services;

/// <summary>
/// Keeps tie-back completion checks consistent even when a unit is represented as
/// a code (S0490), a name, or the seeded display value (S0490 - Unit Name).
/// </summary>
public static class TieBackUnitMatcher
{
    private static readonly Regex UnitCodeRegex = new(@"^\s*([A-Za-z]\d{4})\b", RegexOptions.Compiled);

    public static bool Matches(string? storedUnit, params string?[] candidates)
    {
        if (string.IsNullOrWhiteSpace(storedUnit)) return false;
        var stored = storedUnit.Trim();
        var storedCode = UnitCode(stored);
        var storedName = NameOnly(stored);

        foreach (var candidateRaw in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidateRaw)) continue;
            var candidate = candidateRaw.Trim();
            if (stored.Equals(candidate, StringComparison.OrdinalIgnoreCase)) return true;

            var candidateCode = UnitCode(candidate);
            if (!string.IsNullOrWhiteSpace(storedCode) && !string.IsNullOrWhiteSpace(candidateCode) &&
                storedCode.Equals(candidateCode, StringComparison.OrdinalIgnoreCase)) return true;

            var candidateName = NameOnly(candidate);
            if (!string.IsNullOrWhiteSpace(storedName) && !string.IsNullOrWhiteSpace(candidateName) &&
                storedName.Equals(candidateName, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static string UnitCode(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : UnitCodeRegex.Match(value).Groups[1].Value.ToUpperInvariant();

    private static string NameOnly(string value)
    {
        var text = value.Trim();
        var match = UnitCodeRegex.Match(text);
        if (!match.Success) return text;
        text = text[match.Length..].Trim();
        return text.TrimStart('-', '–', '—', ':', '|').Trim();
    }
}
