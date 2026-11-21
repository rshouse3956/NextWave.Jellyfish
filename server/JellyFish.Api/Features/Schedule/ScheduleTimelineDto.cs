namespace JellyFish.Api.Features.Schedule;

public sealed record ScheduleTimelineRangeDto(double StartHour, double EndHour);

public sealed record ScheduleTimelineItemDto(
    string Name,
    string FolderName,
    string PatternName,
    string Priority,
    IReadOnlyList<string> ZoneNames,
    IReadOnlyList<ScheduleTimelineRangeDto> Ranges
);

