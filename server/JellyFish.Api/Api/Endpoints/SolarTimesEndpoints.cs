using JellyFish.Api.Common;
using JellyFish.Api.Features.SolarTimes;
using JellyFish.Api.Features.SolarTimes.Queries;
using MediatR;

namespace JellyFish.Api.Api.Endpoints;

public static class SolarTimesEndpoints
{
    public static IEndpointRouteBuilder MapSolarTimesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/solar-times").WithTags("SolarTimes");

        group.MapGet("/", async (double lat, double lon, double? elevationMeters, string? date, IMediator mediator) =>
        {
            var elev = elevationMeters ?? 0.0;

            DateOnly d = date is not null
                ? (DateOnly.TryParse(date, out var parsed) ? parsed : default)
                : DateOnly.FromDateTime(DateTime.UtcNow);

            if (date is not null && d == default)
                return Result.Failure<SolarTimesDto>(Error.Validation("Invalid date (expected yyyy-MM-dd)."))
                    .ToHttpResult();

            var res = await mediator.Send(new GetSolarTimesQuery(lat, lon, elev, d));
            return res.ToHttpResult();
        });

        return app;
    }
}

