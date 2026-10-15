namespace StudyConnect.Infrastructure.Services;

/// <summary>Stores notifications in the database and pushes them to the user's open tabs in real time.</summary>
public sealed class NotificationService : INotificationService
{
    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;
    private readonly IRealtimeNotifier _realtime;

    public NotificationService(IDbContextFactory<StudyConnectDbContext> dbFactory, IRealtimeNotifier realtime)
    {
        _dbFactory = dbFactory;
        _realtime = realtime;
    }

    public async Task NotifyAsync(int userId, NotificationType type, string title, string body, string? link = null, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title.Truncate(150),
            Body = body.Truncate(500),
            Link = link
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        await _realtime.PublishAsync(userId, new RealtimeEvent(RealtimeEventType.Notification, EntityId: notification.Id, Text: notification.Title));
    }

    public async Task<IReadOnlyList<Notification>> GetRecentAsync(int userId, int take = 15, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), cancellationToken);
    }
    public async Task<bool> DismissAsync(int userId, int notificationId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        int deleted = await db.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted > 0;
    }
}
