namespace JellyFish.Api.Features.Schedule;

public sealed record ScheduleDto(
    string Name,
    string FolderName,
    string PatternName,
    string Priority,
    string OnTime,
    string OnDay,
    string OffTime,
    string OffDay,
    IReadOnlyList<string> ZoneNames,
    DateTime CreateDate,
    DateTime LastModifiedDate,
    DateTime? NextEvent,
    string NextEventKind,
    bool IsManualOverride);

