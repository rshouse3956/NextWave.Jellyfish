using JellyFish.Api.Common;
using JellyFish.Api.Features.JellyfishController.Commands;
using JellyFish.Api.Features.JellyfishController.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class JellyfishControllerEndpoints
{
    public static IEndpointRouteBuilder MapJellyfishControllerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jellyfish-controller").WithTags("JellyfishController");

        group.MapGet("/folders", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new GetFoldersQuery());
            return res.ToHttpResult();
        });

        group.MapGet("/patterns", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new GetPatternsQuery());
            return res.ToHttpResult();
        });

        group.MapGet("/folders/{folderName}/patterns/{name}", async (string folderName, string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new GetPatternDetailQuery(folderName, name));
            return res.ToHttpResult();
        });

        group.MapGet("/zones", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new GetZonesQuery());
            return res.ToHttpResult();
        });

        group.MapPost("/folders/{folderName}/patterns/{name}/run", async (string folderName, string name, RunPatternRequest body, IMediator mediator) =>
        {
            var res = await mediator.Send(new RunPatternCommand(
                FolderName: folderName,
                Name: name,
                State: body.State,
                ZoneNames: body.ZoneNames,
                Data: body.Data,
                IsManual: true,
                ScheduleName: body.ScheduleName
            ));
            return res.ToHttpResult();
        });
        group.MapGet("/health", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new HealthCheckQuery());
            return res.ToHttpResult();
        });

        group.MapPost("/sync/folders", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new SyncFoldersFromControllerCommand());
            return res.ToHttpResult();
        });

        group.MapPost("/sync/zones", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new SyncZonesFromControllerCommand());
            return res.ToHttpResult();
        });

        group.MapPost("/sync/patterns", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new SyncPatternsFromControllerCommand());
            return res.ToHttpResult();
        });

        group.MapPost("/sync/patterns/{folderName}/{name}", async (string folderName, string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new SyncPatternDetailFromControllerCommand(folderName, name));
            return res.ToHttpResult();
        });

        group.MapPost("/sync/patterns/details", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new SyncAllPatternDetailsFromControllerCommand());
            return res.ToHttpResult();
        });



        return app;
    }
}
