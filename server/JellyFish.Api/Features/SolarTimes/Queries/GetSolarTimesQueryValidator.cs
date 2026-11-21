using FluentValidation;

namespace JellyFish.Api.Features.SolarTimes.Queries;

public sealed class GetSolarTimesQueryValidator : AbstractValidator<GetSolarTimesQuery>
{
    public GetSolarTimesQueryValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        // Allow elevations roughly from Dead Sea to high mountains; clamp negatives in service
        RuleFor(x => x.ElevationMeters).InclusiveBetween(-500, 10000);
    }
}

