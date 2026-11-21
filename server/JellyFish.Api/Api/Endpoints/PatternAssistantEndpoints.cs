using JellyFish.Api.Common;
using JellyFish.Api.Features.PatternAssistant.Commands;
using JellyFish.Api.Features.PatternAssistant.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class PatternAssistantEndpoints
{
    public static IEndpointRouteBuilder MapPatternAssistantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pattern-assistant").WithTags("PatternAssistant");

        // Whether the assistant can be used (enabled, Ollama reachable, model pulled). Always 200.
        group.MapGet("/status", async (IMediator mediator, CancellationToken ct) =>
        {
            var res = await mediator.Send(new GetPatternAssistantStatusQuery(), ct);
            return res.ToHttpResult();
        });

        // Generate (or refine, with currentDetails) a pattern. Nothing is run or saved.
        group.MapPost("/generate", async (GeneratePatternCommand body, IMediator mediator, CancellationToken ct) =>
        {
            var res = await mediator.Send(body, ct);
            return res.ToHttpResult();
        });

        return app;
    }
}
