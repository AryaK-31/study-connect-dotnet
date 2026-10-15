namespace StudyConnect.Infrastructure.Services;

/// <summary>Study session management.</summary>
public sealed class StudySessionService : IStudySessionService
{
    public const int MinCapacity = 2;
    public const int MaxCapacity = 100;
    public const int MinDuration = 15;
    public const int MaxDuration = 480;

    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;
    private readonly INotificationService _notifications;
    private readonly IRealtimeNotifier _realtime;

    public StudySessionService(IDbContextFactory<StudyConnectDbContext> dbFactory, INotificationService notifications, IRealtimeNotifier realtime)
    {
        _dbFactory = dbFactory;
        _notifications = notifications;
        _realtime = realtime;
    }

    public async Task<IReadOnlyList<StudySession>> GetSessionsAsync(SessionQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<StudySession> sessions = db.StudySessions
            .AsNoTracking()
            .Include(s => s.Creator)
            .Include(s => s.Subject)
            .Include(s => s.Attendees).ThenInclude(a => a.User)
            .AsSplitQuery();

        if (!query.IncludePast)
        {
            DateTime cutoff = DateTime.Now.AddHours(-1);
            sessions = sessions.Where(s => s.StartsAt >= cutoff);
        }

        if (query.SubjectId is int subjectId)
        {
            sessions = sessions.Where(s => s.SubjectId == subjectId);
        }

        if (query.AttendedByUserId is int attendeeId)
        {
            sessions = sessions.Where(s => s.Attendees.Any(a => a.UserId == attendeeId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim().ToLower();
            sessions = sessions.Where(s => s.Title.ToLower().Contains(term) || s.Topic.ToLower().Contains(term) || s.Location.ToLower().Contains(term));
        }

        return await sessions.OrderBy(s => s.StartsAt).ToListAsync(cancellationToken);
    }

    public async Task<Result<StudySession>> CreateAsync(int creatorId, SessionCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string title = (request.Title ?? string.Empty).Trim();
        string topic = (request.Topic ?? string.Empty).Trim();
        string location = string.IsNullOrWhiteSpace(request.Location) ? "Online" : request.Location.Trim();

        if (title.Length < 3 || title.Length > 100)
        {
            return Result<StudySession>.Failure("Title must be between 3 and 100 characters.");
        }

        if (topic.Length < 2 || topic.Length > 150)
        {
            return Result<StudySession>.Failure("Topic must be between 2 and 150 characters.");
        }

        if (request.StartsAt <= DateTime.Now)
        {
            return Result<StudySession>.Failure("The session must start in the future.");
        }

        if (request.Capacity < MinCapacity || request.Capacity > MaxCapacity)
        {
            return Result<StudySession>.Failure($"Capacity must be between {MinCapacity} and {MaxCapacity}.");
        }

        if (request.DurationMinutes < MinDuration || request.DurationMinutes > MaxDuration)
        {
            return Result<StudySession>.Failure($"Duration must be between {MinDuration} and {MaxDuration} minutes.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (request.SubjectId is int subjectId && !await db.Subjects.AnyAsync(s => s.Id == subjectId, cancellationToken))
        {
            return Result<StudySession>.Failure("The selected subject does not exist.");
        }

        var session = new StudySession
        {
            Title = title,
            Topic = topic,
            StartsAt = request.StartsAt,
            DurationMinutes = request.DurationMinutes,
            Location = location,
            Mode = request.Mode,
            Capacity = request.Capacity,
            CreatorId = creatorId,
            SubjectId = request.SubjectId
        };

        // The creator automatically attends their own session.
        session.Attendees.Add(new SessionAttendee { UserId = creatorId });

        db.StudySessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return Result<StudySession>.Success(session);
    }

    public async Task<OperationResult> JoinAsync(int userId, int sessionId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        StudySession? session = await db.StudySessions
            .Include(s => s.Attendees)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return OperationResult.Failure("Session not found.");
        }

        if (session.IsAttending(userId))
        {
            return OperationResult.Success();
        }

        if (session.IsFull)
        {
            return OperationResult.Failure("Sorry, this session is full.");
        }

        if (session.HasEnded)
        {
            return OperationResult.Failure("This session has already finished.");
        }

        User? joiner = await db.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (joiner is null)
        {
            return OperationResult.Failure("Account not found.");
        }

        session.Attendees.Add(new SessionAttendee { UserId = userId });
        await db.SaveChangesAsync(cancellationToken);

        // Replaces the e-mail the Node.js version sent to the session creator.
        if (session.CreatorId != userId)
        {
            string contact = string.IsNullOrWhiteSpace(joiner.ContactNumber) ? joiner.Email : $"{joiner.Email} · {joiner.ContactNumber}";
            await _notifications.NotifyAsync(
                session.CreatorId,
                NotificationType.SessionJoined,
                $"{joiner.Name} joined \"{session.Title}\"",
                $"Contact: {contact}",
                "/sessions",
                cancellationToken);
        }

        await PublishSessionChangedAsync(session, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> LeaveAsync(int userId, int sessionId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        StudySession? session = await db.StudySessions
            .Include(s => s.Attendees)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return OperationResult.Failure("Session not found.");
        }

        if (session.CreatorId == userId)
        {
            return OperationResult.Failure("The creator cannot leave their own session. Delete it instead.");
        }

        SessionAttendee? attendance = session.Attendees.FirstOrDefault(a => a.UserId == userId);
        if (attendance is null)
        {
            return OperationResult.Failure("You are not attending this session.");
        }

        db.SessionAttendees.Remove(attendance);
        await db.SaveChangesAsync(cancellationToken);

        await PublishSessionChangedAsync(session, cancellationToken);
        return OperationResult.Success();
    }

    public async Task<OperationResult> DeleteAsync(int userId, int sessionId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        StudySession? session = await db.StudySessions
            .Include(s => s.Attendees)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return OperationResult.Failure("Session not found.");
        }

        if (session.CreatorId != userId)
        {
            return OperationResult.Failure("Only the creator can delete this session.");
        }

        List<int> attendeeIds = session.Attendees.Select(a => a.UserId).Where(id => id != userId).ToList();
        string title = session.Title;

        db.StudySessions.Remove(session);
        await db.SaveChangesAsync(cancellationToken);

        foreach (int attendeeId in attendeeIds)
        {
            await _notifications.NotifyAsync(attendeeId, NotificationType.SessionCancelled, $"\"{title}\" was cancelled", "The organiser deleted this study session.", "/sessions", cancellationToken);
        }

        await _realtime.PublishAsync(attendeeIds.Append(userId), new RealtimeEvent(RealtimeEventType.SessionChanged, userId, sessionId));
        return OperationResult.Success();
    }

    private Task PublishSessionChangedAsync(StudySession session, CancellationToken cancellationToken) =>
        _realtime.PublishAsync(
            session.Attendees.Select(a => a.UserId).Append(session.CreatorId),
            new RealtimeEvent(RealtimeEventType.SessionChanged, EntityId: session.Id));
}
