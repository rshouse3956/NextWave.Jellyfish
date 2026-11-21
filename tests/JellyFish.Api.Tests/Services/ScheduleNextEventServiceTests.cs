using JellyFish.Api.Domain.Services;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using Microsoft.Extensions.Options;
using Shouldly;

namespace JellyFish.Api.Tests.Services;

public class ScheduleNextEventServiceTests
{
    private sealed class FakeSolarTimesService : ISolarTimesService
    {
        public JellyFish.Api.Common.Result<SolarTimes> CalculateUtc(double latitudeDeg, double longitudeDeg, double elevationMeters, DateOnly date)
        {
            // Deterministic sunrise/sunset for any date: 06:00/18:00 UTC
            var sunrise = date.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Utc);
            var sunset = date.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Utc);
            return JellyFish.Api.Common.Result.Success(new SolarTimes(sunrise, sunset, date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), 12 * 60));
        }
    }

    private static ScheduleNextEventService CreateSvc()
    {
        var opts = Options.Create(new ScheduleSettings
        {
            TimeZoneId = "America/Denver",
            Latitude = 40.0,
            Longitude = -105.0,
            ElevationMeters = 1600
        });
        return new ScheduleNextEventService(opts, new FakeSolarTimesService());
    }

    [Theory]
    [InlineData("2025-07-01T12:00:00Z", false)] // midday: should be inactive (on=sunset, off=sunrise)
    [InlineData("2025-07-01T19:00:00Z", true)]  // evening after sunset: active
    [InlineData("2025-07-01T04:00:00Z", true)]  // pre-dawn before sunrise: active
    public void IsActive_OnSunset_OffSunrise(string nowIso, bool expected)
    {
        var svc = CreateSvc();
        var now = DateTime.Parse(nowIso).ToUniversalTime();
        var res = svc.IsActiveAt("sunset", "* * *", "sunrise", "* * *", now);
        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("2025-07-01T12:00:00Z", true)]  // midday between sunrise and sunset: active
    [InlineData("2025-07-01T19:00:00Z", false)] // after sunset: inactive
    [InlineData("2025-07-01T04:00:00Z", false)] // before sunrise: inactive
    public void IsActive_OnSunrise_OffSunset(string nowIso, bool expected)
    {
        var svc = CreateSvc();
        var now = DateTime.Parse(nowIso).ToUniversalTime();
        var res = svc.IsActiveAt("sunrise", "* * *", "sunset", "* * *", now);
        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBe(expected);
    }
    [Fact]
    public void IsActive_OnSunset_OffSpecific_FutureDate_Inactive()
    {
        var svc = CreateSvc();
        var now = DateTime.Parse("2025-09-24T12:00:00Z").ToUniversalTime();
        var res = svc.IsActiveAt("sunset", "31 10 ?", "22:00", "31 10 ?", now);
        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBeFalse();
    }

    [Fact]
    public void IsActive_OnFixed_OffFixed_SameFutureDate_Inactive()
    {
        var svc = CreateSvc();
        var now = DateTime.Parse("2025-09-24T12:00:00Z").ToUniversalTime();
        var res = svc.IsActiveAt("18:00", "29 9 *", "21:00", "29 9 *", now);
        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBeFalse();
    }

    [Theory]
    [InlineData("2025-10-28T19:00:00Z", false)] // Day -1, 7pm between sunrise and sunset: inactive
    [InlineData("2025-10-29T19:00:00Z", true)]  // Day 1, 7pm between sunrise and sunset: active
    [InlineData("2025-10-30T19:00:00Z", true)]  // Day 2, 7pm between sunrise and sunset: active
    [InlineData("2025-10-31T19:00:00Z", true)]  // Day 3, 7pm between sunrise and sunset: active
    [InlineData("2025-11-01T19:00:00Z", false)] // Day +1, 7pm between sunrise and sunset: inactive
    public void IsActive_OnSunset_OffFixed_DateRange_Inactive(string nowIso, bool expected)
    {
        var svc = CreateSvc();
        var now = DateTime.Parse(nowIso).ToUniversalTime();
        var res = svc.IsActiveAt("sunset", "29 10 *", "21:00", "31 10 *", now);
        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBe(expected);
    }

    /// <summary>
    /// Tests overnight schedule on specific date: On: 31 12 * @ 22:00, Off: 31 12 * @ 02:00
    /// The schedule should be active from 22:00 on Dec 31 to 02:00 on Jan 1.
    /// MST (America/Denver) is UTC-7 in winter.
    /// Dec 31 at 22:00 MST = Jan 1 at 05:00 UTC
    /// Jan 1 at 02:00 MST = Jan 1 at 09:00 UTC
    /// </summary>
    [Theory]
    [InlineData("2026-01-01T05:00:00Z", true)]  // Dec 31 at 22:00 local = exactly at On time - active
    [InlineData("2026-01-01T06:00:00Z", true)]  // Dec 31 at 23:00 local - active
    [InlineData("2026-01-01T07:00:00Z", true)]  // Jan 1 at 00:00 local - active (overnight window)
    [InlineData("2026-01-01T08:00:00Z", true)]  // Jan 1 at 01:00 local - active (overnight window)
    [InlineData("2026-01-01T09:00:00Z", false)] // Jan 1 at 02:00 local = Off time - inactive
    [InlineData("2026-01-01T12:00:00Z", false)] // Jan 1 at 05:00 local - inactive
    [InlineData("2026-01-01T04:00:00Z", false)] // Dec 31 at 21:00 local - inactive (before On)
    [InlineData("2025-12-31T20:00:00Z", false)] // Dec 31 at 13:00 local - not scheduled (before On)
    public void IsActive_SameDayPattern_OvernightSchedule(string nowIso, bool expected)
    {
        var svc = CreateSvc();
        var now = DateTime.Parse(nowIso).ToUniversalTime();
        // On: 31 12 * @ 22:00 (Dec 31 at 10PM), Off: 31 12 * @ 02:00 (should be Jan 1 at 2AM)
        var res = svc.IsActiveAt("22:00", "31 12 *", "02:00", "31 12 *", now);
        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBe(expected);
    }

}

