using SlideQuest.Shared.Enums;

namespace SlideQuest.Client.Services;

public interface IGameHubClient
{
    event Action<Direction>? DirectionChanged;
    
    Task EnsureConnectionAsync(CancellationToken cancellationToken = default);
    
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}