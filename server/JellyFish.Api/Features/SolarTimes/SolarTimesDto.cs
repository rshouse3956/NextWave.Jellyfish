namespace JellyFish.Api.Features.SolarTimes;

public sealed record SolarTimesDto(DateTime? SunriseUtc, DateTime? SunsetUtc, DateTime SolarNoonUtc, int DayLengthMinutes);

