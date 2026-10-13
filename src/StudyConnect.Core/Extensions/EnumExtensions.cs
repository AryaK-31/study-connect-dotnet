using System.ComponentModel;
using System.Reflection;
using System.Text.RegularExpressions;

namespace StudyConnect.Core.Extensions;

/// <summary>Extension methods that turn enum values into friendly labels.</summary>
public static class EnumExtensions
{
    private static readonly Regex PascalCaseBoundary = new("(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

    /// <summary>
    /// Returns the <see cref="DescriptionAttribute"/> text of an enum value, or splits the
    /// PascalCase name ("HighDistinction" becomes "High Distinction") when no description exists.
    /// </summary>
    public static string ToDisplayName(this Enum value)
    {
        string name = value.ToString();
        FieldInfo? field = value.GetType().GetField(name);
        DescriptionAttribute? description = field?.GetCustomAttribute<DescriptionAttribute>();

        return description?.Description ?? PascalCaseBoundary.Replace(name, " ");
    }

    /// <summary>Short three-letter day name, e.g. "Mon".</summary>
    public static string ToShortName(this DayOfWeek day) => day.ToString()[..3];
}
