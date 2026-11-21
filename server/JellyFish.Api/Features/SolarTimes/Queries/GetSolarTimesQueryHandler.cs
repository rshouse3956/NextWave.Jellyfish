using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using MediatR;

namespace JellyFish.Api.Features.SolarTimes.Queries;

public sealed class GetSolarTimesQueryHandler : IRequestHandler<GetSolarTimesQuery, Result<SolarTimesDto>>
{
    private readonly ISolarTimesService _svc;

    public GetSolarTimesQueryHandler(ISolarTimesService svc)
    {
        _svc = svc;
    }

    public Task<Result<SolarTimesDto>> Handle(GetSolarTimesQuery request, CancellationToken cancellationToken)
    {
        var res = _svc.CalculateUtc(request.Latitude, request.Longitude, request.ElevationMeters, request.Date);
        if (res.IsFailure)
            return Task.FromResult(Result.Failure<SolarTimesDto>(res.Error));

        var v = res.Value;
        var dto = new SolarTimesDto(v.SunriseUtc, v.SunsetUtc, v.SolarNoonUtc, v.DayLengthMinutes);
        return Task.FromResult(Result.Success(dto));
    }
}

