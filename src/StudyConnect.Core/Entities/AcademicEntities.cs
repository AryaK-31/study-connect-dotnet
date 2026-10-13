using System.Globalization;
using StudyConnect.Core.Enums;

namespace StudyConnect.Core.Entities;

/// <summary>A university subject, e.g. "31927 Applications Development with .NET".</summary>
public class Subject : EntityBase
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Smallest allowed assignment group (inclusive).</summary>
    public int MinTeamSize { get; set; } = 2;

    /// <summary>Largest allowed assignment group (inclusive).</summary>
    public int MaxTeamSize { get; set; } = 3;

    public ICollection<LabSlot> Labs { get; set; } = new List<LabSlot>();

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public string DisplayName => $"{Code} – {Name}";
}

/// <summary>
/// A weekly laboratory/tutorial class of a subject. Assignment groups must be formed
/// within the same lab, which is why matching is lab-aware.
/// </summary>
public class LabSlot : EntityBase
{
    public int SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;

    /// <summary>Short code such as "Lab 02".</summary>
    public string Code { get; set; } = string.Empty;

    public DayOfWeek Day { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Room { get; set; } = string.Empty;

    public string Tutor { get; set; } = string.Empty;

    /// <summary>Human friendly summary, e.g. "Lab 02 · Mon 13:00–15:00".</summary>
    public string Display =>
        $"{Code} · {Day.ToString()[..3]} {StartTime.ToString("HH:mm", CultureInfo.InvariantCulture)}–{EndTime.ToString("HH:mm", CultureInfo.InvariantCulture)}";
}

/// <summary>
/// A student's enrolment in a subject, including the lab they attend and the
/// team-formation preferences used by the Team Finder.
/// </summary>
public class Enrollment : EntityBase
{
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public int SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;

    public int LabSlotId { get; set; }

    public LabSlot LabSlot { get; set; } = null!;

    /// <summary>True while the student is available to join an assignment team.</summary>
    public bool LookingForTeam { get; set; } = true;

    public TeamRole PreferredRole { get; set; } = TeamRole.FullStack;

    public SkillLevel Skill { get; set; } = SkillLevel.Intermediate;

    public TargetGrade Goal { get; set; } = TargetGrade.Distinction;

    /// <summary>Short pitch shown to potential team mates.</summary>
    public string? Pitch { get; set; }
}

/// <summary>An assignment group within one subject and one lab.</summary>
public class Team : EntityBase
{
    public const int MaximumAllowedMembers = 6;

    public int SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;

    public int LabSlotId { get; set; }

    public LabSlot LabSlot { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? ProjectIdea { get; set; }

    public int MaxMembers { get; set; } = 3;

    public int LeaderId { get; set; }

    public User Leader { get; set; } = null!;

    /// <summary>When false the team no longer accepts join requests.</summary>
    public bool IsOpen { get; set; } = true;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();

    public ICollection<TeamInvitation> Invitations { get; set; } = new List<TeamInvitation>();

    public ICollection<TeamMessage> Messages { get; set; } = new List<TeamMessage>();

    // ----- Computed helpers -----

    public bool IsFull => Members.Count >= MaxMembers;

    public int SpotsLeft => Math.Max(0, MaxMembers - Members.Count);

    public bool IsRecruiting => IsOpen && !IsFull;

    public bool HasMember(int userId) => Members.Any(m => m.UserId == userId);
}

/// <summary>Join table: a student who belongs to a team.</summary>
public class TeamMember : EntityBase
{
    public int TeamId { get; set; }

    public Team Team { get; set; } = null!;

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public TeamRole Role { get; set; }
}

/// <summary>An invitation from a team to a student, or a join request from a student to a team.</summary>
public class TeamInvitation : EntityBase
{
    public int TeamId { get; set; }

    public Team Team { get; set; } = null!;

    /// <summary>The student being invited, or the student asking to join.</summary>
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public InvitationKind Kind { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public string? Message { get; set; }

    public DateTime? RespondedAtUtc { get; set; }
}

/// <summary>A message in a team's group chat.</summary>
public class TeamMessage : EntityBase
{
    public int TeamId { get; set; }

    public Team Team { get; set; } = null!;

    public int SenderId { get; set; }

    public User Sender { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
}
