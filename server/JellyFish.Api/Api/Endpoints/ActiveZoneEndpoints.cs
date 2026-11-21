using JellyFish.Api.Common;
using JellyFish.Api.Features.ActiveZone.Commands;
using JellyFish.Api.Features.ActiveZone.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class ActiveZoneEndpoints
{
    public static IEndpointRouteBuilder MapActiveZoneEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/active-zones").WithTags("ActiveZones");

        group.MapGet("/", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new GetActiveZonesQuery());
            return res.ToHttpResult();
        });

        group.MapPost("/{zoneName}/stop", async (string zoneName, IMediator mediator) =>
        {
            var res = await mediator.Send(new StopActiveZoneCommand(zoneName));
            return res.ToHttpResult();
        });

        return app;
    }
}

