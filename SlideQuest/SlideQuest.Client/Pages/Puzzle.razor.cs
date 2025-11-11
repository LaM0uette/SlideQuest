using GridGenerator;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace SlideQuest.Client.Pages;

public class PuzzlePresenter : ComponentBase
{
    [Inject] protected IGridGenerator _gridGenerator { get; set; } = null!;

    protected readonly int[] _sizes = { 8, 16, 32 };
    protected int _selectedSize = 8;

    protected int _width;
    protected int _height;
    protected CellType[,]? _grid;
    protected (int x, int y) _start;
    protected (int x, int y) _end;
    protected (int x, int y) _player;
    protected bool _won;
    protected List<string> _moves = new();

    protected override void OnInitialized()
    {
        _width = _selectedSize;
        _height = _selectedSize;
    }

    protected void Generate()
    {
        _width = _selectedSize;
        _height = _selectedSize;
        var generated = _gridGenerator.Generate(_width, _height);
        _grid = generated.Cells;
        _start = generated.Start;
        _end = generated.End;
        _moves = generated.Moves;
        _player = _start;
        _won = false;
        StateHasChanged();
    }

    // All generation logic has been extracted to GridGenerator. Only keep player interactions below.

    private bool InGrid(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height;

    // --- Interactive sliding logic ---
    protected void OnKeyDown(KeyboardEventArgs e)
    {
        if (_grid is null || _won) return;
        var key = e.Key?.ToLowerInvariant();
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
        if (_grid is null || _won) return;
        switch (dir)
        {
            case "U": Slide(0, -1); break;
            case "D": Slide(0, 1); break;
            case "L": Slide(-1, 0); break;
            case "R": Slide(1, 0); break;
        }
    }

    protected void Slide(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;
        var (x, y) = _player;

        // slide until next cell would be blocked or out of grid
        while (true)
        {
            int nx = x + dx;
            int ny = y + dy;
            if (!InGrid(nx, ny) || IsBlocked(nx, ny))
                break;
            x = nx; y = ny;
        }

        _player = (x, y);
        if (_player == _end) _won = true;
        StateHasChanged();
    }

    protected bool IsBlocked(int x, int y)
    {
        if (_grid is null) return true;
        var cell = _grid[x, y];
        // Only obstacles and borders block sliding; start/end/path/empty are slideable
        return cell == CellType.Obstacle;
    }
}
