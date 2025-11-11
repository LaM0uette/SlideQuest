using GridGenerator;

namespace SlideQuest.Services;

public class FakeGridGenerator : IGridGenerator
{
    public Grid Generate(int width, int height, int? seed = null)
    {
        throw new NotImplementedException();
    }
}