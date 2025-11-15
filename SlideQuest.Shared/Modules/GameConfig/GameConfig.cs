namespace GameConfig;

public class GameConfig : IGameConfig
{
    #region Statements

    private readonly Dictionary<Difficulty, (int Min, int Max)> _difficultyLimits = new()
    {
        { Difficulty.Easy, (5, 8) },
        { Difficulty.Normal, (10, 16) },
        { Difficulty.Hard, (16, 24) },
        { Difficulty.Expert, (24, 32) }
    };

    #endregion

    #region Methods

    public int GetMinLimit(Difficulty difficulty)
    {
        return _difficultyLimits[difficulty].Min;
    }

    public int GetMaxLimit(Difficulty difficulty)
    {
        return _difficultyLimits[difficulty].Max;
    }
    
    public (int Min, int Max) GetLimits(Difficulty difficulty)
    {
        return _difficultyLimits[difficulty];
    }

    #endregion
}