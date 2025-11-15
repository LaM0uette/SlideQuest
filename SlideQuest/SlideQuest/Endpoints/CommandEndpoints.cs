using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SlideQuest.Client.Services;
using SlideQuest.Hubs;
using SlideQuest.Services;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Endpoints;

public static class CommandEndpoints
{
    #region Statements
    
    public static IEndpointRouteBuilder MapDirectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/direction", SwitchDirection);
        endpoints.MapPost("/reset", Reset);
        endpoints.MapPost("/gen", Generate);

        return endpoints;
    }

    #endregion

    #region Methods

    private static IResult SwitchDirection([FromBody] Direction direction, DirectionBatcher batcher)
    {
        batcher.AddVote(direction);
        return Results.Accepted("/direction", new { direction });
    }

    private static async Task<IResult> Reset(IHubContext<GameHub, IGameHubClient> hub)
    {
        await hub.Clients.All.Reset();
        return Results.Accepted("/reset");
    }

    private static async Task<IResult> Generate(IHubContext<GameHub, IGameHubClient> hub)
    {
        await hub.Clients.All.Generate();
        return Results.Accepted("/gen");
    }

    #endregion
}
