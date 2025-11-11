namespace GridGenerator;

public class Grid
{
    public int Width;
    public int Height;
    public CellType[,] Cells;
    public (int x, int y) Start;
    public (int x, int y) End;
    public List<string> Moves;

    public Grid(int width, int height, CellType[,] cells, (int x, int y) start, (int x, int y) end, List<string> moves)
    {
        Width = width;
        Height = height;
        Cells = cells;
        Start = start;
        End = end;
        Moves = moves;
    }
}