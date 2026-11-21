using JellyFish.Api.Common;

namespace JellyFish.Api.Domain.Services;

public interface IScheduleNextEventService
{
    Result<ScheduleNextEventResult> ComputeNextEventUtc(string onTime, string onDay, string offTime, string offDay);
    Result<bool> IsActiveAt(string onTime, string onDay, string offTime, string offDay, DateTime nowUtc);
    Result<JellyFish.Api.Domain.Schedule?> SelectDesiredActive(IReadOnlyList<JellyFish.Api.Domain.Schedule> schedules, DateTime nowUtc);
    Result<ScheduleDiagnostics> GetDiagnostics(string onTime, string onDay, string offTime, string offDay, DateTime? nowUtc = null);
    Result<IReadOnlyList<ScheduleEvent>> ComputeMultiDayEvents(string onTime, string onDay, string offTime, string offDay, DateTime fromUtc, DateTime toUtc);
}

public sealed record ScheduleNextEventResult(DateTime? NextUtc, string Kind);
public sealed record ScheduleEvent(DateTime EventUtc, string Kind, DateOnly Date);
public sealed record ScheduleDiagnostics(
    DateTime? PrevOnUtc,
    DateTime? PrevOffUtc,
    DateTime? NextOnUtc,
    DateTime? NextOffUtc,
    bool IsActive,
    DateTime? NextOverallUtc,
    string NextKind,
    string TimeZoneIdResolved
);

