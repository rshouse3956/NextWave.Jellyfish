using JellyFish.Api.Common;
using JellyFish.Api.Features.Holiday;
using JellyFish.Api.Features.Holiday.Commands;
using JellyFish.Api.Features.Holiday.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class HolidayEndpoints
{
    public static IEndpointRouteBuilder MapHolidayEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/holidays").WithTags("Holidays");

        group.MapGet("/", async (IMediator mediator) =>
        {
            var res = await mediator.Send(new ListHolidaysQuery());
            return res.ToHttpResult();
        });

        group.MapGet("/{name}", async (string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new GetHolidayQuery(name));
            return res.ToHttpResult();
        });

        group.MapPost("/", async (CreateHolidayCommand cmd, IMediator mediator) =>
        {
            var res = await mediator.Send(cmd);
            return res.ToHttpResult();
        });

        group.MapPut("/{name}", async (string name, UpdateHolidayCommand body, IMediator mediator) =>
        {
            var res = await mediator.Send(body with { Name = name });
            return res.ToHttpResult();
        });

        group.MapDelete("/{name}", async (string name, IMediator mediator) =>
        {
            var res = await mediator.Send(new DeleteHolidayCommand(name));
            return res.ToHttpResult();
        });

        group.MapGet("/is-holiday", async (string date, IMediator mediator) =>
        {
            if (!DateOnly.TryParse(date, out var d))
                return Result.Failure<IsHolidayDto>(Error.Validation("Invalid date")).ToHttpResult();

            var res = await mediator.Send(new IsHolidayQuery(d));
            return res.ToHttpResult();
        });

        return app;
    }
}

