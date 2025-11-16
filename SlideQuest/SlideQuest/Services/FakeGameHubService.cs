using SlideQuest.Client.Services;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Services;

// No-op implementation used on the server during prerendering to satisfy DI
public sealed class FakeGameHubService : IGameHubService
{
    public event Action<Direction>? DirectionChanged;
    public event Action? ResetRequested;
    public event Action<int?, string?>? GenerateRequested;

    public Task EnsureConnectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
