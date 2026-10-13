using System.ComponentModel.DataAnnotations.Schema;
using StudyConnect.Core.Enums;
using StudyConnect.Core.Extensions;
using StudyConnect.Core.Matching;

namespace StudyConnect.Core.Entities;

/// <summary>A registered student.</summary>
public class User : EntityBase
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Login e-mail, always stored in lower case.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash produced by <see cref="Security.IPasswordHasher"/>. Never the raw password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string? ContactNumber { get; set; }

    public string? Degree { get; set; }

    public string? Bio { get; set; }

    /// <summary>True once the student has saved interests and availability at least once.</summary>
    public bool ProfileCompleted { get; set; }

    /// <summary>SHA-256 hash of the current password-reset token (the raw token is only e-mailed).</summary>
    public string? PasswordResetTokenHash { get; set; }

    public DateTime? PasswordResetExpiresUtc { get; set; }

    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UserInterest> Interests { get; set; } = new List<UserInterest>();

    public ICollection<UserAvailability> Availability { get; set; } = new List<UserAvailability>();

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    // ----- Computed helpers (read-only properties are not mapped by EF Core) -----

    /// <summary>Two-letter initials used by the avatar component.</summary>
    public string Initials => Name.ToInitials();

    /// <summary>First name only, used for friendly greetings.</summary>
    public string FirstName => Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? Name;

    /// <summary>Interests formatted for display (title case).</summary>
    [NotMapped]
    public IEnumerable<string> InterestNames => Interests.Select(i => i.Name.ToTitleCase()).OrderBy(n => n);

    /// <summary>Availability converted into value objects used by the matching engine.</summary>
    [NotMapped]
    public IEnumerable<AvailabilitySlot> AvailabilitySlots => Availability.Select(a => a.ToSlot());
}

/// <summary>One interest/topic tag of a student (stored normalised in lower case).</summary>
public class UserInterest : EntityBase
{
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
}

/// <summary>One free time block of a student, e.g. Monday afternoon.</summary>
public class UserAvailability : EntityBase
{
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public DayOfWeek Day { get; set; }

    public TimeBlock Block { get; set; }

    public AvailabilitySlot ToSlot() => new(Day, Block);
}
