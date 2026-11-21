using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using MediatR;
using Microsoft.Extensions.Options;

namespace JellyFish.Api.Features.Schedule.Queries;

public sealed class GetTimelineTodayQueryHandler : IRequestHandler<GetTimelineTodayQuery, Result<List<ScheduleTimelineItemDto>>>
{
    private readonly IScheduleRepository _schedules;
    private readonly IScheduleNextEventService _nextSvc;
    private readonly IOptions<ScheduleSettings> _settings;

    public GetTimelineTodayQueryHandler(IScheduleRepository schedules, IScheduleNextEventService nextSvc, IOptions<ScheduleSettings> settings)
    {
        _schedules = schedules;
        _nextSvc = nextSvc;
        _settings = settings;
    }

    private readonly record struct LocalEvent(DateTime Local, string Kind);

    public async Task<Result<List<ScheduleTimelineItemDto>>> Handle(GetTimelineTodayQuery request, CancellationToken cancellationToken)
    {
        var tzId = _settings.Value.TimeZoneId;
        if (string.IsNullOrWhiteSpace(tzId))
            return Result.Failure<List<ScheduleTimelineItemDto>>(Error.Validation("Schedule:TimeZoneId is required"));

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch (Exception ex)
        {
            return Result.Failure<List<ScheduleTimelineItemDto>>(Error.Validation($"Invalid TimeZoneId '{tzId}': {ex.Message}"));
        }

        var listRes = await _schedules.ListAsync(cancellationToken);
        if (listRes.IsFailure) return Result.Failure<List<ScheduleTimelineItemDto>>(listRes.Error);

        // Determine the local date to compute for
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var targetDate = request.Date ?? DateOnly.FromDateTime(nowLocal);

        var dayStartLocal = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var dayEndLocal = dayStartLocal.AddDays(1);

        // Provide a generous UTC window to the service; we will filter to the exact local date when building ranges
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal.AddDays(-2), tz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(dayEndLocal.AddDays(2), tz);

        var items = new List<ScheduleTimelineItemDto>();

        foreach (var s in listRes.Value)
        {
            var evRes = _nextSvc.ComputeMultiDayEvents(s.OnTime, s.OnDay, s.OffTime, s.OffDay, fromUtc, toUtc);
            if (evRes.IsFailure)
            {
                // Skip schedules with invalid definitions
                continue;
            }

            var events = evRes.Value
                .Select(e => new LocalEvent(TimeZoneInfo.ConvertTimeFromUtc(e.EventUtc, tz), e.Kind))
                .OrderBy(e => e.Local)
                .ToList();

            // Determine if schedule is active at the very start of the local day using the core service
            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, tz);
            var activeAtStartRes = _nextSvc.IsActiveAt(s.OnTime, s.OnDay, s.OffTime, s.OffDay, dayStartUtc);
            var activeAtStart = activeAtStartRes.IsSuccess && activeAtStartRes.Value;

            var ranges = BuildRangesForLocalDay(events, dayStartLocal, dayEndLocal, activeAtStart);

            if (ranges.Count > 0)
            {
                items.Add(new ScheduleTimelineItemDto(
                    s.Name,
                    s.FolderName,
                    s.PatternName,
                    s.Priority,
                    s.ZoneNames,
                    ranges
                ));
            }
        }

        return Result.Success(items);
    }

    private static List<ScheduleTimelineRangeDto> BuildRangesForLocalDay(
        List<LocalEvent> localEvents,
        DateTime dayStartLocal,
        DateTime dayEndLocal,
        bool activeAtStart)
    {
        var ranges = new List<ScheduleTimelineRangeDto>();

        DateTime? currentStart = activeAtStart ? dayStartLocal : (DateTime?)null;

        // Process events within the day window
        foreach (var e in localEvents)
        {
            if (e.Local <= dayStartLocal) continue;
            if (e.Local > dayEndLocal) break;

            if (string.Equals(e.Kind, "On", StringComparison.OrdinalIgnoreCase))
            {
                if (currentStart == null)
                {
                    currentStart = e.Local < dayStartLocal ? dayStartLocal : e.Local;
                }
            }
            else // Off
            {
                if (currentStart != null)
                {
                    var start = currentStart.Value < dayStartLocal ? dayStartLocal : currentStart.Value;
                    var end = e.Local > dayEndLocal ? dayEndLocal : e.Local;
                    if (end > start)
                    {
                        var startHour = (start - dayStartLocal).TotalMinutes / 60.0;
                        var endHour = (end - dayStartLocal).TotalMinutes / 60.0;
                        ranges.Add(new ScheduleTimelineRangeDto(startHour, endHour));
                    }
                    currentStart = null;
                }
            }
        }

        // If still active at end of day, close the range
        if (currentStart != null)
        {
            var start = currentStart.Value < dayStartLocal ? dayStartLocal : currentStart.Value;
            var end = dayEndLocal;
            if (end > start)
            {
                var startHour = (start - dayStartLocal).TotalMinutes / 60.0;
                var endHour = (end - dayStartLocal).TotalMinutes / 60.0;
                ranges.Add(new ScheduleTimelineRangeDto(startHour, endHour));
            }
        }

        return ranges;
    }
}

