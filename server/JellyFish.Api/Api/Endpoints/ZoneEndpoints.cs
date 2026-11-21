using JellyFish.Api.Common;
using JellyFish.Api.Features.Zone.Commands;
using JellyFish.Api.Features.Zone.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class ZoneEndpoints
{
    public static IEndpointRouteBuilder MapZoneEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/zones").WithTags("Zones");

        group.MapGet("/", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new ListZonesQuery());
            return res.ToHttpResult();
        });

        group.MapGet("/{name}", async (string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new GetZoneQuery(name));
            return res.ToHttpResult();
        });

        group.MapPost("/", async (CreateZoneCommand body, IMediator mediator) =>
        {
            var res = await mediator.Send(body);
            return res.ToHttpResult();
        });

        group.MapPut("/{name}", async (string name, UpdateZoneCommand body, IMediator mediator) =>
        {
            var res = await mediator.Send(body with { Name = name });
            return res.ToHttpResult();
        });

        group.MapDelete("/{name}", async (string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new DeleteZoneCommand(name));
            return res.ToHttpResult();
        });

        return app;
    }
}

