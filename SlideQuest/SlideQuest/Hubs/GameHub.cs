using Microsoft.AspNetCore.SignalR;
using SlideQuest.Client.Services;

namespace SlideQuest.Hubs;

public class GameHub : Hub<IGameHubClient>
{
}
