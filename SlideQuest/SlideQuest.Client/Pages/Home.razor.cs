using Microsoft.AspNetCore.Components;
using SlideQuest.Client.Services;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Client.Pages;

public class HomePresenter : ComponentBase, IDisposable
{
    #region Statements

    protected Direction? Direction;
    
    [Inject] private IGameHubClient _gameHubClient { get; set; } = null!;

    protected override void OnInitialized()
    {
        _gameHubClient.DirectionChanged += OnDirectionChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        await _gameHubClient.EnsureConnectionAsync();
    }

    #endregion

    #region Methods

    private void OnDirectionChanged(Direction direction)
    {
        Direction = direction;
        
        Console.WriteLine($"[Home] DirectionChanged: {direction}");
        StateHasChanged();
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _gameHubClient.DirectionChanged -= OnDirectionChanged;
        
        GC.SuppressFinalize(this);
    }

    #endregion
}