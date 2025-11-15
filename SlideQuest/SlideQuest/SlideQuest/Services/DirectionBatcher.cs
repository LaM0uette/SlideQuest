using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SlideQuest.Hubs;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Services;

public sealed class DirectionBatcher : IDisposable
{
    #region Statements
    
    private const int FLUSH_INTERVAL_SECONDS = 2;

    private readonly IHubContext<GameHub> _hub;
    
    private readonly Timer _timer;
    private readonly ConcurrentDictionary<Direction, int> _counts = new();
    private readonly object _flushLock = new();
    private bool _disposed;

    private static readonly TimeSpan Period = TimeSpan.FromSeconds(FLUSH_INTERVAL_SECONDS);

    public DirectionBatcher(IHubContext<GameHub> hub)
    {
        _hub = hub;

        foreach (Direction direction in Enum.GetValues<Direction>())
        {
            _counts.TryAdd(direction, 0);
        }

        _timer = new Timer(async void (_) => await FlushAsync().ConfigureAwait(false), null, Period, Period);
    }

    #endregion

    #region Methods

    public void AddVote(Direction direction)
    {
        _counts.AddOrUpdate(direction, 1, (_, current) => checked(current + 1));
    }

    
    private async Task FlushAsync()
    {
        if (_disposed) 
            return;

        if (!Monitor.TryEnter(_flushLock))
            return;
        
        try
        {
            KeyValuePair<Direction, int>[] snapshot = _counts.ToArray();
            
            int total = snapshot.Sum(kv => kv.Value);
            if (total == 0)
                return;

            Direction selected = snapshot
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .First().Key;

            foreach (Direction key in _counts.Keys.ToArray())
            {
                _counts.AddOrUpdate(key, 0, (_, _) => 0);
            }

            await _hub.Clients.All.SendAsync("DirectionChanged", selected);
        }
        catch
        {
            // Ignore exceptions during flush
        }
        finally
        {
            Monitor.Exit(_flushLock);
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }

    #endregion
}
