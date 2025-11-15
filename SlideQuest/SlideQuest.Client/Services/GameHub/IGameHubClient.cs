using SlideQuest.Shared.Enums;

namespace SlideQuest.Client.Services;

public interface IGameHubClient
{
    Task SwitchDirection(Direction direction);
    Task Reset();
    Task Generate();
}
