using StudyConnect.Core.Enums;

namespace StudyConnect.Core.Entities;

/// <summary>A study session any student can create and others can join.</summary>
public class StudySession : EntityBase
{
    public string Title { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    /// <summary>Local start date/time as entered by the organiser.</summary>
    public DateTime StartsAt { get; set; }

    public int DurationMinutes { get; set; } = 60;

    public string Location { get; set; } = "Online";

    public SessionMode Mode { get; set; } = SessionMode.Online;

    /// <summary>Maximum number of attendees (including the creator).</summary>
    public int Capacity { get; set; } = 10;

    public int CreatorId { get; set; }

    public User Creator { get; set; } = null!;

    /// <summary>Optional subject the session belongs to.</summary>
    public int? SubjectId { get; set; }

    public Subject? Subject { get; set; }

    public ICollection<SessionAttendee> Attendees { get; set; } = new List<SessionAttendee>();

    // ----- Computed helpers -----

    public DateTime EndsAt => StartsAt.AddMinutes(DurationMinutes);

    public bool IsFull => Attendees.Count >= Capacity;

    public int SpotsLeft => Math.Max(0, Capacity - Attendees.Count);

    public bool HasEnded => EndsAt < DateTime.Now;

    public bool IsAttending(int userId) => Attendees.Any(a => a.UserId == userId);
}

/// <summary>Join table: a student attending a study session.</summary>
public class SessionAttendee : EntityBase
{
    public int StudySessionId { get; set; }

    public StudySession StudySession { get; set; } = null!;

    public int UserId { get; set; }

    public User User { get; set; } = null!;
}
