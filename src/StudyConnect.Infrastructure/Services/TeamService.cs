using StudyConnect.Core.Validation;

namespace StudyConnect.Infrastructure.Services;

/// <summary>
/// Assignment teams: creation, invitations, join requests, membership and team chat.
/// Business rules enforced here:
/// <list type="bullet">
/// <item>every member must be enrolled in the subject and attend the <b>same lab</b> as the team;</item>
/// <item>a student can belong to only one team per subject;</item>
/// <item>a team cannot exceed its maximum size (which must respect the subject's limits).</item>
/// </list>
/// </summary>
public sealed class TeamService : ITeamService
{
    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;
    private readonly INotificationService _notifications;
    private readonly IRealtimeNotifier _realtime;
    private readonly ILogger<TeamService> _logger;

    public TeamService(
        IDbContextFactory<StudyConnectDbContext> dbFactory,
        INotificationService notifications,
        IRealtimeNotifier realtime,
        ILogger<TeamService> logger)
    {
        _dbFactory = dbFactory;
        _notifications = notifications;
        _realtime = realtime;
        _logger = logger;
    }

    // ------------------------------------------------------------------ queries

    public async Task<IReadOnlyList<Team>> GetTeamsForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Teams
            .AsNoTracking()
            .Include(t => t.Subject)
            .Include(t => t.LabSlot)
            .Include(t => t.Members).ThenInclude(m => m.User)
            .Where(t => t.Members.Any(m => m.UserId == userId))
            .AsSplitQuery()
            .OrderBy(t => t.Subject.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<Team?> GetTeamAsync(int teamId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Teams
            .AsNoTracking()
            .Include(t => t.Subject)
            .Include(t => t.LabSlot)
            .Include(t => t.Leader)
            .Include(t => t.Members).ThenInclude(m => m.User).ThenInclude(u => u.Interests)
            .Include(t => t.Members).ThenInclude(m => m.User).ThenInclude(u => u.Availability)
            .Include(t => t.Invitations).ThenInclude(i => i.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
    }

    public async Task<IReadOnlyList<TeamInvitation>> GetPendingInvitationsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Invitations sent to me, plus join requests sent to any team I belong to.
        return await db.TeamInvitations
            .AsNoTracking()
            .Include(i => i.User)
            .Include(i => i.Team).ThenInclude(t => t.Subject)
            .Include(i => i.Team).ThenInclude(t => t.LabSlot)
            .Include(i => i.Team).ThenInclude(t => t.Members).ThenInclude(m => m.User)
            .Where(i => i.Status == InvitationStatus.Pending
                        && ((i.Kind == InvitationKind.Invite && i.UserId == userId)
                            || (i.Kind == InvitationKind.JoinRequest && i.Team.LeaderId == userId)))
            .AsSplitQuery()
            .OrderByDescending(i => i.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TeamMessage>> GetMessagesAsync(int userId, int teamId, int take = 100, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (!await db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken))
        {
            return Array.Empty<TeamMessage>();
        }

        List<TeamMessage> latest = await db.TeamMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.TeamId == teamId)
            .OrderByDescending(m => m.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        latest.Reverse();
        return latest;
    }

    // ------------------------------------------------------------------ team lifecycle

    public async Task<Result<Team>> CreateTeamAsync(int leaderId, TeamCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string name = (request.Name ?? string.Empty).Trim();
        if (name.Length < 3 || name.Length > 60)
        {
            return Result<Team>.Failure("Team name must be between 3 and 60 characters.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        Subject? subject = await db.Subjects.FindAsync(new object[] { request.SubjectId }, cancellationToken);
        if (subject is null)
        {
            return Result<Team>.Failure("Subject not found.");
        }

        // Global cap: allow up to 6 members regardless of the subject's MaxTeamSize.
        if (request.MaxMembers < subject.MinTeamSize || request.MaxMembers > Team.MaximumAllowedMembers)
        {
            return Result<Team>.Failure($"Teams in {subject.Code} must have {subject.MinTeamSize}-{Team.MaximumAllowedMembers} members.");
        }

        Enrollment? enrollment = await db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == leaderId && e.SubjectId == request.SubjectId, cancellationToken);
        if (enrollment is null)
        {
            return Result<Team>.Failure($"Enrol in {subject.Code} (and choose your lab) before creating a team.");
        }

        if (await IsInTeamForSubjectAsync(db, leaderId, request.SubjectId, cancellationToken))
        {
            return Result<Team>.Failure($"You already belong to a team for {subject.Code}.");
        }

        var team = new Team
        {
            SubjectId = request.SubjectId,
            LabSlotId = enrollment.LabSlotId,
            Name = name,
            ProjectIdea = string.IsNullOrWhiteSpace(request.ProjectIdea) ? null : request.ProjectIdea.Trim().Truncate(500),
            MaxMembers = request.MaxMembers,
            LeaderId = leaderId,
            IsOpen = true
        };

        team.Members.Add(new TeamMember { UserId = leaderId, Role = enrollment.PreferredRole });
        enrollment.LookingForTeam = false;

        db.Teams.Add(team);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Team {TeamId} '{Name}' created by {UserId}", team.Id, team.Name, leaderId);
        return Result<Team>.Success(team);
    }

    public async Task<OperationResult> UpdateTeamAsync(int userId, int teamId, TeamUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string name = (request.Name ?? string.Empty).Trim();
        if (name.Length < 3 || name.Length > 60)
        {
            return OperationResult.Failure("Team name must be between 3 and 60 characters.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        Team? team = await db.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

        if (team is null)
        {
            return OperationResult.Failure("Team not found.");
        }

        if (team.LeaderId != userId)
        {
            return OperationResult.Failure("Only the team leader can edit the team.");
        }

        team.Name = name;
        team.ProjectIdea = string.IsNullOrWhiteSpace(request.ProjectIdea) ? null : request.ProjectIdea.Trim().Truncate(500);
        team.IsOpen = request.IsOpen;
        await db.SaveChangesAsync(cancellationToken);

        await PublishTeamChangedAsync(team, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> LeaveTeamAsync(int userId, int teamId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        Team? team = await db.Teams
            .Include(t => t.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

        TeamMember? membership = team?.Members.FirstOrDefault(m => m.UserId == userId);
        if (team is null || membership is null)
        {
            return OperationResult.Failure("You are not a member of this team.");
        }

        string leaverName = membership.User.Name;
        List<int> remaining = team.Members.Where(m => m.UserId != userId).Select(m => m.UserId).ToList();

        db.TeamMembers.Remove(membership);
        await SetLookingForTeamAsync(db, userId, team.SubjectId, true, cancellationToken);

        if (remaining.Count == 0)
        {
            db.Teams.Remove(team);
        }
        else if (team.LeaderId == userId)
        {
            // Hand leadership to the longest-standing remaining member.
            team.LeaderId = team.Members
                .Where(m => m.UserId != userId)
                .OrderBy(m => m.CreatedAtUtc)
                .First()
                .UserId;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (int memberId in remaining)
        {
            await _notifications.NotifyAsync(memberId, NotificationType.TeamUpdate, $"{leaverName} left {team.Name}", "Your team has a free spot again.", $"/teams/{team.Id}", cancellationToken);
        }

        await _realtime.PublishAsync(remaining.Append(userId), new RealtimeEvent(RealtimeEventType.TeamChanged, userId, teamId));
        return OperationResult.Success();
    }

    public async Task<OperationResult> RemoveMemberAsync(int leaderId, int teamId, int memberId, CancellationToken cancellationToken = default)
    {
        if (leaderId == memberId)
        {
            return OperationResult.Failure("Use 'Leave team' to remove yourself.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        Team? team = await db.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

        if (team is null)
        {
            return OperationResult.Failure("Team not found.");
        }

        if (team.LeaderId != leaderId)
        {
            return OperationResult.Failure("Only the team leader can remove members.");
        }

        TeamMember? membership = team.Members.FirstOrDefault(m => m.UserId == memberId);
        if (membership is null)
        {
            return OperationResult.Failure("That student is not in your team.");
        }

        db.TeamMembers.Remove(membership);
        await SetLookingForTeamAsync(db, memberId, team.SubjectId, true, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await _notifications.NotifyAsync(memberId, NotificationType.TeamUpdate, $"You were removed from {team.Name}", "You are visible in Team Finder again.", "/team-finder", cancellationToken);
        await _realtime.PublishAsync(team.Members.Select(m => m.UserId).Append(memberId), new RealtimeEvent(RealtimeEventType.TeamChanged, leaderId, teamId));
        return OperationResult.Success();
    }

    // ------------------------------------------------------------------ invitations & join requests

    public async Task<OperationResult> InviteAsync(int inviterId, int teamId, int inviteeId, string? message, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        Team? team = await db.Teams
            .Include(t => t.Members).ThenInclude(m => m.User)
            .Include(t => t.Subject)
            .FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

        if (team is null)
        {
            return OperationResult.Failure("Team not found.");
        }

        TeamMember? inviter = team.Members.FirstOrDefault(m => m.UserId == inviterId);
        if (inviter is null)
        {
            return OperationResult.Failure("Only team members can invite students.");
        }

        if (team.IsFull)
        {
            return OperationResult.Failure("Your team is already full.");
        }

        OperationResult eligibility = await CheckEligibilityAsync(db, team, inviteeId, cancellationToken);
        if (eligibility.Failed)
        {
            return eligibility;
        }

        TeamInvitation? existing = await db.TeamInvitations.FirstOrDefaultAsync(
            i => i.TeamId == teamId && i.UserId == inviteeId && i.Status == InvitationStatus.Pending,
            cancellationToken);

        if (existing is not null)
        {
            return existing.Kind == InvitationKind.JoinRequest
                ? OperationResult.Failure("This student already asked to join - accept their request on the Teams page.")
                : OperationResult.Failure("This student has already been invited.");
        }

        db.TeamInvitations.Add(new TeamInvitation
        {
            TeamId = teamId,
            UserId = inviteeId,
            Kind = InvitationKind.Invite,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim().Truncate(300)
        });
        await db.SaveChangesAsync(cancellationToken);

        await _notifications.NotifyAsync(
            inviteeId,
            NotificationType.TeamInvite,
            $"{inviter.User.Name} invited you to join {team.Name}",
            $"{team.Subject.Code} · {team.Members.Count}/{team.MaxMembers} members",
            "/teams",
            cancellationToken);

        return OperationResult.Success();
    }

    public async Task<OperationResult> RequestToJoinAsync(int userId, int teamId, string? message, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        Team? team = await db.Teams
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);

        if (team is null)
        {
            return OperationResult.Failure("Team not found.");
        }

        if (!team.IsRecruiting)
        {
            return OperationResult.Failure("This team is not accepting new members.");
        }

        if (team.IsFull)
        {
            return OperationResult.Failure("This team is already full.");
        }

        // Allow requests even for users without an Enrollment.
        // If an Enrollment exists, require same lab; otherwise allow the request.
        if (!await IsUserInSameLabOrNoEnrollmentAsync(db, userId, team, cancellationToken))
        {
            return OperationResult.Failure("Only students who attend the same lab can request to join this team.");
        }

        TeamInvitation? existing = await db.TeamInvitations.FirstOrDefaultAsync(
            i => i.TeamId == teamId && i.UserId == userId && i.Status == InvitationStatus.Pending,
            cancellationToken);

        if (existing is not null)
        {
            return existing.Kind == InvitationKind.Invite
                ? OperationResult.Failure("This team already invited you - accept the invitation on the Teams page.")
                : OperationResult.Failure("You have already asked to join this team.");
        }

        User? requester = await db.Users.FindAsync(new object[] { userId }, cancellationToken);

        db.TeamInvitations.Add(new TeamInvitation
        {
            TeamId = teamId,
            UserId = userId,
            Kind = InvitationKind.JoinRequest,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim().Truncate(300)
        });
        await db.SaveChangesAsync(cancellationToken);

        foreach (int memberId in team.Members.Select(m => m.UserId))
        {
            await _notifications.NotifyAsync(
                memberId,
                NotificationType.TeamJoinRequest,
                $"{requester?.Name ?? "A student"} wants to join {team.Name}",
                "Review the request on the Teams page.",
                "/teams",
                cancellationToken);
        }

        return OperationResult.Success();
    }

    public async Task<OperationResult> RespondToInvitationAsync(int userId, int invitationId, bool accept, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        TeamInvitation? invitation = await db.TeamInvitations
            .Include(i => i.User)
            .Include(i => i.Team).ThenInclude(t => t.Members)
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

        if (invitation is null || invitation.Status != InvitationStatus.Pending)
        {
            return OperationResult.Failure("This invitation is no longer available.");
        }

        Team team = invitation.Team;

        // Invitations are answered by the invited student; only the team leader handles join requests.
        bool allowed = invitation.Kind == InvitationKind.Invite
            ? invitation.UserId == userId
            : team.LeaderId == userId;

        if (!allowed)
        {
            return OperationResult.Failure(invitation.Kind == InvitationKind.JoinRequest
                ? "Only the team leader can respond to join requests."
                : "You cannot respond to this invitation.");
        }

        invitation.RespondedAtUtc = DateTime.UtcNow;

        if (!accept)
        {
            invitation.Status = InvitationStatus.Declined;
            await db.SaveChangesAsync(cancellationToken);

            int notifyId = invitation.Kind == InvitationKind.Invite ? team.LeaderId : invitation.UserId;
            string title = invitation.Kind == InvitationKind.Invite
                ? $"{invitation.User.Name} declined the invitation to {team.Name}"
                : $"Your request to join {team.Name} was declined";

            await _notifications.NotifyAsync(notifyId, NotificationType.TeamUpdate, title, "Keep looking in Team Finder - there are more students in your lab.", "/team-finder", cancellationToken);
            return OperationResult.Success();
        }

        // ----- Accepting: re-check every rule because things may have changed since the invite. -----
        if (team.IsFull)
        {
            return OperationResult.Failure($"{team.Name} is already full.");
        }

        OperationResult eligibility = await CheckEligibilityAsync(db, team, invitation.UserId, cancellationToken);
        if (eligibility.Failed)
        {
            return eligibility;
        }

        Enrollment? enrollment = await db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == invitation.UserId && e.SubjectId == team.SubjectId, cancellationToken);

        team.Members.Add(new TeamMember { UserId = invitation.UserId, Role = enrollment?.PreferredRole ?? TeamRole.FullStack });
        invitation.Status = InvitationStatus.Accepted;
        if (enrollment is not null)
        {
            enrollment.LookingForTeam = false;
        }

        // The new member cannot join another team for this subject, so withdraw their other pending invitations.
        List<TeamInvitation> otherInvitations = await db.TeamInvitations
            .Where(i => i.Id != invitation.Id
                        && i.UserId == invitation.UserId
                        && i.Status == InvitationStatus.Pending
                        && i.Team.SubjectId == team.SubjectId)
            .ToListAsync(cancellationToken);

        // If the team is now full, close its remaining invitations as well.
        if (team.Members.Count >= team.MaxMembers)
        {
            otherInvitations.AddRange(await db.TeamInvitations
                .Where(i => i.TeamId == team.Id && i.Id != invitation.Id && i.Status == InvitationStatus.Pending)
                .ToListAsync(cancellationToken));
        }

        foreach (TeamInvitation other in otherInvitations.DistinctBy(i => i.Id))
        {
            other.Status = InvitationStatus.Cancelled;
            other.RespondedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} joined team {TeamId}", invitation.UserId, team.Id);

        foreach (int memberId in team.Members.Select(m => m.UserId))
        {
            string title = memberId == invitation.UserId
                ? $"You joined {team.Name} 🎉"
                : $"{invitation.User.Name} joined {team.Name}";

            await _notifications.NotifyAsync(memberId, NotificationType.TeamUpdate, title, $"{team.Members.Count}/{team.MaxMembers} members", $"/teams/{team.Id}", cancellationToken);
        }

        await PublishTeamChangedAsync(team, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> CancelInvitationAsync(int userId, int invitationId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        TeamInvitation? invitation = await db.TeamInvitations
            .Include(i => i.Team).ThenInclude(t => t.Members)
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken);

        if (invitation is null || invitation.Status != InvitationStatus.Pending)
        {
            return OperationResult.Failure("This invitation is no longer pending.");
        }

        // Members can withdraw invitations they sent; students can withdraw their own join requests.
        bool allowed = invitation.Kind == InvitationKind.Invite
            ? invitation.Team.HasMember(userId)
            : invitation.UserId == userId;

        if (!allowed)
        {
            return OperationResult.Failure("You cannot cancel this invitation.");
        }

        invitation.Status = InvitationStatus.Cancelled;
        invitation.RespondedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Success();
    }

    // ------------------------------------------------------------------ team chat

    public async Task<Result<TeamMessage>> PostMessageAsync(int userId, int teamId, string text, CancellationToken cancellationToken = default)
    {
        string trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return Result<TeamMessage>.Failure("Message cannot be empty.");
        }

        if (trimmed.Length > InputValidators.MaxMessageLength)
        {
            return Result<TeamMessage>.Failure($"Messages are limited to {InputValidators.MaxMessageLength} characters.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<int> memberIds = await db.TeamMembers
            .Where(m => m.TeamId == teamId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        if (!memberIds.Contains(userId))
        {
            return Result<TeamMessage>.Failure("Only team members can post in the team chat.");
        }

        var message = new TeamMessage { TeamId = teamId, SenderId = userId, Text = trimmed };
        db.TeamMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        await _realtime.PublishAsync(memberIds, new RealtimeEvent(RealtimeEventType.TeamMessage, userId, teamId, trimmed.Truncate(80)));
        return Result<TeamMessage>.Success(message);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Checks that a student may join <paramref name="team"/> (enrolled, same lab, no other team).</summary>
    private static async Task<OperationResult> CheckEligibilityAsync(StudyConnectDbContext db, Team team, int userId, CancellationToken cancellationToken)
    {
        if (team.HasMember(userId))
        {
            return OperationResult.Failure("This student is already in the team.");
        }

        Enrollment? enrollment = await db.Enrollments
            .Include(e => e.User)
            .FirstOrDefaultAsync(e => e.UserId == userId && e.SubjectId == team.SubjectId, cancellationToken);

        if (enrollment is null)
        {
            return OperationResult.Failure("This student is not enrolled in the team's subject.");
        }

        if (enrollment.LabSlotId != team.LabSlotId)
        {
            return OperationResult.Failure($"{enrollment.User.FirstName} attends a different lab - assignment groups must be from the same lab.");
        }

        if (await IsInTeamForSubjectAsync(db, userId, team.SubjectId, cancellationToken))
        {
            return OperationResult.Failure($"{enrollment.User.FirstName} is already in another team for this subject.");
        }

        return OperationResult.Success();
    }

    private static Task<bool> IsInTeamForSubjectAsync(StudyConnectDbContext db, int userId, int subjectId, CancellationToken cancellationToken) =>
        db.TeamMembers.AnyAsync(m => m.UserId == userId && m.Team.SubjectId == subjectId, cancellationToken);

    private static async Task SetLookingForTeamAsync(StudyConnectDbContext db, int userId, int subjectId, bool looking, CancellationToken cancellationToken)
    {
        Enrollment? enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.SubjectId == subjectId, cancellationToken);
        if (enrollment is not null)
        {
            enrollment.LookingForTeam = looking;
        }
    }

    private static async Task<bool> IsUserInSameLabOrNoEnrollmentAsync(StudyConnectDbContext db, int userId, Team team, CancellationToken cancellationToken)
    {
        Enrollment? enrollment = await db.Enrollments
            .FirstOrDefaultAsync(e => e.UserId == userId && e.SubjectId == team.SubjectId, cancellationToken);

        // If the user has no enrollment for the subject we allow the request.
        if (enrollment is null)
        {
            return true;
        }

        // If enrolled, require same lab.
        return enrollment.LabSlotId == team.LabSlotId;
    }

    private Task PublishTeamChangedAsync(Team team, CancellationToken cancellationToken) =>
        _realtime.PublishAsync(team.Members.Select(m => m.UserId), new RealtimeEvent(RealtimeEventType.TeamChanged, EntityId: team.Id));
}
