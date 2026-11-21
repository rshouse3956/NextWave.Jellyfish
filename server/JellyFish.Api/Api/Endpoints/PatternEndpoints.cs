using JellyFish.Api.Common;
using JellyFish.Api.Features.Pattern.Commands;
using JellyFish.Api.Features.Pattern.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class PatternEndpoints
{
    public static IEndpointRouteBuilder MapPatternEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/folders/{folderName}/patterns").WithTags("Patterns");

        group.MapGet("/", async (string folderName, IMediator mediator) =>
        {
            var res = await mediator.Send(new ListPatternsByFolderQuery(folderName));
            return res.ToHttpResult();
        });

        group.MapGet("/{name}", async (string folderName, string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new GetPatternQuery(folderName, name));
            return res.ToHttpResult();
        });

        group.MapPost("/", async (string folderName, CreatePatternCommand body, IMediator mediator) =>
        {
            var res = await mediator.Send(body with { FolderName = folderName });
            return res.ToHttpResult();
        });

        group.MapPut("/{name}", async (string folderName, string name, UpdatePatternCommand body, IMediator mediator) =>
        {
            var res = await mediator.Send(body with { FolderName = folderName, Name = name });
            return res.ToHttpResult();
        });

        group.MapDelete("/{name}", async (string folderName, string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new DeletePatternCommand(folderName, name));
            return res.ToHttpResult();
        });

        group.MapPut("/{name}/details", async (string folderName, string name, UpsertPatternDetailCommand body, IMediator mediator) =>
        {
            var res = await mediator.Send(body with { FolderName = folderName, Name = name });
            return res.ToHttpResult();
        });

        group.MapDelete("/{name}/details", async (string folderName, string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new DeletePatternDetailCommand(folderName, name));
            return res.ToHttpResult();
        });

        return app;
    }
}

