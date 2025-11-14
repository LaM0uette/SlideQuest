using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SlideQuest.Hubs;
using SlideQuest.Services;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Endpoints;

public static class DirectionEndpoints
{
    #region Statements

    private const string ApiRoute = "/direction";

    public static IEndpointRouteBuilder MapDirectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(ApiRoute, SwitchDirection);
        endpoints.MapPost("/reset", ResetPlayer);
        endpoints.MapPost("/gen", GenerateMap);

        return endpoints;
    }

    #endregion

    #region Methods

    private static IResult SwitchDirection([FromBody] Direction direction, DirectionBatcher batcher)
    {
        // On ne diffuse pas immédiatement, on ajoute un vote au batcher
        batcher.AddVote(direction);
        return Results.Accepted(ApiRoute, new { direction });
    }

    private static async Task<IResult> ResetPlayer(IHubContext<GameHub> hub)
    {
        await hub.Clients.All.SendAsync("Reset");
        return Results.Accepted("/reset");
    }

    private static async Task<IResult> GenerateMap(IHubContext<GameHub> hub)
    {
        await hub.Clients.All.SendAsync("Generate");
        return Results.Accepted("/gen");
    }

    #endregion
}
