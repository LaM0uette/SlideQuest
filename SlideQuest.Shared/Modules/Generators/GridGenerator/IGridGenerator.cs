namespace GridGenerator;

public interface IGridGenerator
{
    Grid Generate(int width, int height, int? seed = null);
}