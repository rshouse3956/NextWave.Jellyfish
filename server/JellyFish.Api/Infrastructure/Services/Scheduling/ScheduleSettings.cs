namespace JellyFish.Api.Infrastructure.Services.Scheduling;

public sealed class ScheduleSettings
{
    public string TimeZoneId { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? ElevationMeters { get; init; }
    public int PollingMinutes { get; init; } = 1;
}

