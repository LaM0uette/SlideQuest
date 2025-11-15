using SlideQuest.Shared.Enums;

namespace GridGenerator;

public record Grid(
    int Width,
    int Height,
    int Seed,
    Cell[,] Cells,
    Cell Start,
    Cell End,
    List<Direction> MovesForWin
);