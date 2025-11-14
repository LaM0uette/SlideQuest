using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SlideQuest.Hubs;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Services;

/// <summary>
/// Agrège les directions reçues et émet la direction majoritaire toutes les N secondes via SignalR.
/// </summary>
public sealed class DirectionBatcher : IDisposable
{
    private readonly IHubContext<GameHub> _hub;
    private readonly System.Threading.Timer _timer;
    private readonly ConcurrentDictionary<Direction, int> _counts = new();
    private readonly object _flushLock = new();
    private bool _disposed;

    private static readonly TimeSpan Period = TimeSpan.FromSeconds(3);

    public DirectionBatcher(IHubContext<GameHub> hub)
    {
        _hub = hub;

        // Initialize counts for all enum values to avoid missing keys
        foreach (Direction dir in Enum.GetValues(typeof(Direction)))
        {
            _counts.TryAdd(dir, 0);
        }

        _timer = new System.Threading.Timer(async _ => await FlushAsync().ConfigureAwait(false), null, Period, Period);
    }

    public void AddVote(Direction direction)
    {
        _counts.AddOrUpdate(direction, 1, (_, current) => checked(current + 1));
    }

    private async Task FlushAsync()
    {
        if (_disposed) return;

        // Ensure only one flush at a time
        if (!System.Threading.Monitor.TryEnter(_flushLock))
            return;
        try
        {
            // Snapshot counts
            var snapshot = _counts.ToArray();

            int total = 0;
            foreach (var kv in snapshot)
                total += kv.Value;

            if (total == 0)
                return; // nothing to do

            // Majority by highest count, tie-breaker by enum order
            Direction selected = snapshot
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key) // enum order tie-break
                .First().Key;

            // Reset counts to zero for next window
            foreach (var key in _counts.Keys.ToArray())
            {
                _counts.AddOrUpdate(key, 0, (_, __) => 0);
            }

            // Broadcast once
            await _hub.Clients.All.SendAsync("DirectionChanged", selected);
        }
        catch
        {
            // Swallow to avoid timer termination; in real app log this
        }
        finally
        {
            System.Threading.Monitor.Exit(_flushLock);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }
}
