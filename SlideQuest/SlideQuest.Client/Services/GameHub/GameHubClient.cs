using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Client.Services;

public sealed class GameHubClient : IGameHubClient, IAsyncDisposable
{
    #region Statements

    public event Action<Direction>? DirectionChanged;
    public event Action? ResetRequested;
    public event Action? GenerateRequested;
    
    private readonly NavigationManager _navigationManager;
    
    private HubConnection? _connection;
    
    public GameHubClient(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    #endregion

    #region IGameHubClient

    public async Task EnsureConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            await StartAsync(cancellationToken);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
            return;

        string hubUrl = new Uri(new Uri(_navigationManager.BaseUri), "hubs/game").ToString();

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<Direction>("DirectionChanged", direction =>
        {
            DirectionChanged?.Invoke(direction);
        });

        _connection.On("Reset", () =>
        {
            ResetRequested?.Invoke();
        });

        _connection.On("Generate", () =>
        {
            GenerateRequested?.Invoke();
        });

        await _connection.StartAsync(cancellationToken);
        Console.WriteLine("[SignalR] Connected to GameHub");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            await _connection.StopAsync(cancellationToken);
            await _connection.DisposeAsync();
            _connection = null;
            Console.WriteLine("[SignalR] Disconnected from GameHub");
        }
    }

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    #endregion
}
