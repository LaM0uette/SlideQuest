using SlideQuest.Shared.Enums;

namespace GridGenerator;

public class Grid
{
    public readonly int Width;
    public readonly int Height;
    public readonly CellType[,] Cells;
    
    public readonly Cell Start;
    public readonly Cell End;
    public readonly List<Direction> Moves;

    public Grid(int width, int height, CellType[,] cells, Cell start, Cell end, List<Direction> moves)
    {
        Width = width;
        Height = height;
        Cells = cells;
        Start = start;
        End = end;
        Moves = moves;
    }
}