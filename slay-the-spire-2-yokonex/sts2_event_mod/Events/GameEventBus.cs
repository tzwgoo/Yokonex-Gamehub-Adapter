using System.Collections.Concurrent;

namespace STS2Bridge.Events;

public sealed class GameEventBus
{
    private readonly ConcurrentQueue<GameEvent> _recentEvents = new();
    private readonly HashSet<string>? _whitelist;
    private readonly Func<string, bool>? _isEventEnabled;
    private readonly int _capacity;
    private long _recentVersion;
    private event Action<GameEvent>? EventPublished;

    public GameEventBus(int capacity = 200, IEnumerable<string>? whitelist = null, Func<string, bool>? isEventEnabled = null)
    {
        _capacity = Math.Max(1, capacity);
        _whitelist = whitelist is null ? null : new HashSet<string>(whitelist, StringComparer.OrdinalIgnoreCase);
        _isEventEnabled = isEventEnabled;
    }

    public IDisposable Subscribe(Action<GameEvent> handler)
    {
        EventPublished += handler;
        return new Subscription(() => EventPublished -= handler);
    }

    public long RecentVersion => Interlocked.Read(ref _recentVersion);

    public void Publish(GameEvent gameEvent)
    {
        if (_whitelist is not null && !_whitelist.Contains(gameEvent.Type))
        {
            return;
        }

        if (_isEventEnabled is not null && !_isEventEnabled(gameEvent.Type))
        {
            return;
        }

        _recentEvents.Enqueue(gameEvent);
        while (_recentEvents.Count > _capacity && _recentEvents.TryDequeue(out _))
        {
        }

        Interlocked.Increment(ref _recentVersion);

        EventPublished?.Invoke(gameEvent);
    }

    public IReadOnlyList<GameEvent> GetRecentEvents(int limit)
    {
        return _recentEvents.Reverse().Take(Math.Max(0, limit)).Reverse().ToArray();
    }

    public void ClearRecentEvents()
    {
        while (_recentEvents.TryDequeue(out _))
        {
        }

        Interlocked.Increment(ref _recentVersion);
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                unsubscribe();
            }
        }
    }
}
