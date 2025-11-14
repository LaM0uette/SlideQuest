using Microsoft.AspNetCore.Mvc;
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

    #endregion
}
