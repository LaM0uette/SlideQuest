namespace GameConfig;

public interface IGameConfig
{
    int GetMinLimit(Difficulty difficulty);
    int GetMaxLimit(Difficulty difficulty);
    (int Min, int Max) GetLimits(Difficulty difficulty);
}