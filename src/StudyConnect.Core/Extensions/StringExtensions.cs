namespace StudyConnect.Core.Extensions;

/// <summary>Extension methods for working with user-entered text.</summary>
public static class StringExtensions
{
    /// <summary>Words that should keep a specific casing when shown in title case.</summary>
    private static readonly Dictionary<string, string> SpecialCasing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ai"] = "AI",
        ["ml"] = "ML",
        ["ui"] = "UI",
        ["ux"] = "UX",
        ["ui/ux"] = "UI/UX",
        [".net"] = ".NET",
        ["c#"] = "C#",
        ["c++"] = "C++",
        ["sql"] = "SQL",
        ["api"] = "API",
        ["apis"] = "APIs",
        ["iot"] = "IoT",
        ["aws"] = "AWS",
        ["nlp"] = "NLP",
        ["devops"] = "DevOps",
        ["javascript"] = "JavaScript",
        ["typescript"] = "TypeScript",
        ["asp.net"] = "ASP.NET",
        ["maui"] = "MAUI",
        ["wpf"] = "WPF",
        ["linq"] = "LINQ"
    };

    /// <summary>
    /// Normalises a tag/interest so that "  Machine   LEARNING " and "machine learning"
    /// are treated as the same value (trimmed, single spaces, lower case).
    /// </summary>
    public static string NormalizeTag(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string[] words = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words).ToLowerInvariant();
    }

    /// <summary>Converts "machine learning" into "Machine Learning" (keeping acronyms such as AI or .NET).</summary>
    public static string ToTitleCase(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        IEnumerable<string> words = value.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => SpecialCasing.TryGetValue(word, out string? special)
                ? special
                : char.ToUpperInvariant(word[0]) + word[1..]);

        return string.Join(' ', words);
    }

    /// <summary>Returns up to two initials, e.g. "Alice Johnson" becomes "AJ".</summary>
    public static string ToInitials(this string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        string[] parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string initials = parts.Length == 1
            ? parts[0][..1]
            : $"{parts[0][0]}{parts[^1][0]}";

        return initials.ToUpperInvariant();
    }

    /// <summary>Shortens long text and appends an ellipsis.</summary>
    public static string Truncate(this string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..Math.Max(1, maxLength - 1)].TrimEnd() + "…";
    }

    /// <summary>True when the text contains <paramref name="term"/> ignoring case.</summary>
    public static bool ContainsIgnoreCase(this string? value, string? term)
    {
        if (string.IsNullOrEmpty(term))
        {
            return true;
        }

        return value?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
