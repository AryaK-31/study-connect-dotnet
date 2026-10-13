using StudyConnect.Core.Enums;
using StudyConnect.Core.Extensions;

namespace StudyConnect.Core.Matching;

/// <summary>A free time block in the week (value object).</summary>
public readonly record struct AvailabilitySlot(DayOfWeek Day, TimeBlock Block)
{
    public override string ToString() => $"{Day.ToShortName()} {Block}";

    /// <summary>Parses the "Monday:Morning" format used by the seed files.</summary>
    public static bool TryParse(string? text, out AvailabilitySlot slot)
    {
        slot = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && Enum.TryParse(parts[0], ignoreCase: true, out DayOfWeek day)
            && Enum.TryParse(parts[1], ignoreCase: true, out TimeBlock block))
        {
            slot = new AvailabilitySlot(day, block);
            return true;
        }

        return false;
    }
}

/// <summary>
/// Everything the matching engine needs to know about a student for one subject.
/// It is a plain object (no EF Core types) which keeps the algorithms decoupled from the
/// database and makes them trivial to unit test.
/// </summary>
public sealed class StudentProfile
{
    public StudentProfile(int userId, string name, IEnumerable<string> interests, IEnumerable<AvailabilitySlot> availability)
    {
        UserId = userId;
        Name = name;
        Interests = new HashSet<string>(interests.Select(i => i.NormalizeTag()).Where(i => i.Length > 0));
        Availability = new HashSet<AvailabilitySlot>(availability);
    }

    public int UserId { get; }

    public string Name { get; }

    /// <summary>Normalised (lower-case) interest tags.</summary>
    public IReadOnlySet<string> Interests { get; }

    public IReadOnlySet<AvailabilitySlot> Availability { get; }

    public string? Email { get; init; }

    public string? Degree { get; init; }

    public string? Pitch { get; init; }

    /// <summary>Lab the student attends for the subject being matched (null when not enrolled).</summary>
    public int? LabSlotId { get; init; }

    public string? LabLabel { get; init; }

    public TeamRole PreferredRole { get; init; } = TeamRole.FullStack;

    public SkillLevel Skill { get; init; } = SkillLevel.Intermediate;

    public TargetGrade Goal { get; init; } = TargetGrade.Distinction;

    public bool LookingForTeam { get; init; } = true;

    public override string ToString() => $"{Name} (#{UserId})";
}
