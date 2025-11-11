namespace GridGenerator;

public class Grid
{
    public int Width;
    public int Height;
    public CellType[,] Cells;
    public Cell Start;
    public Cell End;
    public List<string> Moves;

    public Grid(int width, int height, CellType[,] cells, Cell start, Cell end, List<string> moves)
    {
        Width = width;
        Height = height;
        Cells = cells;
        Start = start;
        End = end;
        Moves = moves;
    }
}