using Microsoft.Extensions.Logging;
using StudyConnect.Core.Abstractions;
using StudyConnect.Core.Models;

namespace StudyConnect.Infrastructure.Realtime;

/// <summary>
/// Thread-safe in-process publish/subscribe hub (registered as a singleton).
/// Every open Blazor page subscribes with a <see cref="RealtimeEventHandler"/> delegate; when a
/// service publishes an event for a user, the delegates of all that user's tabs are invoked and
/// the components re-render over their SignalR connection.
/// </summary>
public sealed class InMemoryRealtimeNotifier : IRealtimeNotifier
{
    private readonly object _gate = new();
    private readonly Dictionary<int, List<RealtimeEventHandler>> _subscribers = new();
    private readonly ILogger<InMemoryRealtimeNotifier> _logger;

    public InMemoryRealtimeNotifier(ILogger<InMemoryRealtimeNotifier> logger)
    {
        _logger = logger;
    }

    public int OnlineCount
    {
        get
        {
            lock (_gate)
            {
                return _subscribers.Count;
            }
        }
    }

    public IDisposable Subscribe(int userId, RealtimeEventHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            if (!_subscribers.TryGetValue(userId, out List<RealtimeEventHandler>? handlers))
            {
                handlers = new List<RealtimeEventHandler>();
                _subscribers[userId] = handlers;
            }

            handlers.Add(handler);
        }

        return new Subscription(() => Unsubscribe(userId, handler));
    }

    public Task PublishAsync(int userId, RealtimeEvent realtimeEvent) =>
        PublishAsync(new[] { userId }, realtimeEvent);

    public async Task PublishAsync(IEnumerable<int> userIds, RealtimeEvent realtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        ArgumentNullException.ThrowIfNull(realtimeEvent);

        List<RealtimeEventHandler> targets;
        lock (_gate)
        {
            // Copy the handlers while holding the lock, invoke them after releasing it.
            targets = userIds
                .Distinct()
                .Where(_subscribers.ContainsKey)
                .SelectMany(id => _subscribers[id])
                .ToList();
        }

        IEnumerable<Task> deliveries = targets.Select(handler => InvokeSafelyAsync(handler, realtimeEvent));
        await Task.WhenAll(deliveries);
    }

    public bool IsOnline(int userId)
    {
        lock (_gate)
        {
            return _subscribers.ContainsKey(userId);
        }
    }

    private async Task InvokeSafelyAsync(RealtimeEventHandler handler, RealtimeEvent realtimeEvent)
    {
        try
        {
            await handler(realtimeEvent);
        }
        catch (Exception ex)
        {
            // A closed browser tab must never break delivery to the others.
            _logger.LogDebug(ex, "Real-time handler failed for event {Type}", realtimeEvent.Type);
        }
    }

    private void Unsubscribe(int userId, RealtimeEventHandler handler)
    {
        lock (_gate)
        {
            if (_subscribers.TryGetValue(userId, out List<RealtimeEventHandler>? handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _subscribers.Remove(userId);
                }
            }
        }
    }

    /// <summary>Disposable token returned by <see cref="Subscribe"/>; disposing it unsubscribes once.</summary>
    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
