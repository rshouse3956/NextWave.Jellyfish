using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.SolarTimes.Queries;

public sealed record GetSolarTimesQuery(
    double Latitude,
    double Longitude,
    double ElevationMeters,
    DateOnly Date
) : IRequest<Result<SolarTimesDto>>;

