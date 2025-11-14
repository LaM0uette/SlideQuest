using SlideQuest.Shared.Enums;

namespace GridGenerator;

public class Grid
{
    public readonly int Width;
    public readonly int Height;
    public readonly int Seed;
    
    public readonly Cell[,] Cells;
    public readonly Cell Start;
    public readonly Cell End;
    
    public readonly List<Direction> MovesForWin;

    public Grid(int width, int height, int seed, Cell[,] cells, Cell start, Cell end, List<Direction> movesForWin)
    {
        Width = width;
        Height = height;
        Seed = seed;
        
        Cells = cells;
        Start = start;
        End = end;
        
        MovesForWin = movesForWin;
    }
}