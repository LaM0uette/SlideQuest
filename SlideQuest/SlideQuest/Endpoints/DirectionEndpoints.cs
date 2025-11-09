using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SlideQuest.Hubs;
using SlideQuest.Shared.Enums;

namespace SlideQuest.Endpoints;

public static class DirectionEndpoints
{
    #region Statements

    private const string ApiRoute = "/direction";

    public static IEndpointRouteBuilder MapDirectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(ApiRoute, SwitchDirection);

        return endpoints;
    }

    #endregion

    #region Methods

    private static async Task<IResult> SwitchDirection([FromBody] Direction direction, IHubContext<GameHub> hub)
    {
        // Broadcast to all connected clients via SignalR
        await hub.Clients.All.SendAsync("DirectionChanged", direction);
        return Results.Accepted(ApiRoute, new { direction });
    }

    #endregion
}
