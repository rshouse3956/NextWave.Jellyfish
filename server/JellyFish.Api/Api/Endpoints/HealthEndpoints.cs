using JellyFish.Api.Common;
using JellyFish.Api.Features.Health;
using JellyFish.Api.Features.Health.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetHealthQuery());
            return result.ToHttpResult();
        })
        .WithName("GetHealth")
        .WithOpenApi();

        return app;
    }
}

