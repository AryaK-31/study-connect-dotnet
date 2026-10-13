namespace StudyConnect.Infrastructure.Services;

/// <summary>Connection requests between students.</summary>
public sealed class ConnectionService : IConnectionService
{
    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;
    private readonly INotificationService _notifications;
    private readonly IRealtimeNotifier _realtime;
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(
        IDbContextFactory<StudyConnectDbContext> dbFactory,
        INotificationService notifications,
        IRealtimeNotifier realtime,
        ILogger<ConnectionService> logger)
    {
        _dbFactory = dbFactory;
        _notifications = notifications;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task<OperationResult> SendRequestAsync(int fromUserId, int toUserId, CancellationToken cancellationToken = default)
    {
        if (fromUserId == toUserId)
        {
            return OperationResult.Failure("You cannot connect with yourself.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        User? sender = await db.Users.FindAsync(new object[] { fromUserId }, cancellationToken);
        User? receiver = await db.Users.FindAsync(new object[] { toUserId }, cancellationToken);
        if (sender is null || receiver is null)
        {
            return OperationResult.Failure("User not found.");
        }

        Connection? existing = await FindBetweenAsync(db, fromUserId, toUserId, cancellationToken);

        if (existing is not null)
        {
            switch (existing.Status)
            {
                case ConnectionStatus.Accepted:
                    return OperationResult.Failure($"You are already connected with {receiver.Name}.");

                case ConnectionStatus.Pending when existing.RequesterId == fromUserId:
                    return OperationResult.Success(); // idempotent: request already sent

                case ConnectionStatus.Pending:
                    // They already asked us - sending a request back simply accepts theirs.
                    await db.DisposeAsync();
                    return await AcceptAsync(fromUserId, toUserId, cancellationToken);

                default:
                    // A previously declined request may be sent again.
                    existing.RequesterId = fromUserId;
                    existing.AddresseeId = toUserId;
                    existing.Status = ConnectionStatus.Pending;
                    existing.CreatedAtUtc = DateTime.UtcNow;
                    existing.RespondedAtUtc = null;
                    break;
            }
        }
        else
        {
            db.Connections.Add(new Connection { RequesterId = fromUserId, AddresseeId = toUserId });
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Connection request {From} -> {To}", fromUserId, toUserId);

        await _notifications.NotifyAsync(
            toUserId,
            NotificationType.ConnectionRequest,
            $"{sender.Name} wants to connect",
            "Accept the request to start chatting.",
            "/connections",
            cancellationToken);

        await _realtime.PublishAsync(new[] { fromUserId, toUserId }, new RealtimeEvent(RealtimeEventType.ConnectionChanged, fromUserId));
        return OperationResult.Success();
    }

    public async Task<OperationResult> AcceptAsync(int userId, int requesterId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        Connection? request = await db.Connections
            .Include(c => c.Addressee)
            .FirstOrDefaultAsync(
                c => c.RequesterId == requesterId && c.AddresseeId == userId && c.Status == ConnectionStatus.Pending,
                cancellationToken);

        if (request is null)
        {
            return OperationResult.Failure("No pending request from this user.");
        }

        request.Status = ConnectionStatus.Accepted;
        request.RespondedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await _notifications.NotifyAsync(
            requesterId,
            NotificationType.ConnectionAccepted,
            $"{request.Addressee.Name} accepted your request",
            "You can now message each other.",
            $"/messages/{userId}",
            cancellationToken);

        await _realtime.PublishAsync(new[] { userId, requesterId }, new RealtimeEvent(RealtimeEventType.ConnectionChanged, userId));
        return OperationResult.Success();
    }

    public async Task<OperationResult> DeclineAsync(int userId, int requesterId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        Connection? request = await db.Connections.FirstOrDefaultAsync(
            c => c.RequesterId == requesterId && c.AddresseeId == userId && c.Status == ConnectionStatus.Pending,
            cancellationToken);

        if (request is null)
        {
            return OperationResult.Failure("No pending request from this user.");
        }

        request.Status = ConnectionStatus.Declined;
        request.RespondedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await _realtime.PublishAsync(new[] { userId, requesterId }, new RealtimeEvent(RealtimeEventType.ConnectionChanged, userId));
        return OperationResult.Success();
    }

    public async Task<OperationResult> RemoveAsync(int userId, int otherUserId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        Connection? connection = await FindBetweenAsync(db, userId, otherUserId, cancellationToken);
        if (connection is null || connection.Status == ConnectionStatus.Declined)
        {
            return OperationResult.Failure("There is no connection to remove.");
        }

        if (connection.Status == ConnectionStatus.Pending && connection.RequesterId != userId)
        {
            return OperationResult.Failure("Use Decline to reject a request you received.");
        }

        db.Connections.Remove(connection);
        await db.SaveChangesAsync(cancellationToken);

        await _realtime.PublishAsync(new[] { userId, otherUserId }, new RealtimeEvent(RealtimeEventType.ConnectionChanged, userId));
        return OperationResult.Success();
    }

    public async Task<IReadOnlyList<User>> GetConnectionsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<int> ids = await db.Connections
            .Where(c => c.Status == ConnectionStatus.Accepted && (c.RequesterId == userId || c.AddresseeId == userId))
            .Select(c => c.RequesterId == userId ? c.AddresseeId : c.RequesterId)
            .ToListAsync(cancellationToken);

        return await LoadUsersAsync(db, ids, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetIncomingRequestsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<int> ids = await db.Connections
            .Where(c => c.Status == ConnectionStatus.Pending && c.AddresseeId == userId)
            .Select(c => c.RequesterId)
            .ToListAsync(cancellationToken);

        return await LoadUsersAsync(db, ids, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetOutgoingRequestsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<int> ids = await db.Connections
            .Where(c => c.Status == ConnectionStatus.Pending && c.RequesterId == userId)
            .Select(c => c.AddresseeId)
            .ToListAsync(cancellationToken);

        return await LoadUsersAsync(db, ids, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, PeerStatus>> GetPeerStatusesAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<Connection> connections = await db.Connections
            .AsNoTracking()
            .Where(c => c.Status != ConnectionStatus.Declined && (c.RequesterId == userId || c.AddresseeId == userId))
            .ToListAsync(cancellationToken);


        return connections
            .GroupBy(c => c.OtherUserId(userId))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    Connection best = group.OrderByDescending(c => c.Status == ConnectionStatus.Accepted).First();
                    if (best.Status == ConnectionStatus.Accepted)
                    {
                        return PeerStatus.Connected;
                    }

                    return best.RequesterId == userId ? PeerStatus.RequestSent : PeerStatus.RequestReceived;
                });
    }

    public async Task<bool> AreConnectedAsync(int firstUserId, int secondUserId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await IsConnectedAsync(db, firstUserId, secondUserId, cancellationToken);
    }

    /// <summary>Shared helper also used by the message service.</summary>
    internal static Task<bool> IsConnectedAsync(StudyConnectDbContext db, int a, int b, CancellationToken cancellationToken) =>
        db.Connections.AnyAsync(
            c => c.Status == ConnectionStatus.Accepted
                 && ((c.RequesterId == a && c.AddresseeId == b) || (c.RequesterId == b && c.AddresseeId == a)),
            cancellationToken);

    private static Task<Connection?> FindBetweenAsync(StudyConnectDbContext db, int a, int b, CancellationToken cancellationToken) =>
        db.Connections.FirstOrDefaultAsync(
            c => (c.RequesterId == a && c.AddresseeId == b) || (c.RequesterId == b && c.AddresseeId == a),
            cancellationToken);

    private static async Task<IReadOnlyList<User>> LoadUsersAsync(StudyConnectDbContext db, List<int> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<User>();
        }

        return await db.Users
            .AsNoTracking()
            .Include(u => u.Interests)
            .Where(u => ids.Contains(u.Id))
            .OrderBy(u => u.Name)
            .ToListAsync(cancellationToken);
    }
}
