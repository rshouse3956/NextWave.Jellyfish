namespace JellyFish.Api.Features.Schedule.Mappers;

public static class ScheduleMapper
{
    public static ScheduleDto ToDto(JellyFish.Api.Domain.Schedule e) => new(
        e.Name,
        e.FolderName,
        e.PatternName,
        e.Priority,
        e.OnTime,
        e.OnDay,
        e.OffTime,
        e.OffDay,
        e.ZoneNames,
        e.CreateDate,
        e.LastModifiedDate,
        e.NextEvent,
        e.NextEventKind,
        false);
}

