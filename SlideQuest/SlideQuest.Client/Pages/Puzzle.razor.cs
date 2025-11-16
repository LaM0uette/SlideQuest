using GameConfig;
using GridGenerator;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SlideQuest.Client.Services;
using SlideQuest.Shared.Enums;
using Components.GameLogger;

namespace SlideQuest.Client.Pages;

public class PuzzlePresenter : ComponentBase, IDisposable, IAsyncDisposable
{
    #region Statements
    
    protected Grid? Grid;
    protected Cell? PlayerCell;
    
    protected GameLoggerPresenter? Logger;
    
    [Inject] private IJSRuntime _jsRuntime { get; set; } = null!;
    [Inject] private IGameHubService _gameHubService { get; set; } = null!;
    [Inject] private IGridGenerator _gridGenerator { get; set; } = null!;
    [Inject] private IGameConfig _gameConfig { get; set; } = null!;
    
    private Difficulty _currentDifficulty = Difficulty.Normal;
    private int _minGridLimit;
    private int _maxGridLimit;
    
    protected override void OnInitialized()
    {
        _minGridLimit = _gameConfig.GetMinLimit(_currentDifficulty);
        _maxGridLimit = _gameConfig.GetMaxLimit(_currentDifficulty);
        
        _gameHubService.GenerateRequested += OnGenerateRequested;
        _gameHubService.ResetRequested += OnResetRequested;
        _gameHubService.DirectionChanged += OnDirectionChanged;
        
        Generate(); // TODO: remove this
    }

    protected override async Task OnInitializedAsync()
    {
        await _gameHubService.EnsureConnectionAsync();
    }

    private IJSObjectReference? _puzzleKeysModule;
    private DotNetObjectReference<PuzzlePresenter>? _puzzlePresenterRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                _puzzleKeysModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "/js/puzzleKeys.js");
                _puzzlePresenterRef = DotNetObjectReference.Create(this);
                await _puzzleKeysModule.InvokeVoidAsync("register", _puzzlePresenterRef);
            }
            catch
            {
                // ignore JS init errors
            }
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    #region Methods

    [JSInvokable]
    public void OnWindowKeyDown(string key, string code)
    {
        KeyboardEventArgs args = new()
        {
            Key = key,
            Code = code
        };
        
        OnKeyDown(args);
    }
    
    
    private void OnGenerateRequested(int? difficultyCode, string? seed)
    {
        if (difficultyCode.HasValue)
        {
            _currentDifficulty = difficultyCode.Value switch
            {
                1 => Difficulty.Easy,
                2 => Difficulty.Normal,
                3 => Difficulty.Hard,
                4 => Difficulty.Expert,
                _ => Difficulty.Normal
            };
        }
        else
        {
            _currentDifficulty = Difficulty.Normal; // default when not provided
        }
        
        _minGridLimit = _gameConfig.GetMinLimit(_currentDifficulty);
        _maxGridLimit = _gameConfig.GetMaxLimit(_currentDifficulty);
        
        Logger?.Log($"> hub: generate ({_currentDifficulty}) seed={seed ?? ""}");
        Generate(seed);

    }
    
    private void OnResetRequested()
    {
        if (Grid is null) 
            return;
        
        PlayerCell = Grid.Start;
        
        Logger?.Log("> hub: reset");
        StateHasChanged();
    }

    private void OnDirectionChanged(Direction direction)
    {
        switch (direction)
        {
            case Direction.Top: Slide(0, -1);
                break;
            
            case Direction.Bottom: Slide(0, 1);
                break;
            
            case Direction.Left: Slide(-1, 0);
                break;
            
            case Direction.Right: Slide(1, 0);
                break;
            
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }

        Logger?.Log($"> hub: {direction}");
    }
    
    private void OnKeyDown(KeyboardEventArgs eventArgs)
    {
        string key = eventArgs.Key.ToLowerInvariant();
        string code = eventArgs.Code.ToLowerInvariant();
        
        if (key == " " || key == "space" || key == "spacebar" || code == "space")
        {
            Generate();
            return;
        }
        
        if (Grid is null)
            return;
        
        switch (key)
        {
            case "arrowup": case "w": case "z": Slide(0, -1); break;
            case "arrowdown": case "s": Slide(0, 1); break;
            case "arrowleft": case "a": case "q": Slide(-1, 0); break;
            case "arrowright": case "d": Slide(1, 0); break;
        }
    }
    
    private void Slide(int directionX, int directionY)
    {
        if (directionX == 0 && directionY == 0) 
            return;
        
        (int playerX, int playerY, _) = PlayerCell ?? throw new InvalidOperationException("PlayerCell is null");

        while (true)
        {
            int nextPlayerX = playerX + directionX;
            int nextPlayerY = playerY + directionY;
            
            if (!InGrid(nextPlayerX, nextPlayerY) || IsBlocked(nextPlayerX, nextPlayerY))
                break;
            
            playerX = nextPlayerX; 
            playerY = nextPlayerY;
        }

        PlayerCell = new Cell(playerX, playerY);
        
        if (PlayerCell == Grid?.End)
        {
            Logger?.Log(">>> Puzzle completed! <<<");
            Generate();
        }
        
        StateHasChanged();
    }
    
    private void Generate(string? seed = null)
    {
        int? parsedSeed = null;
        if (!string.IsNullOrWhiteSpace(seed))
        {
            parsedSeed = int.TryParse(seed, out int parsed) ? parsed : null;
        }

        int seedToUse = parsedSeed ?? Random.Shared.Next(int.MinValue, int.MaxValue);

        Random random = new(seedToUse);
        int width = random.Next(_minGridLimit, _maxGridLimit + 1);
        int height = random.Next(_minGridLimit, _maxGridLimit + 1);

        const int maxAttempts = 5;
        Grid? generated = null;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                generated = _gridGenerator.Generate(width, height, seedToUse);

                bool hasMoves = generated.MovesForWin.Count > 0;
                bool hasPathOrObstacle = false;
                
                for (int y = 0; y < generated.Height && !hasPathOrObstacle; y++)
                {
                    for (int x = 0; x < generated.Width && !hasPathOrObstacle; x++)
                    {
                        CellType cellType = generated.Cells[x, y].Type;
                        
                        if (cellType is CellType.Path or CellType.Obstacle)
                        {
                            hasPathOrObstacle = true;
                        }
                    }
                }

                if (hasMoves && hasPathOrObstacle)
                    break;

                generated = null;
            }
            catch (Exception)
            {
                generated = null;
            }
        }

        if (generated is null)
        {
            try
            {
                if (parsedSeed.HasValue)
                {
                    generated = _gridGenerator.Generate(width, height, seedToUse);
                }
                else
                {
                    int altSeed = Random.Shared.Next(int.MinValue, int.MaxValue);
                    Random altRandom = new(altSeed);
                    
                    int altW = altRandom.Next(_minGridLimit, _maxGridLimit + 1);
                    int altH = altRandom.Next(_minGridLimit, _maxGridLimit + 1);
                    generated = _gridGenerator.Generate(altW, altH, altSeed);
                }
            }
            catch
            {
                // Ignore exceptions on last attempt
            }
        }

        if (generated is null)
            return;

        Grid = generated;
        
        PlayerCell = Grid.Start;
        
        StateHasChanged();
    }
    
    private bool InGrid(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Grid?.Width && y < Grid?.Height;
    }

    private bool IsBlocked(int x, int y)
    {
        if (Grid is null) 
            return true;
        
        CellType cell = Grid.Cells[x, y].Type;
        return cell == CellType.Obstacle;
    }
    
    #endregion

    #region IDisposable

    public void Dispose()
    {
        _gameHubService.DirectionChanged -= OnDirectionChanged;
        _gameHubService.ResetRequested -= OnResetRequested;
        _gameHubService.GenerateRequested -= OnGenerateRequested;
        
        _puzzlePresenterRef?.Dispose();
        _puzzlePresenterRef = null;
        
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_puzzleKeysModule is not null)
            {
                try { await _puzzleKeysModule.InvokeVoidAsync("unregister"); } catch { /* ignore */ }
                try { await _puzzleKeysModule.DisposeAsync(); } catch { /* ignore */ }
            }
        }
        finally
        {
            _puzzleKeysModule = null;
        }
        
        GC.SuppressFinalize(this);
    }

    #endregion
}
