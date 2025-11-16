using SlideQuest.Shared.Enums;

namespace SlideQuest.Client.Services;

public interface IGameHubService
{
    event Action<Direction>? DirectionChanged;
    event Action? ResetRequested;
    event Action<int?, string?>? GenerateRequested;
    
    Task EnsureConnectionAsync(CancellationToken cancellationToken = default);
    
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}