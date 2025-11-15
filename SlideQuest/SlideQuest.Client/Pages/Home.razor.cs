using Microsoft.AspNetCore.Components;
using SlideQuest.Client.Services;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Client.Pages;

public class HomePresenter : ComponentBase, IDisposable
{
    #region Statements

    protected Direction? Direction;
    
    [Inject] private IGameHubService _gameHubService { get; set; } = null!;

    protected override void OnInitialized()
    {
        _gameHubService.DirectionChanged += OnDirectionChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        await _gameHubService.EnsureConnectionAsync();
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
        _gameHubService.DirectionChanged -= OnDirectionChanged;
        
        GC.SuppressFinalize(this);
    }

    #endregion
}