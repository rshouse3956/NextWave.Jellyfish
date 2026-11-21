using JellyFish.Api.Common;
using JellyFish.Api.Features.Folder;
using JellyFish.Api.Features.Folder.Commands;
using JellyFish.Api.Features.Folder.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class FolderEndpoints
{
    public static IEndpointRouteBuilder MapFolderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/folders").WithTags("Folders");

        group.MapGet("/", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new ListFoldersQuery());
            return res.ToHttpResult();
        });

        group.MapGet("/{name}", async (string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new GetFolderQuery(name));
            return res.ToHttpResult();
        });

        group.MapPost("/", async (CreateFolderCommand cmd, IMediator mediator) =>
        {
            var res = await mediator.Send(cmd);
            return res.ToHttpResult();
        });

        group.MapPut("/{name}", async (string name, UpdateFolderCommand body, IMediator mediator) =>
        {
            var res = await mediator.Send(body with { Name = name });
            return res.ToHttpResult();
        });

        group.MapDelete("/{name}", async (string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new DeleteFolderCommand(name));
            return res.ToHttpResult();
        });

        return app;
    }
}

