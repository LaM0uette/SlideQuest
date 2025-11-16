using GameConfig;
using GridGenerator;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SlideQuest.Client.Services;
using SlideQuest.Shared.Enums;
using Components.GameLogger;

namespace SlideQuest.Client.Pages;

public class PuzzlePresenter : ComponentBase, IDisposable
{
    #region Statements
    
    protected Difficulty CurrentDifficulty = Difficulty.Easy;

    protected ElementReference GridRef;
    
    protected Grid? Grid;
    protected Cell? PlayerCell;
    
    protected GameLoggerPresenter? Logger;
    
    [Inject] private IGameHubService _gameHubService { get; set; } = null!;
    [Inject] private IGridGenerator _gridGenerator { get; set; } = null!;
    [Inject] private IGameConfig _gameConfig { get; set; } = null!;
    
    private int _minGridLimit;
    private int _maxGridLimit;
    
    private bool _shouldFocusGrid; // TODO: listen key on top to remove focus issues
    
    protected override void OnInitialized()
    {
        _minGridLimit = _gameConfig.GetMinLimit(CurrentDifficulty);
        _maxGridLimit = _gameConfig.GetMaxLimit(CurrentDifficulty);
        
        _shouldFocusGrid = true;

        _gameHubService.GenerateRequested += OnGenerateRequested;
        _gameHubService.ResetRequested += OnResetRequested;
        _gameHubService.DirectionChanged += OnDirectionChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        await _gameHubService.EnsureConnectionAsync();
    }
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldFocusGrid)
        {
            _shouldFocusGrid = false;
            
            try
            {
                await GridRef.FocusAsync();
            }
            catch
            {
                // Ignore focus errors (e.g., element not rendered yet)
            }
        }
        
        await base.OnAfterRenderAsync(firstRender);
    }

    

    
    protected string? _seedInput; // TODO: remove this

    #endregion

    #region Methods
    
    protected void OnDifficultyChanged(ChangeEventArgs eventArgs)
    {
        if (eventArgs.Value is null) 
            return;

        if (!Enum.TryParse(typeof(Difficulty), eventArgs.Value.ToString(), out object? value) || value is not Difficulty difficulty) 
            return;
        
        CurrentDifficulty = difficulty;
        
        _minGridLimit = _gameConfig.GetMinLimit(CurrentDifficulty);
        _maxGridLimit = _gameConfig.GetMaxLimit(CurrentDifficulty);
        
        StateHasChanged();
    }
    
    protected void OnKeyDown(KeyboardEventArgs eventArgs)
    {
        if (Grid is null) 
            return;
        
        string key = eventArgs.Key.ToLowerInvariant();
        
        switch (key)
        {
            case "arrowup": case "w": case "z": Slide(0, -1); break;
            case "arrowdown": case "s": Slide(0, 1); break;
            case "arrowleft": case "a": case "q": Slide(-1, 0); break;
            case "arrowright": case "d": Slide(1, 0); break;
        }
    }

    protected void SlideButton(string dir)
    {
        if (Grid is null) 
            return;
        
        switch (dir)
        {
            case "U": Slide(0, -1); break;
            case "D": Slide(0, 1); break;
            case "L": Slide(-1, 0); break;
            case "R": Slide(1, 0); break;
        }
        
        _shouldFocusGrid = true;
    }
    
    
    private void OnGenerateRequested(int? difficultyCode, string? seed)
    {
        if (difficultyCode.HasValue)
        {
            CurrentDifficulty = difficultyCode.Value switch
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
            CurrentDifficulty = Difficulty.Normal; // default when not provided
        }
        
        _minGridLimit = _gameConfig.GetMinLimit(CurrentDifficulty);
        _maxGridLimit = _gameConfig.GetMaxLimit(CurrentDifficulty);
        
        _seedInput = seed; // store the provided seed string; Generate will parse/hash it
        
        Logger?.Log($"> hub: generate ({CurrentDifficulty}) seed={(seed ?? "").ToString()}");
        Generate(seed);
        
        _shouldFocusGrid = true;
    }
    
    private void OnResetRequested()
    {
        if (Grid is null) 
            return;
        
        PlayerCell = Grid.Start;
        _shouldFocusGrid = true;
        
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
    
    //TODO: make this private after add !gen command with difficulty/seed params
    protected void Generate(string? seedOverride = null)
    {
        int? parsedSeed = null;
        string? seedSource = seedOverride ?? _seedInput;
        if (!string.IsNullOrWhiteSpace(seedSource))
        {
            if (int.TryParse(seedSource, out int parsed))
            {
                parsedSeed = parsed;
            }
            else
            {
                // Hash string to a stable int
                parsedSeed = GetStableIntFromString(seedSource);
            }
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
        _shouldFocusGrid = true;
        
        StateHasChanged();
    }

    private static int GetStableIntFromString(string input)
    {
        // Simple FNV-1a 32-bit hash to int
        unchecked
        {
            const uint fnvOffset = 2166136261;
            const uint fnvPrime = 16777619;
            uint hash = fnvOffset;
            foreach (char c in input)
            {
                hash ^= c;
                hash *= fnvPrime;
            }
            return (int)hash;
        }
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
        
        GC.SuppressFinalize(this);
    }

    #endregion
}
