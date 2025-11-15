using GameConfig;

namespace SlideQuest.Services;

public class FakeGameConfig : IGameConfig
{
    public int GetMinLimit(Difficulty difficulty)
    {
        return 0;
    }

    public int GetMaxLimit(Difficulty difficulty)
    {
        return 0;
    }

    public (int Min, int Max) GetLimits(Difficulty difficulty)
    {
        return (0, 0);
    }
}