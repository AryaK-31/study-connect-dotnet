using StudyConnect.Core.Entities;
using StudyConnect.Core.Enums;
using StudyConnect.Core.MachineLearning;
using StudyConnect.Core.Matching;

namespace StudyConnect.Core.Models;

// ---------------------------------------------------------------------------
// Data-transfer objects passed between the UI and the service layer.
// Records give us immutability, value equality and concise syntax.
// ---------------------------------------------------------------------------

public sealed record RegistrationRequest(string Name, string Email, string Password, string ConfirmPassword, string? ContactNumber);

public sealed record PasswordResetRequestResult(bool Accepted, string Message, string? DevelopmentResetLink);

public sealed record ProfileUpdate(
    string Name,
    string? ContactNumber,
    string? Degree,
    string? Bio,
    IReadOnlyCollection<string> Interests,
    IReadOnlyCollection<AvailabilitySlot> Availability);

public sealed record SessionCreateRequest(
    string Title,
    string Topic,
    DateTime StartsAt,
    int DurationMinutes,
    string Location,
    SessionMode Mode,
    int Capacity,
    int? SubjectId);

/// <summary>Filters for the study session list.</summary>
public sealed record SessionQuery(string? Search = null, int? SubjectId = null, int? AttendedByUserId = null, bool IncludePast = false);

public sealed record EnrollmentRequest(
    int SubjectId,
    int LabSlotId,
    TeamRole PreferredRole,
    SkillLevel Skill,
    TargetGrade Goal,
    bool LookingForTeam,
    string? Pitch);

public sealed record TeamCreateRequest(int SubjectId, string Name, string? ProjectIdea, int MaxMembers);

public sealed record TeamUpdateRequest(string Name, string? ProjectIdea, bool IsOpen);

/// <summary>One row of the inbox (a conversation with another student).</summary>
public sealed record ConversationSummary(
    int OtherUserId,
    string OtherUserName,
    string? LastMessage,
    DateTime? LastMessageAtUtc,
    bool LastMessageFromMe,
    int UnreadCount,
    bool IsConnected);

/// <summary>Filters chosen on the Team Finder screen.</summary>
public sealed class TeamFinderFilter
{
    /// <summary>Assignment groups must come from the same lab, so this is on by default.</summary>
    public bool SameLabOnly { get; set; } = true;

    public bool OnlyAvailable { get; set; } = true;

    public TeamRole? Role { get; set; }

    public string? Search { get; set; }

    /// <summary>Hide candidates scoring below this (0-100).</summary>
    public double MinimumScore { get; set; }

    /// <summary>Desired team size used for suggestions (clamped to the six-member team limit).</summary>
    public int TeamSize { get; set; } = 3;
}

/// <summary>One candidate shown on the Team Finder screen.</summary>
public sealed record CandidateMatch(MatchResult Match, bool InTeam, PeerStatus PeerStatus, bool HasPendingInvite)
{
    public StudentProfile Profile => Match.Candidate;
}

/// <summary>Everything the Team Finder screen needs, produced in one service call.</summary>
public sealed record TeamFinderResult(
    Subject? Subject,
    Enrollment? MyEnrollment,
    Team? MyTeam,
    IReadOnlyList<CandidateMatch> Candidates,
    IReadOnlyList<TeamSuggestion> Suggestions,
    IReadOnlyList<Team> OpenTeams,
    int TotalEnrolled,
    int AvailableInMyLab)
{
    public static TeamFinderResult NotEnrolled(Subject? subject) =>
        new(subject, null, null, Array.Empty<CandidateMatch>(), Array.Empty<TeamSuggestion>(), Array.Empty<Team>(), 0, 0);
}

/// <summary>A label/value pair rendered by the bar-chart component.</summary>
public sealed record ChartDatum(string Label, double Value, string? Hint = null);

/// <summary>A community discovered by K-Means, resolved to student names for display.</summary>
public sealed record CommunityView(StudyCommunity Community, IReadOnlyList<string> MemberNames);

/// <summary>Analytics shown on the Insights screen.</summary>
public sealed record InsightsReport(
    int TotalStudents,
    int StudentsLookingForTeam,
    int TeamsFormed,
    int UpcomingSessions,
    IReadOnlyList<ChartDatum> TopInterests,
    IReadOnlyList<ChartDatum> LabDemand,
    IReadOnlyList<ChartDatum> RoleDistribution,
    IReadOnlyList<ChartDatum> AvailabilityByDay,
    IReadOnlyList<CommunityView> Communities);

/// <summary>A message to be delivered by an <see cref="Abstractions.IEmailSender"/>.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? PlainTextBody = null);

/// <summary>A real-time event pushed to a signed-in user's open browser tabs.</summary>
public sealed record RealtimeEvent(RealtimeEventType Type, int? FromUserId = null, int? EntityId = null, string? Text = null);
