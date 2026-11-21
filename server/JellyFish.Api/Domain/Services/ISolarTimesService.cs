using JellyFish.Api.Common;

namespace JellyFish.Api.Domain.Services;

public interface ISolarTimesService
{
    Result<SolarTimes> CalculateUtc(double latitudeDeg, double longitudeDeg, double elevationMeters, DateOnly date);
}

public sealed record SolarTimes(DateTime? SunriseUtc, DateTime? SunsetUtc, DateTime SolarNoonUtc, int DayLengthMinutes);

