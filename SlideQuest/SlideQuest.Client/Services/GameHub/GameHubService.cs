using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Client.Services;

public sealed class GameHubService : IGameHubService, IGameHubClient, IAsyncDisposable
{
    #region Statements

    public event Action<Direction>? DirectionChanged;
    public event Action? ResetRequested;
    public event Action? GenerateRequested;
    
    private readonly NavigationManager _navigationManager;
    
    private HubConnection? _connection;
    
    public GameHubService(NavigationManager navigationManager)
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

        Uri baseUri = new(_navigationManager.BaseUri);
        string hubUrl = new Uri(baseUri, "hubs/game").ToString();

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<Direction>(nameof(IGameHubClient.SwitchDirection), SwitchDirection);
        _connection.On(nameof(IGameHubClient.Reset), Reset);
        _connection.On(nameof(IGameHubClient.Generate), Generate);

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

    
    public Task SwitchDirection(Direction direction)
    {
        DirectionChanged?.Invoke(direction);
        return Task.CompletedTask;
    }
    
    public Task Reset()
    {
        ResetRequested?.Invoke();
        return Task.CompletedTask;
    }

    public Task Generate()
    {
        GenerateRequested?.Invoke();
        return Task.CompletedTask;
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
