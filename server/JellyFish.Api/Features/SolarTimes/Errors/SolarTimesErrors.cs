using JellyFish.Api.Common;

namespace JellyFish.Api.Features.SolarTimes.Errors;

public static class SolarTimesErrors
{
    public static Error InvalidCoordinates() => Error.Validation("Latitude must be [-90,90] and longitude [-180,180].");
    public static Error InvalidElevation() => Error.Validation("Elevation must be between -500 and 10000 meters.");
}

