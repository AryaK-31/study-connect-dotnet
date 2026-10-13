using StudyConnect.Core.Common;
using StudyConnect.Core.Entities;
using StudyConnect.Core.Enums;
using StudyConnect.Core.Matching;
using StudyConnect.Core.Models;

namespace StudyConnect.Core.Abstractions;

// ---------------------------------------------------------------------------
// Service contracts. The UI depends only on these interfaces (low coupling);
// the Infrastructure project supplies the implementations through dependency injection.
// ---------------------------------------------------------------------------

/// <summary>Registration, login and password management.</summary>
public interface IAuthService
{
    Task<Result<User>> RegisterAsync(RegistrationRequest request, CancellationToken cancellationToken = default);

    Task<Result<User>> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<PasswordResetRequestResult> RequestPasswordResetAsync(string email, string resetPageUrl, CancellationToken cancellationToken = default);

    Task<OperationResult> ResetPasswordAsync(string token, string newPassword, string confirmPassword, CancellationToken cancellationToken = default);

    Task<OperationResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword, string confirmPassword, CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>Reading and editing a student's own profile.</summary>
public interface IProfileService
{
    Task<User?> GetUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateProfileAsync(int userId, ProfileUpdate update, CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteAccountAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Most common interests across all students (used as quick-pick suggestions).</summary>
    Task<IReadOnlyList<string>> GetPopularInterestsAsync(int take = 16, CancellationToken cancellationToken = default);
}

/// <summary>Peer connections (the "connection request before chatting" feature).</summary>
public interface IConnectionService
{
    Task<OperationResult> SendRequestAsync(int fromUserId, int toUserId, CancellationToken cancellationToken = default);

    Task<OperationResult> AcceptAsync(int userId, int requesterId, CancellationToken cancellationToken = default);

    Task<OperationResult> DeclineAsync(int userId, int requesterId, CancellationToken cancellationToken = default);

    /// <summary>Cancels a pending request sent by the user or removes an accepted connection.</summary>
    Task<OperationResult> RemoveAsync(int userId, int otherUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetConnectionsAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetIncomingRequestsAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetOutgoingRequestsAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, PeerStatus>> GetPeerStatusesAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> AreConnectedAsync(int firstUserId, int secondUserId, CancellationToken cancellationToken = default);
}

/// <summary>One-to-one chat between connected students.</summary>
public interface IMessageService
{
    Task<Result<DirectMessage>> SendAsync(int senderId, int recipientId, string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DirectMessage>> GetConversationAsync(int userId, int otherUserId, int take = 100, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(int userId, CancellationToken cancellationToken = default);

    Task<int> MarkConversationReadAsync(int userId, int otherUserId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteConversationAsync(int userId, int otherUserId, CancellationToken cancellationToken = default);
}

/// <summary>Creating, joining and leaving study sessions.</summary>
public interface IStudySessionService
{
    Task<IReadOnlyList<StudySession>> GetSessionsAsync(SessionQuery query, CancellationToken cancellationToken = default);

    Task<Result<StudySession>> CreateAsync(int creatorId, SessionCreateRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> JoinAsync(int userId, int sessionId, CancellationToken cancellationToken = default);

    Task<OperationResult> LeaveAsync(int userId, int sessionId, CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteAsync(int userId, int sessionId, CancellationToken cancellationToken = default);
}

/// <summary>Subjects, labs and enrolments.</summary>
public interface ISubjectService
{
    Task<IReadOnlyList<Subject>> GetSubjectsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Enrollment>> GetEnrollmentsAsync(int userId, CancellationToken cancellationToken = default);

    Task<Result<Enrollment>> SaveEnrollmentAsync(int userId, EnrollmentRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> UnenrollAsync(int userId, int subjectId, CancellationToken cancellationToken = default);
}

/// <summary>Student matching: "similar students" and the lab-aware Team Finder.</summary>
public interface IMatchmakingService
{
    Task<IReadOnlyList<MatchResult>> GetSimilarStudentsAsync(int userId, int take = 9, CancellationToken cancellationToken = default);

    Task<TeamFinderResult> FindTeammatesAsync(int userId, int subjectId, TeamFinderFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>Assignment teams, invitations and team chat.</summary>
public interface ITeamService
{
    Task<Result<Team>> CreateTeamAsync(int leaderId, TeamCreateRequest request, CancellationToken cancellationToken = default);

    Task<OperationResult> UpdateTeamAsync(int userId, int teamId, TeamUpdateRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Team>> GetTeamsForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task<Team?> GetTeamAsync(int teamId, CancellationToken cancellationToken = default);

    Task<OperationResult> InviteAsync(int inviterId, int teamId, int inviteeId, string? message, CancellationToken cancellationToken = default);

    Task<OperationResult> RequestToJoinAsync(int userId, int teamId, string? message, CancellationToken cancellationToken = default);

    Task<OperationResult> RespondToInvitationAsync(int userId, int invitationId, bool accept, CancellationToken cancellationToken = default);

    Task<OperationResult> CancelInvitationAsync(int userId, int invitationId, CancellationToken cancellationToken = default);

    /// <summary>Invitations addressed to the user plus join requests for the user's teams.</summary>
    Task<IReadOnlyList<TeamInvitation>> GetPendingInvitationsAsync(int userId, CancellationToken cancellationToken = default);

    Task<OperationResult> LeaveTeamAsync(int userId, int teamId, CancellationToken cancellationToken = default);

    Task<OperationResult> RemoveMemberAsync(int leaderId, int teamId, int memberId, CancellationToken cancellationToken = default);

    Task<Result<TeamMessage>> PostMessageAsync(int userId, int teamId, string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamMessage>> GetMessagesAsync(int userId, int teamId, int take = 100, CancellationToken cancellationToken = default);
}

/// <summary>Persistent in-app notifications (the bell menu).</summary>
public interface INotificationService
{
    Task NotifyAsync(int userId, NotificationType type, string title, string body, string? link = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetRecentAsync(int userId, int take = 15, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>Analytics and machine-learning powered insights.</summary>
public interface IInsightsService
{
    Task<InsightsReport> GetReportAsync(int? subjectId, CancellationToken cancellationToken = default);
}

/// <summary>Sends e-mails. Two implementations exist: SMTP and a local "outbox" folder for development.</summary>
public interface IEmailSender
{
    /// <summary>False when mail is only written to disk (development); the UI then shows reset links directly.</summary>
    bool DeliversExternally { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Delegate type for real-time event handlers.</summary>
public delegate Task RealtimeEventHandler(RealtimeEvent realtimeEvent);

/// <summary>
/// Publish/subscribe hub that pushes events (new messages, notifications, team changes) to every
/// open browser tab of a user. Blazor Server components subscribe with a delegate and re-render when
/// an event arrives over their SignalR circuit - this replaces Socket.io from the Node.js version.
/// </summary>
public interface IRealtimeNotifier
{
    IDisposable Subscribe(int userId, RealtimeEventHandler handler);

    Task PublishAsync(int userId, RealtimeEvent realtimeEvent);

    Task PublishAsync(IEnumerable<int> userIds, RealtimeEvent realtimeEvent);

    bool IsOnline(int userId);

    int OnlineCount { get; }
}
