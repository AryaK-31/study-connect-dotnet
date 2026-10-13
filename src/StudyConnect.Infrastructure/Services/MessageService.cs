using StudyConnect.Core.Validation;

namespace StudyConnect.Infrastructure.Services;

/// <summary>
/// Direct messages between connected students (port of the Socket.io "send_message" / "mark_read"
/// handlers and the messages / conversations / deleteConversation GraphQL resolvers).
/// </summary>
public sealed class MessageService : IMessageService
{
    private readonly IDbContextFactory<StudyConnectDbContext> _dbFactory;
    private readonly IRealtimeNotifier _realtime;

    public MessageService(IDbContextFactory<StudyConnectDbContext> dbFactory, IRealtimeNotifier realtime)
    {
        _dbFactory = dbFactory;
        _realtime = realtime;
    }

    public async Task<Result<DirectMessage>> SendAsync(int senderId, int recipientId, string text, CancellationToken cancellationToken = default)
    {
        string trimmed = (text ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return Result<DirectMessage>.Failure("Message cannot be empty.");
        }

        if (trimmed.Length > InputValidators.MaxMessageLength)
        {
            return Result<DirectMessage>.Failure($"Messages are limited to {InputValidators.MaxMessageLength} characters.");
        }

        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Only connected users may message each other (same rule as the original app).
        if (!await ConnectionService.IsConnectedAsync(db, senderId, recipientId, cancellationToken))
        {
            return Result<DirectMessage>.Failure("You must be connected to message this user.");
        }

        var message = new DirectMessage
        {
            SenderId = senderId,
            RecipientId = recipientId,
            ConversationKey = ConversationKey.For(senderId, recipientId),
            Text = trimmed
        };

        db.DirectMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        // Deliver to both participants so every open tab updates.
        await _realtime.PublishAsync(
            new[] { senderId, recipientId },
            new RealtimeEvent(RealtimeEventType.DirectMessage, senderId, message.Id, trimmed.Truncate(80)));

        return Result<DirectMessage>.Success(message);
    }

    public async Task<IReadOnlyList<DirectMessage>> GetConversationAsync(int userId, int otherUserId, int take = 100, CancellationToken cancellationToken = default)
    {
        string key = ConversationKey.For(userId, otherUserId);
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<DirectMessage> latest = await db.DirectMessages
            .AsNoTracking()
            .Where(m => m.ConversationKey == key)
            .OrderByDescending(m => m.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        latest.Reverse(); // oldest first for display
        return latest;
    }

    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Lightweight projection into an anonymous type.
        var messages = await db.DirectMessages
            .AsNoTracking()
            .Where(m => m.SenderId == userId || m.RecipientId == userId)
            .OrderByDescending(m => m.Id)
            .Select(m => new { m.ConversationKey, m.SenderId, m.RecipientId, m.Text, m.CreatedAtUtc, m.IsRead })
            .ToListAsync(cancellationToken);

        List<int> connectedIds = await db.Connections
            .Where(c => c.Status == ConnectionStatus.Accepted && (c.RequesterId == userId || c.AddresseeId == userId))
            .Select(c => c.RequesterId == userId ? c.AddresseeId : c.RequesterId)
            .ToListAsync(cancellationToken);

        // Group messages per conversation, keeping the latest message and the unread count.
        var conversations = messages
            .GroupBy(m => m.ConversationKey)
            .Select(group =>
            {
                var last = group.First();
                return new
                {
                    OtherUserId = last.SenderId == userId ? last.RecipientId : last.SenderId,
                    Last = last,
                    Unread = group.Count(m => m.RecipientId == userId && !m.IsRead)
                };
            })
            .ToList();

        List<int> otherIds = conversations.Select(c => c.OtherUserId).Concat(connectedIds).Distinct().ToList();
        Dictionary<int, string> names = await db.Users
            .Where(u => otherIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var connectedSet = connectedIds.ToHashSet();

        List<ConversationSummary> summaries = conversations
            .Where(c => names.ContainsKey(c.OtherUserId))
            .Select(c => new ConversationSummary(
                c.OtherUserId,
                names[c.OtherUserId],
                c.Last.Text,
                c.Last.CreatedAtUtc,
                c.Last.SenderId == userId,
                c.Unread,
                connectedSet.Contains(c.OtherUserId)))
            .ToList();

        // Connections without any messages yet are listed too, so a chat can be started.
        HashSet<int> withMessages = summaries.Select(s => s.OtherUserId).ToHashSet();
        summaries.AddRange(connectedIds
            .Where(id => !withMessages.Contains(id) && names.ContainsKey(id))
            .Select(id => new ConversationSummary(id, names[id], null, null, false, 0, true))
            .OrderBy(s => s.OtherUserName));

        return summaries;
    }

    public async Task<int> MarkConversationReadAsync(int userId, int otherUserId, CancellationToken cancellationToken = default)
    {
        string key = ConversationKey.For(userId, otherUserId);
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        int updated = await db.DirectMessages
            .Where(m => m.ConversationKey == key && m.RecipientId == userId && !m.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.IsRead, true), cancellationToken);

        if (updated > 0)
        {
            // Tell the sender their messages were read, and refresh the reader's unread badge.
            await _realtime.PublishAsync(new[] { otherUserId, userId }, new RealtimeEvent(RealtimeEventType.MessagesRead, userId));
        }

        return updated;
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.DirectMessages.CountAsync(m => m.RecipientId == userId && !m.IsRead, cancellationToken);
    }

    public async Task<OperationResult> DeleteConversationAsync(int userId, int otherUserId, CancellationToken cancellationToken = default)
    {
        string key = ConversationKey.For(userId, otherUserId);
        await using StudyConnectDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        int deleted = await db.DirectMessages.Where(m => m.ConversationKey == key).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            return OperationResult.Failure("There are no messages to delete.");
        }

        await _realtime.PublishAsync(new[] { userId, otherUserId }, new RealtimeEvent(RealtimeEventType.MessagesRead, userId));
        return OperationResult.Success();
    }
}
