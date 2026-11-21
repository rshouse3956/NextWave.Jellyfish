using JellyFish.Api.Domain.Services;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using Microsoft.Extensions.Options;
using Shouldly;

namespace JellyFish.Api.Tests.Services;

public class ScheduleComplexEdgeCasesTests
{
    private sealed class FakeSolarTimesServiceLocal : ISolarTimesService
    {
        private readonly TimeZoneInfo _tz;
        public FakeSolarTimesServiceLocal(string tzId) { _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        public JellyFish.Api.Common.Result<SolarTimes> CalculateUtc(double latitudeDeg, double longitudeDeg, double elevationMeters, DateOnly date)
        {
            // Deterministic solar times at LOCAL 06:00/18:00 converted to UTC
            var sunriseLocal = date.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Unspecified);
            var sunsetLocal = date.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Unspecified);
            var sunriseUtc = TimeZoneInfo.ConvertTimeToUtc(sunriseLocal, _tz);
            var sunsetUtc = TimeZoneInfo.ConvertTimeToUtc(sunsetLocal, _tz);
            var solarNoonLocal = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified);
            var solarNoonUtc = TimeZoneInfo.ConvertTimeToUtc(solarNoonLocal, _tz);
            return JellyFish.Api.Common.Result.Success(new SolarTimes(sunriseUtc, sunsetUtc, solarNoonUtc, 12 * 60));
        }
    }

    private static ScheduleNextEventService CreateSvc(string tzId = "America/Denver")
    {
        var opts = Options.Create(new ScheduleSettings
        {
            TimeZoneId = tzId,
            Latitude = 40.0,
            Longitude = -105.0,
            ElevationMeters = 1600
        });
        return new ScheduleNextEventService(opts, new FakeSolarTimesServiceLocal(tzId));
    }

    [Fact]
    public void Active_From_Nov24_Sunset_To_Fourth_Thursday_November_21_00()
    {
        // On: 24 11 * @ Sunset
        // Off: * 11 4#4 @ 21:00
        var svc = CreateSvc();

        // 2025 Fourth Thursday of November is Nov 27
        // At 18:00 local (6pm), it should still be active until 21:00
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Denver");
        var local = new DateTime(2025, 11, 27, 18, 0, 0, DateTimeKind.Unspecified);
        var nowUtc = TimeZoneInfo.ConvertTimeToUtc(local, tz);

        var res = svc.IsActiveAt("Sunset", "24 11 *", "21:00", "* 11 4#4", nowUtc);
        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBeTrue();

        // At 22:00 local on the same day, it should be inactive
        var lateLocal = new DateTime(2025, 11, 27, 22, 0, 0, DateTimeKind.Unspecified);
        var lateUtc = TimeZoneInfo.ConvertTimeToUtc(lateLocal, tz);
        var res2 = svc.IsActiveAt("Sunset", "24 11 *", "21:00", "* 11 4#4", lateUtc);
        res2.IsSuccess.ShouldBeTrue();
        res2.Value.ShouldBeFalse();
    }

    [Fact]
    public void ComputeMultiDayEvents_Should_Generate_PerDay_On_Off_For_Nov24_To_4thThu()
    {
        var svc = CreateSvc();
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Denver");
        var fromLocal = new DateTime(2025, 11, 23, 0, 0, 0, DateTimeKind.Unspecified);
        var toLocal = new DateTime(2025, 11, 29, 0, 0, 0, DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromLocal, tz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toLocal, tz);

        var res = svc.ComputeMultiDayEvents("Sunset", "24 11 *", "21:00", "* 11 4#4", fromUtc, toUtc);
        res.IsSuccess.ShouldBeTrue();
        var events = res.Value.Select(e => new { e.Kind, Local = TimeZoneInfo.ConvertTimeFromUtc(e.EventUtc, tz) }).ToList();

        // Expect ON at sunset and OFF at 21:00 for 24th, 25th, 26th, and 27th
        var dates = new[] { 24, 25, 26, 27 };
        foreach (var d in dates)
        {
            events.ShouldContain(e => e.Kind == "On" && e.Local.Date == new DateTime(2025, 11, d) && e.Local.Hour == 18);
            events.ShouldContain(e => e.Kind == "Off" && e.Local.Date == new DateTime(2025, 11, d) && e.Local.Hour == 21);
        }
    }
}
