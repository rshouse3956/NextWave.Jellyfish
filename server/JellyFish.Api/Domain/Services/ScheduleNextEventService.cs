using Cronos;
using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using Microsoft.Extensions.Options;

namespace JellyFish.Api.Domain.Services;

/// <summary>
/// Represents a parsed time specification (fixed time or solar event).
/// </summary>
public sealed record TimeSpecification
{
    public static readonly TimeOnly ApproximateSunrise = new(6, 0);
    public static readonly TimeOnly ApproximateSunset = new(18, 0);

    public bool IsSolar { get; }
    public TimeOnly? FixedTime { get; }
    public bool IsSunrise { get; }

    public bool IsSunset => IsSolar && !IsSunrise;

    /// <summary>
    /// Returns the approximate time of day for this specification.
    /// For solar events, uses approximate sunrise (6:00) or sunset (18:00).
    /// </summary>
    public TimeOnly ApproximateTimeOfDay => IsSolar
        ? (IsSunrise ? ApproximateSunrise : ApproximateSunset)
        : FixedTime ?? TimeOnly.MinValue;

    private TimeSpecification(bool isSolar, TimeOnly? fixedTime, bool isSunrise)
    {
        IsSolar = isSolar;
        FixedTime = fixedTime;
        IsSunrise = isSunrise;
    }

    public static TimeSpecification Solar(bool isSunrise) => new(true, null, isSunrise);
    public static TimeSpecification Fixed(TimeOnly time) => new(false, time, false);
}

public sealed class ScheduleNextEventService : IScheduleNextEventService
{
    /// <summary>Maximum number of days to search forward/backward for schedule events.</summary>
    private const int MaxSearchDays = 370;

    /// <summary>Maximum iterations when searching backward for previous events (roughly 2x MaxSearchDays).</summary>
    private const int MaxPrevSearchIterations = 740;

    private readonly TimeZoneInfo _timeZone;
    private readonly double? _lat;
    private readonly double? _lon;
    private readonly double? _elev;
    private readonly ISolarTimesService _solar;

    public ScheduleNextEventService(IOptions<ScheduleSettings> options, ISolarTimesService solarTimes)
    {
        var o = options.Value;
        _lat = o.Latitude;
        _lon = o.Longitude;
        _elev = o.ElevationMeters;
        _solar = solarTimes;

        // Validate and cache the timezone at startup
        var tzId = o.TimeZoneId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tzId))
        {
            throw new InvalidOperationException("TimeZoneId is required in Schedule configuration.");
        }

        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Invalid TimeZoneId '{tzId}' in Schedule configuration. " +
                $"Ensure the timezone is valid and tzdata is installed in the container. Error: {ex.Message}", ex);
        }
    }

    public Result<ScheduleNextEventResult> ComputeNextEventUtc(string onTime, string onDay, string offTime, string offDay)
    {
        var nowUtc = DateTime.UtcNow;

        // Use the multi-day logic to get the next event
        var multiDayRes = ComputeMultiDayEvents(onTime, onDay, offTime, offDay, nowUtc, nowUtc.AddDays(MaxSearchDays));
        if (multiDayRes.IsFailure) return Result.Failure<ScheduleNextEventResult>(multiDayRes.Error);

        var nextEvent = multiDayRes.Value.FirstOrDefault(e => e.EventUtc > nowUtc);
        if (nextEvent != null)
        {
            return Result.Success(new ScheduleNextEventResult(nextEvent.EventUtc, nextEvent.Kind));
        }

        return Result.Success(new ScheduleNextEventResult(null, string.Empty));
    }

    private Result<DateTime?> NextEventFor(string timeSpec, string dayFragment, DateTime nowUtc, TimeZoneInfo tz)
    {
        var dayExprResult = ParseDayFragment(dayFragment);
        if (dayExprResult.IsFailure) return Result.Failure<DateTime?>(dayExprResult.Error);
        var dayExpr = dayExprResult.Value;

        var timeSpecResult = ParseTimeSpec(timeSpec);
        if (timeSpecResult.IsFailure) return Result.Failure<DateTime?>(timeSpecResult.Error);
        var spec = timeSpecResult.Value;

        var fromUtc = nowUtc;
        for (int i = 0; i < MaxSearchDays; i++)
        {
            var nextDayUtc = dayExpr.GetNextOccurrence(fromUtc, tz);
            if (nextDayUtc is null) return Result.Success<DateTime?>(null);

            var nextLocal = TimeZoneInfo.ConvertTimeFromUtc(nextDayUtc.Value.Kind == DateTimeKind.Unspecified
                ? TimeZoneInfo.ConvertTimeToUtc(nextDayUtc.Value, tz)
                : (nextDayUtc.Value.Kind == DateTimeKind.Utc ? nextDayUtc.Value : nextDayUtc.Value.ToUniversalTime()), tz);
            var localDate = DateOnly.FromDateTime(nextLocal);

            var candidateResult = ComputeEventTime(localDate, spec, tz);
            if (candidateResult.IsFailure)
            {
                // For solar times that don't exist (polar regions), advance to next day
                fromUtc = nextDayUtc.Value.AddSeconds(1);
                continue;
            }

            var candidateUtc = candidateResult.Value;
            if (candidateUtc > nowUtc)
                return Result.Success<DateTime?>(candidateUtc);

            // Otherwise, advance to find the next matching day
            fromUtc = nextDayUtc.Value.AddSeconds(1);
        }

        return Result.Success<DateTime?>(null);
    }
    private Result<DateTime?> PrevEventFor(string timeSpec, string dayFragment, DateTime nowUtc, TimeZoneInfo tz)
    {
        var dayExprResult = ParseDayFragment(dayFragment);
        if (dayExprResult.IsFailure) return Result.Failure<DateTime?>(dayExprResult.Error);
        var dayExpr = dayExprResult.Value;

        var timeSpecResult = ParseTimeSpec(timeSpec);
        if (timeSpecResult.IsFailure) return Result.Failure<DateTime?>(timeSpecResult.Error);
        var spec = timeSpecResult.Value;

        var start = nowUtc.AddDays(-MaxSearchDays);
        DateTime? last = null;
        var cursor = start;

        for (int i = 0; i < MaxPrevSearchIterations; i++)
        {
            var nextDayUtc = dayExpr.GetNextOccurrence(cursor, tz);
            if (nextDayUtc is null || nextDayUtc.Value > nowUtc) break;

            var nextLocal = TimeZoneInfo.ConvertTimeFromUtc(nextDayUtc.Value.Kind == DateTimeKind.Unspecified
                ? TimeZoneInfo.ConvertTimeToUtc(nextDayUtc.Value, tz)
                : (nextDayUtc.Value.Kind == DateTimeKind.Utc ? nextDayUtc.Value : nextDayUtc.Value.ToUniversalTime()), tz);
            var localDate = DateOnly.FromDateTime(nextLocal);

            var candidateResult = ComputeEventTime(localDate, spec, tz);
            if (candidateResult.IsFailure)
            {
                // For solar times that don't exist (polar regions), skip this day
                cursor = nextDayUtc.Value.AddSeconds(1);
                continue;
            }

            var candidateUtc = candidateResult.Value;
            if (candidateUtc <= nowUtc)
                last = candidateUtc;

            cursor = nextDayUtc.Value.AddSeconds(1);
        }

        return Result.Success(last);
    }

    public Result<bool> IsActiveAt(string onTime, string onDay, string offTime, string offDay, DateTime nowUtc)
    {
        // If ON/OFF day fragments differ, treat as a multi-day range with per-day ON/OFF,
        // not a single continuous window. For each local day within the [start..end] window,
        // the schedule is active only between today's ON and today's OFF times.
        var tz = _timeZone;

        var normalizedOnDay = NormalizeDayFragment(onDay);
        var normalizedOffDay = NormalizeDayFragment(offDay);
        if (!string.Equals(normalizedOnDay, normalizedOffDay, StringComparison.Ordinal))
        {
            // Compute the most recent ON day (date) before or on now
            var prevOnEvt = PrevEventFor(onTime, onDay, nowUtc, tz);
            if (prevOnEvt.IsFailure) return Result.Failure<bool>(prevOnEvt.Error);
            if (!prevOnEvt.Value.HasValue) return Result.Success(false);
            var prevOnLocal = TimeZoneInfo.ConvertTimeFromUtc(prevOnEvt.Value.Value, tz);
            var startDate = DateOnly.FromDateTime(prevOnLocal);

            // Find the OFF day on/after the startDate
            var offDayExprResult = ParseDayFragment(offDay);
            if (offDayExprResult.IsFailure) return Result.Failure<bool>(offDayExprResult.Error);
            var offDayExpr = offDayExprResult.Value;

            var startLocalMidnight = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            var startUtcForCron = TimeZoneInfo.ConvertTimeToUtc(startLocalMidnight, tz);
            var nextOffDay = offDayExpr.GetNextOccurrence(startUtcForCron.AddSeconds(-1), tz);
            if (nextOffDay is null) return Result.Success(false);
            var nextOffLocal = TimeZoneInfo.ConvertTimeFromUtc(nextOffDay.Value, tz);
            var endDate = DateOnly.FromDateTime(nextOffLocal);

            var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var todayDate = DateOnly.FromDateTime(todayLocal);

            if (todayDate < startDate || todayDate > endDate)
                return Result.Success(false);

            // Evaluate today's ON and OFF times
            var onSpec = ParseTimeSpec(onTime);
            if (onSpec.IsFailure) return Result.Failure<bool>(onSpec.Error);
            var offSpec = ParseTimeSpec(offTime);
            if (offSpec.IsFailure) return Result.Failure<bool>(offSpec.Error);

            var onUtcRes = ComputeEventTime(todayDate, onSpec.Value, tz);
            if (onUtcRes.IsFailure) return Result.Failure<bool>(onUtcRes.Error);
            var offUtcRes = ComputeEventTime(todayDate, offSpec.Value, tz);
            if (offUtcRes.IsFailure) return Result.Failure<bool>(offUtcRes.Error);

            var onUtcToday = onUtcRes.Value;
            var offUtcToday = offUtcRes.Value;

            // If OFF precedes ON within the same local day, treat as two segments; determine if "now" is before OFF or after ON
            if (offUtcToday <= onUtcToday)
            {
                var activeWrapped = nowUtc < offUtcToday || nowUtc >= onUtcToday;
                return Result.Success(activeWrapped);
            }
            else
            {
                var active = nowUtc >= onUtcToday && nowUtc < offUtcToday;
                return Result.Success(active);
            }
        }

        // Same-day patterns: for specific dates (not "* * *") with overnight wrapping
        // we need special handling since the Off time should be on the next day.
        var onTimeSpec = ParseTimeSpec(onTime);
        if (onTimeSpec.IsFailure) return Result.Failure<bool>(onTimeSpec.Error);
        var offTimeSpec = ParseTimeSpec(offTime);
        if (offTimeSpec.IsFailure) return Result.Failure<bool>(offTimeSpec.Error);

        // Check if this is an overnight schedule (Off time < On time)
        var isOvernightSchedule = ShouldOffEventPushToNextDay(onTimeSpec.Value, offTimeSpec.Value);

        if (isOvernightSchedule && !IsSimpleDailySchedule(onDay, offDay))
        {
            // For specific date patterns with overnight wrapping (e.g., On: 31 12 * @ 22:00, Off: 31 12 * @ 02:00)
            // The schedule is active from On time on the specified day until Off time the next day.
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

            // Find the most recent On date that could affect today
            var prevOnEvt = PrevEventFor(onTime, onDay, nowUtc, tz);
            if (prevOnEvt.IsFailure) return Result.Failure<bool>(prevOnEvt.Error);
            if (!prevOnEvt.Value.HasValue) return Result.Success(false);

            var prevOnLocal = TimeZoneInfo.ConvertTimeFromUtc(prevOnEvt.Value.Value, tz);
            var onDate = DateOnly.FromDateTime(prevOnLocal);
            var offDate = onDate.AddDays(1); // Off is always next day for overnight schedules

            // Calculate the actual On and Off times
            var onUtcOvernightRes = ComputeEventTime(onDate, onTimeSpec.Value, tz);
            if (onUtcOvernightRes.IsFailure) return Result.Failure<bool>(onUtcOvernightRes.Error);
            var offUtcOvernightRes = ComputeEventTime(offDate, offTimeSpec.Value, tz);
            if (offUtcOvernightRes.IsFailure) return Result.Failure<bool>(offUtcOvernightRes.Error);

            var onUtcOvernight = onUtcOvernightRes.Value;
            var offUtcOvernight = offUtcOvernightRes.Value;

            // Active if now is between On and Off
            var active = nowUtc >= onUtcOvernight && nowUtc < offUtcOvernight;
            return Result.Success(active);
        }

        // Fallback: same-day patterns use the general recent ON vs OFF comparison
        var prevOn = PrevEventFor(onTime, onDay, nowUtc, tz);
        if (prevOn.IsFailure) return Result.Failure<bool>(prevOn.Error);
        var prevOff = PrevEventFor(offTime, offDay, nowUtc, tz);
        if (prevOff.IsFailure) return Result.Failure<bool>(prevOff.Error);

        var on = prevOn.Value; var off = prevOff.Value;
        var activeSimple = on.HasValue && (!off.HasValue || on.Value > off.Value);
        return Result.Success(activeSimple);
    }

    private static bool IsSimpleDailySchedule(string onDay, string offDay)
    {
        // Check if both patterns are "* * *" (every day)
        var normalizedOnDay = NormalizeDayFragment(onDay);
        var normalizedOffDay = NormalizeDayFragment(offDay);
        return normalizedOnDay == "* * *" && normalizedOffDay == "* * *";
    }

    /// <summary>
    /// Determines if the Off event should be pushed to the next day based on time comparison.
    /// </summary>
    private static bool ShouldOffEventPushToNextDay(TimeSpecification onSpec, TimeSpecification offSpec)
    {
        // If either specification can't determine a time, assume same day
        if (onSpec.ApproximateTimeOfDay == TimeOnly.MinValue || offSpec.ApproximateTimeOfDay == TimeOnly.MinValue)
            return false;

        // If OFF time is earlier in the day than ON time, it should push to next day
        return offSpec.ApproximateTimeOfDay < onSpec.ApproximateTimeOfDay;
    }

    public Result<ScheduleDiagnostics> GetDiagnostics(string onTime, string onDay, string offTime, string offDay, DateTime? nowUtc = null)
    {
        var tz = _timeZone;
        var now = nowUtc ?? DateTime.UtcNow;

        var prevOn = PrevEventFor(onTime, onDay, now, tz);
        if (prevOn.IsFailure) return Result.Failure<ScheduleDiagnostics>(prevOn.Error);
        var prevOff = PrevEventFor(offTime, offDay, now, tz);
        if (prevOff.IsFailure) return Result.Failure<ScheduleDiagnostics>(prevOff.Error);

        var nextOn = NextEventFor(onTime, onDay, now, tz);
        if (nextOn.IsFailure) return Result.Failure<ScheduleDiagnostics>(nextOn.Error);
        var nextOff = NextEventFor(offTime, offDay, now, tz);
        if (nextOff.IsFailure) return Result.Failure<ScheduleDiagnostics>(nextOff.Error);

        var isActiveRes = IsActiveAt(onTime, onDay, offTime, offDay, now);
        if (isActiveRes.IsFailure) return Result.Failure<ScheduleDiagnostics>(isActiveRes.Error);
        var isActive = isActiveRes.Value;

        var nextOverall = ComputeNextEventUtc(onTime, onDay, offTime, offDay);
        if (nextOverall.IsFailure) return Result.Failure<ScheduleDiagnostics>(nextOverall.Error);

        var diag = new ScheduleDiagnostics(
            PrevOnUtc: prevOn.Value,
            PrevOffUtc: prevOff.Value,
            NextOnUtc: nextOn.Value,
            NextOffUtc: nextOff.Value,
            IsActive: isActive,
            NextOverallUtc: nextOverall.Value.NextUtc,
            NextKind: nextOverall.Value.Kind,
            TimeZoneIdResolved: tz.Id
        );
        return Result.Success(diag);
    }

    public Result<JellyFish.Api.Domain.Schedule?> SelectDesiredActive(IReadOnlyList<JellyFish.Api.Domain.Schedule> schedules, DateTime nowUtc)
    {
        JellyFish.Api.Domain.Schedule? best = null;
        foreach (var s in schedules)
        {
            var activeRes = IsActiveAt(s.OnTime, s.OnDay, s.OffTime, s.OffDay, nowUtc);
            if (activeRes.IsFailure || !activeRes.Value) continue;
            if (best is null || PriorityRank(s.Priority) > PriorityRank(best.Priority) ||
                (PriorityRank(s.Priority) == PriorityRank(best.Priority) && string.CompareOrdinal(s.Name, best.Name) < 0))
            {
                best = s;
            }
        }
        return Result.Success(best);
    }

    public Result<IReadOnlyList<ScheduleEvent>> ComputeMultiDayEvents(string onTime, string onDay, string offTime, string offDay, DateTime fromUtc, DateTime toUtc)
    {
        var tz = _timeZone;
        var events = new List<ScheduleEvent>();

        // Validate inputs
        if (string.IsNullOrWhiteSpace(onDay) || string.IsNullOrWhiteSpace(offDay))
            return Result.Failure<IReadOnlyList<ScheduleEvent>>(Error.Validation("Day fragments are required"));

        var onDayNormalized = NormalizeDayFragment(onDay);
        var offDayNormalized = NormalizeDayFragment(offDay);

        // Parse cron expressions for days
        var onDayExprResult = ParseDayFragment(onDay);
        if (onDayExprResult.IsFailure) return Result.Failure<IReadOnlyList<ScheduleEvent>>(onDayExprResult.Error);
        var onDayExpr = onDayExprResult.Value;

        var offDayExprResult = ParseDayFragment(offDay);
        if (offDayExprResult.IsFailure) return Result.Failure<IReadOnlyList<ScheduleEvent>>(offDayExprResult.Error);
        var offDayExpr = offDayExprResult.Value;

        // Parse time specifications
        var onTimeRes = ParseTimeSpec(onTime);
        if (onTimeRes.IsFailure) return Result.Failure<IReadOnlyList<ScheduleEvent>>(onTimeRes.Error);
        var offTimeRes = ParseTimeSpec(offTime);
        if (offTimeRes.IsFailure) return Result.Failure<IReadOnlyList<ScheduleEvent>>(offTimeRes.Error);

        var onSpec = onTimeRes.Value;
        var offSpec = offTimeRes.Value;

        // Convert UTC range to local dates to avoid timezone boundary issues
        var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, tz);
        var toLocal = TimeZoneInfo.ConvertTimeFromUtc(toUtc, tz);
        var fromDate = DateOnly.FromDateTime(fromLocal);
        var toDate = DateOnly.FromDateTime(toLocal);

        // Generate events for the date range
        // Work entirely in local DateOnly to match cron patterns, then convert to UTC for event times

        // First, find local dates that match the ON pattern. We need the most recent ON day before the window
        // as well as any ON days within the window so that multi-day ranges spanning the window are handled.
        var onDays = new List<DateOnly>();
        var currentDate = fromDate.AddDays(-MaxSearchDays); // search back up to ~1 year to capture prior ON
        var maxIterations = (int)(toDate.ToDateTime(TimeOnly.MinValue) - currentDate.ToDateTime(TimeOnly.MinValue)).TotalDays + 10;

        for (int i = 0; i < maxIterations && currentDate <= toDate.AddDays(1); i++)
        {
            // Convert local date to a DateTime for cron evaluation
            var localDateTime = currentDate.ToDateTime(new TimeOnly(0, 0), DateTimeKind.Unspecified);
            var utcForCron = TimeZoneInfo.ConvertTimeToUtc(localDateTime, tz);

            // Use a slight backstep to make the search effectively inclusive for day-level matches
            var nextOnDay = onDayExpr.GetNextOccurrence(utcForCron.AddSeconds(-1), tz);
            if (nextOnDay == null) break;

            // Convert back to local date for comparison
            var nextOnLocal = TimeZoneInfo.ConvertTimeFromUtc(nextOnDay.Value, tz);
            var onDate = DateOnly.FromDateTime(nextOnLocal);

            if (onDate > toDate.AddDays(1)) break;

            if (!onDays.Contains(onDate))
            {
                onDays.Add(onDate);
            }

            // Advance to the next day to avoid returning the same date again (due to inclusive search fudge)
            currentDate = onDate.AddDays(1);
        }

        // For each ON day, find the corresponding OFF day and generate events
        foreach (var startDate in onDays)
        {
            // Find the corresponding OFF day (should be the next local date that matches the OFF pattern)
            DateOnly? endDate = null;

            // Special case: if ON and OFF day patterns are identical, the OFF day is the same as the ON day
            if (onDayNormalized == offDayNormalized)
            {
                endDate = startDate;
            }
            else
            {
                var offSearchDate = startDate;
                for (int i = 0; i < MaxSearchDays; i++) // Search up to a year ahead
                {
                    var localDateTime = offSearchDate.ToDateTime(new TimeOnly(0, 0), DateTimeKind.Unspecified);
                    var utcForCron = TimeZoneInfo.ConvertTimeToUtc(localDateTime, tz);

                    // Inclusive-style search for OFF day
                    var nextOffDay = offDayExpr.GetNextOccurrence(utcForCron.AddSeconds(-1), tz);
                    if (nextOffDay == null) break;

                    var nextOffLocal = TimeZoneInfo.ConvertTimeFromUtc(nextOffDay.Value, tz);
                    var offDate = DateOnly.FromDateTime(nextOffLocal);

                    if (offDate >= startDate)
                    {
                        endDate = offDate;
                        break;
                    }

                    offSearchDate = offDate.AddDays(1);
                }
            }

            if (endDate == null) continue;

            if (onDayNormalized != offDayNormalized)
            {
                // Multi-day range: generate per-day ON/OFF events only for days that intersect the requested window
                var dStart = startDate < fromDate ? fromDate : startDate;
                var dEnd = endDate.Value > toDate ? toDate : endDate.Value;
                for (var d = dStart; d <= dEnd; d = d.AddDays(1))
                {
                    var perDayOnRes = ComputeEventTime(d, onSpec, tz);
                    if (perDayOnRes.IsSuccess)
                    {
                        var onUtc = perDayOnRes.Value;
                        if (onUtc >= fromUtc && onUtc <= toUtc.AddDays(1))
                            events.Add(new ScheduleEvent(onUtc, "On", d));
                    }

                    var perDayOffRes = ComputeEventTime(d, offSpec, tz);
                    if (perDayOffRes.IsSuccess)
                    {
                        var offUtc = perDayOffRes.Value;
                        if (offUtc >= fromUtc && offUtc <= toUtc.AddDays(1))
                            events.Add(new ScheduleEvent(offUtc, "Off", d));
                    }
                }
            }
            else
            {
                // Same-day pattern: single ON and corresponding OFF (may push to next day based on time-of-day ordering)
                var onEventRes = ComputeEventTime(startDate, onSpec, tz);
                if (onEventRes.IsSuccess)
                {
                    var onEventUtc = onEventRes.Value;
                    if (onEventUtc >= fromUtc && onEventUtc <= toUtc.AddDays(1))
                    {
                        events.Add(new ScheduleEvent(onEventUtc, "On", startDate));
                    }
                }

                var offEventDate = ShouldOffEventPushToNextDay(onSpec, offSpec)
                    ? startDate.AddDays(1)
                    : startDate;

                if (offEventDate <= toDate.AddDays(1))
                {
                    var offEventRes = ComputeEventTime(offEventDate, offSpec, tz);
                    if (offEventRes.IsSuccess)
                    {
                        var offEventUtc = offEventRes.Value;
                        if (offEventUtc >= fromUtc && offEventUtc <= toUtc.AddDays(1))
                        {
                            events.Add(new ScheduleEvent(offEventUtc, "Off", offEventDate));
                        }
                    }
                }
            }
        }

        // Sort events by time
        events.Sort((a, b) => a.EventUtc.CompareTo(b.EventUtc));

        return Result.Success<IReadOnlyList<ScheduleEvent>>(events);
    }

    private static Result<TimeSpecification> ParseTimeSpec(string timeSpec)
    {
        if (string.IsNullOrWhiteSpace(timeSpec))
            return Result.Failure<TimeSpecification>(Error.Validation("Time specification is required"));

        var isSunrise = timeSpec.Equals("sunrise", StringComparison.OrdinalIgnoreCase);
        var isSunset = timeSpec.Equals("sunset", StringComparison.OrdinalIgnoreCase);

        if (isSunrise || isSunset)
        {
            return Result.Success(TimeSpecification.Solar(isSunrise));
        }

        if (TimeOnly.TryParseExact(timeSpec, "HH:mm", out var fixedTime))
        {
            return Result.Success(TimeSpecification.Fixed(fixedTime));
        }

        return Result.Failure<TimeSpecification>(Error.Validation($"Time must be HH:mm or 'Sunrise'/'Sunset' (got '{timeSpec}')"));
    }

    private static Result<CronExpression> ParseDayFragment(string dayFragment)
    {
        if (string.IsNullOrWhiteSpace(dayFragment))
            return Result.Failure<CronExpression>(Error.Validation("Day fragment is required"));

        var normalizedFragment = NormalizeDayFragment(dayFragment);
        var dayParts = normalizedFragment.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (dayParts.Length != 3)
            return Result.Failure<CronExpression>(Error.Validation("Day fragment must have exactly 3 cron fields: day-of-month month day-of-week"));

        try
        {
            return Result.Success(CronExpression.Parse($"0 0 {normalizedFragment}"));
        }
        catch
        {
            return Result.Failure<CronExpression>(Error.Validation($"Invalid day fragment '{dayFragment}'"));
        }
    }

    private Result<DateTime> ComputeEventTime(DateOnly date, TimeSpecification spec, TimeZoneInfo tz)
    {
        if (spec.IsSolar)
        {
            if (!_lat.HasValue || !_lon.HasValue || !_elev.HasValue)
                return Result.Failure<DateTime>(Error.Validation("Schedule Sunrise/Sunset requires Schedule:Latitude, Longitude, ElevationMeters configured."));

            var solar = _solar.CalculateUtc(_lat.Value, _lon.Value, _elev.Value, date);
            if (solar.IsFailure) return Result.Failure<DateTime>(solar.Error);

            // Use the actual sunrise/sunset specification
            var solarTime = spec.IsSunrise
                ? (solar.Value.SunriseUtc ?? DateTime.MinValue)
                : (solar.Value.SunsetUtc ?? DateTime.MinValue);

            if (solarTime == DateTime.MinValue)
                return Result.Failure<DateTime>(Error.Validation($"No {(spec.IsSunrise ? "sunrise" : "sunset")} available for date {date}"));

            // Ensure the DateTime has DateTimeKind.Utc set
            var solarTimeWithKind = DateTime.SpecifyKind(solarTime, DateTimeKind.Utc);
            return Result.Success(solarTimeWithKind);
        }
        else if (spec.FixedTime.HasValue)
        {
            var localDateTime = date.ToDateTime(spec.FixedTime.Value, DateTimeKind.Unspecified);
            var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, tz);
            // Ensure the DateTime has DateTimeKind.Utc set
            var utcDateTimeWithKind = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            return Result.Success(utcDateTimeWithKind);
        }

        return Result.Failure<DateTime>(Error.Validation("Invalid time specification"));
    }

    private static int PriorityRank(string p)
    {
        if (string.Equals(p, "High", StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(p, "Normal", StringComparison.OrdinalIgnoreCase)) return 1;
        return 0;
    }

    private static string NormalizeDayFragment(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return fragment;
        // Replace Quartz-style '?' with '*' and compress whitespace to single spaces
        var f = fragment.Replace('?', '*');
        var parts = f.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }
}

