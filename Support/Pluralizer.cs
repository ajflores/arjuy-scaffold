using System;
using System.Collections.Generic;

namespace ArjuyScaffold.Support;

/// <summary>
/// English pluralization for PascalCase entity names (used for default table names). Only the
/// LAST word of the name is inflected: 'OrderCategory' -> 'OrderCategories'. Irregular nouns are
/// matched case-insensitively against the end of the name.
/// </summary>
public static class Pluralizer
{
    private static readonly Dictionary<string, string> Irregular = new(StringComparer.OrdinalIgnoreCase)
    {
        ["person"] = "people",
        ["man"] = "men",
        ["woman"] = "women",
        ["child"] = "children",
        ["foot"] = "feet",
        ["tooth"] = "teeth",
        ["mouse"] = "mice",
        ["goose"] = "geese",
        ["ox"] = "oxen",
        ["criterion"] = "criteria",
        ["datum"] = "data",
        ["analysis"] = "analyses",
        ["index"] = "indices",
        ["matrix"] = "matrices",
        ["leaf"] = "leaves",
        ["life"] = "lives",
        ["knife"] = "knives",
    };

    private static readonly HashSet<string> Uncountable = new(StringComparer.OrdinalIgnoreCase)
    {
        "sheep", "fish", "series", "species", "news", "equipment", "information", "data", "staff",
    };

    public static string Pluralize(string singular)
    {
        if (string.IsNullOrWhiteSpace(singular))
        {
            return singular;
        }

        int wordStart = LastWordStart(singular);
        string prefix = singular.Substring(0, wordStart);
        string word = singular.Substring(wordStart);

        return prefix + PluralizeWord(word);
    }

    private static string PluralizeWord(string word)
    {
        if (Uncountable.Contains(word))
        {
            return word;
        }

        if (Irregular.TryGetValue(word, out string? irregularPlural))
        {
            return MatchCase(word, irregularPlural);
        }

        string lower = word.ToLowerInvariant();

        if (lower.EndsWith("y", StringComparison.Ordinal) && word.Length > 1 && !IsVowel(lower[^2]))
        {
            return word.Substring(0, word.Length - 1) + "ies";
        }

        if (lower.EndsWith("s", StringComparison.Ordinal)
            || lower.EndsWith("x", StringComparison.Ordinal)
            || lower.EndsWith("z", StringComparison.Ordinal)
            || lower.EndsWith("ch", StringComparison.Ordinal)
            || lower.EndsWith("sh", StringComparison.Ordinal))
        {
            return word + "es";
        }

        return word + "s";
    }

    /// <summary>Index where the last PascalCase word of 'value' starts ('OrderCategory' -> 5).</summary>
    private static int LastWordStart(string value)
    {
        for (int index = value.Length - 1; index > 0; index--)
        {
            if (char.IsUpper(value[index]))
            {
                return index;
            }
        }

        return 0;
    }

    private static bool IsVowel(char letter)
    {
        return "aeiou".IndexOf(letter) >= 0;
    }

    private static string MatchCase(string original, string replacement)
    {
        if (original.Length > 0 && char.IsUpper(original[0]))
        {
            return char.ToUpperInvariant(replacement[0]) + replacement.Substring(1);
        }

        return replacement;
    }
}
