using JellyFish.Api.Domain.Services;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace JellyFish.Api.Tests.Domain.Services;

public class ScheduleNextEventServiceMultiDayTests
{
    private readonly ScheduleNextEventService _service;
    private readonly Mock<ISolarTimesService> _solarMock;

    public ScheduleNextEventServiceMultiDayTests()
    {
        _solarMock = new Mock<ISolarTimesService>();
        var options = Options.Create(new ScheduleSettings
        {
            TimeZoneId = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.0060,
            ElevationMeters = 10
        });
        _service = new ScheduleNextEventService(options, _solarMock.Object);
    }

    [Fact]
    public void ComputeMultiDayEvents_Should_Generate_PerDay_On_And_Off_For_CrossDay_Schedule_With_Fixed_Off()
    {
        // Arrange
        var fromUtc = new DateTime(2025, 8, 9, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2025, 8, 12, 23, 59, 59, DateTimeKind.Utc);

        // Setup solar times for each day needed (9th, 10th, 11th)
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), new DateOnly(2025, 8, 9)))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                new DateTime(2025, 8, 9, 10, 30, 0, DateTimeKind.Utc), // sunrise
                new DateTime(2025, 8, 9, 23, 45, 0, DateTimeKind.Utc), // sunset
                new DateTime(2025, 8, 9, 17, 0, 0, DateTimeKind.Utc),  // solar noon
                800 // day length minutes
            )));
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), new DateOnly(2025, 8, 10)))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                new DateTime(2025, 8, 10, 10, 31, 0, DateTimeKind.Utc), // sunrise
                new DateTime(2025, 8, 10, 23, 44, 0, DateTimeKind.Utc), // sunset
                new DateTime(2025, 8, 10, 17, 0, 0, DateTimeKind.Utc),  // solar noon
                800 // day length minutes
            )));
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), new DateOnly(2025, 8, 11)))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                new DateTime(2025, 8, 11, 10, 32, 0, DateTimeKind.Utc), // sunrise
                new DateTime(2025, 8, 11, 23, 43, 0, DateTimeKind.Utc), // sunset
                new DateTime(2025, 8, 11, 17, 0, 0, DateTimeKind.Utc),  // solar noon
                800 // day length minutes
            )));

        // Act - Schedule from 8/9/2025 @ sunset to 8/11/2025 @ 22:00, per-day
        var result = _service.ComputeMultiDayEvents(
            onTime: "sunset",
            onDay: "9 8 *",      // August 9th
            offTime: "22:00",
            offDay: "11 8 *",    // August 11th
            fromUtc: fromUtc,
            toUtc: toUtc);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var events = result.Value.OrderBy(e => e.EventUtc).ToList();

        // Expect ON/OFF for 9th, 10th, and 11th
        events.ShouldContain(e => e.Kind == "On" && e.Date == new DateOnly(2025, 8, 9));
        events.ShouldContain(e => e.Kind == "Off" && e.Date == new DateOnly(2025, 8, 9));
        events.ShouldContain(e => e.Kind == "On" && e.Date == new DateOnly(2025, 8, 10));
        events.ShouldContain(e => e.Kind == "Off" && e.Date == new DateOnly(2025, 8, 10));
        events.ShouldContain(e => e.Kind == "On" && e.Date == new DateOnly(2025, 8, 11));
        events.ShouldContain(e => e.Kind == "Off" && e.Date == new DateOnly(2025, 8, 11));
    }

    [Fact]
    public void ComputeNextEventUtc_Should_Use_MultiDay_Logic()
    {
        // Arrange
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateOnly>()))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                DateTime.UtcNow.AddHours(6),  // sunrise
                DateTime.UtcNow.AddHours(18), // sunset
                DateTime.UtcNow.AddHours(12), // solar noon
                720 // day length minutes
            )));

        // Act
        var result = _service.ComputeNextEventUtc(
            onTime: "sunset",
            onDay: "* * *",      // Every day
            offTime: "sunrise",
            offDay: "* * *");    // Every day

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.NextUtc.ShouldNotBeNull();
        result.Value.Kind.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void IsActiveAt_Should_Use_MultiDay_Logic()
    {
        // Arrange
        var now = DateTime.UtcNow;
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateOnly>()))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                now.AddHours(-12), // sunrise was 12 hours ago
                now.AddHours(-6),  // sunset was 6 hours ago
                now.AddHours(-9),  // solar noon
                720 // day length minutes
            )));

        // Act - Should be active since sunset was more recent than sunrise
        var result = _service.IsActiveAt(
            onTime: "sunset",
            onDay: "* * *",
            offTime: "sunrise",
            offDay: "* * *",
            nowUtc: now);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue(); // Should be active since sunset was more recent
    }

    [Fact]
    public void ComputeMultiDayEvents_Should_Create_PerDay_OnOff_For_FixedTime_When_On_And_Off_Days_Differ()
    {
        // Arrange - 8/9 @ sunset to 8/11 @ 22:00
        var fromUtc = new DateTime(2025, 8, 9, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2025, 8, 12, 23, 59, 59, DateTimeKind.Utc);

        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), new DateOnly(2025, 8, 9)))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                new DateTime(2025, 8, 9, 10, 30, 0, DateTimeKind.Utc), // sunrise
                new DateTime(2025, 8, 9, 23, 45, 0, DateTimeKind.Utc), // sunset
                new DateTime(2025, 8, 9, 17, 0, 0, DateTimeKind.Utc),  // solar noon
                800 // day length minutes
            )));
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), new DateOnly(2025, 8, 10)))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                new DateTime(2025, 8, 10, 10, 31, 0, DateTimeKind.Utc), // sunrise
                new DateTime(2025, 8, 10, 23, 44, 0, DateTimeKind.Utc), // sunset
                new DateTime(2025, 8, 10, 17, 0, 0, DateTimeKind.Utc),  // solar noon
                800 // day length minutes
            )));
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), new DateOnly(2025, 8, 11)))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                new DateTime(2025, 8, 11, 10, 32, 0, DateTimeKind.Utc), // sunrise
                new DateTime(2025, 8, 11, 23, 43, 0, DateTimeKind.Utc), // sunset
                new DateTime(2025, 8, 11, 17, 0, 0, DateTimeKind.Utc),  // solar noon
                800 // day length minutes
            )));

        // Act
        var result = _service.ComputeMultiDayEvents(
            onTime: "sunset",
            onDay: "9 8 *",
            offTime: "22:00",
            offDay: "11 8 *",
            fromUtc: fromUtc,
            toUtc: toUtc);

        // Assert: ON/OFF for each day from 9th..11th
        result.IsSuccess.ShouldBeTrue();
        var events = result.Value.OrderBy(e => e.EventUtc).ToList();
        events.ShouldContain(e => e.Kind == "On" && e.Date == new DateOnly(2025, 8, 9));
        events.ShouldContain(e => e.Kind == "Off" && e.Date == new DateOnly(2025, 8, 9));
        events.ShouldContain(e => e.Kind == "On" && e.Date == new DateOnly(2025, 8, 10));
        events.ShouldContain(e => e.Kind == "Off" && e.Date == new DateOnly(2025, 8, 10));
        events.ShouldContain(e => e.Kind == "On" && e.Date == new DateOnly(2025, 8, 11));
        events.ShouldContain(e => e.Kind == "Off" && e.Date == new DateOnly(2025, 8, 11));
    }

    [Fact]
    public void IsActiveAt_PerDay_Semantics_For_CrossDay_Fixed_Times()
    {
        // Arrange: 8/9..8/11 with daily window 18:00-06:00 local
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

        DateTime ToUtc(int y, int m, int d, int h, int min) =>
            TimeZoneInfo.ConvertTimeToUtc(new DateTime(y, m, d, h, min, 0, DateTimeKind.Unspecified), tz);

        // Act & Assert within the range on 8/10
        var at2amUtc = ToUtc(2025, 8, 10, 2, 0);
        var resNight = _service.IsActiveAt("18:00", "9 8 *", "06:00", "11 8 *", at2amUtc);
        resNight.IsSuccess.ShouldBeTrue();
        resNight.Value.ShouldBeTrue();

        var atNoonUtc = ToUtc(2025, 8, 10, 12, 0);
        var resDay = _service.IsActiveAt("18:00", "9 8 *", "06:00", "11 8 *", atNoonUtc);
        resDay.IsSuccess.ShouldBeTrue();
        resDay.Value.ShouldBeFalse();

        var at8pmUtc = ToUtc(2025, 8, 10, 20, 0);
        var resEve = _service.IsActiveAt("18:00", "9 8 *", "06:00", "11 8 *", at8pmUtc);
        resEve.IsSuccess.ShouldBeTrue();
        resEve.Value.ShouldBeTrue();

}

    [Fact]
    public void ComputeMultiDayEvents_Should_Include_OnDays_Before_Window_Start_For_December_Range()
    {
        // Arrange: window inside the multi-day period (Dec 5-7), while ON day is Dec 1 and OFF day is Dec 25
        var fromUtc = new DateTime(2025, 12, 5, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2025, 12, 7, 0, 0, 0, DateTimeKind.Utc);

        // Provide solar times for days within the checked window
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), new DateOnly(2025, 12, 5)))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                new DateTime(2025, 12, 5, 12, 0, 0, DateTimeKind.Utc),  // sunrise
                new DateTime(2025, 12, 5, 22, 0, 0, DateTimeKind.Utc),  // sunset
                new DateTime(2025, 12, 5, 17, 0, 0, DateTimeKind.Utc),  // solar noon
                600 // day length minutes
            )));
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), new DateOnly(2025, 12, 6)))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                new DateTime(2025, 12, 6, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2025, 12, 6, 22, 0, 0, DateTimeKind.Utc),
                new DateTime(2025, 12, 6, 17, 0, 0, DateTimeKind.Utc),
                600
            )));
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), new DateOnly(2025, 12, 7)))
            .Returns(JellyFish.Api.Common.Result.Success(new SolarTimes(
                new DateTime(2025, 12, 7, 12, 0, 0, DateTimeKind.Utc),
                new DateTime(2025, 12, 7, 22, 0, 0, DateTimeKind.Utc),
                new DateTime(2025, 12, 7, 17, 0, 0, DateTimeKind.Utc),
                600
            )));

        // Fallback for any other date invocations to avoid null returns from mock
        _solarMock.Setup(s => s.CalculateUtc(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<DateOnly>()))
            .Returns((double lat, double lon, double elev, DateOnly d) => JellyFish.Api.Common.Result.Success(new SolarTimes(
                d.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc),
                d.ToDateTime(new TimeOnly(22, 0), DateTimeKind.Utc),
                d.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc),
                600
            )));

        // Act - Multi-day schedule from Dec 1 @ sunset to Dec 25 @ 21:00 local
        var result = _service.ComputeMultiDayEvents(
            onTime: "sunset",
            onDay: "1 12 *",
            offTime: "21:00",
            offDay: "25 12 *",
            fromUtc: fromUtc,
            toUtc: toUtc);

        // Assert - ensure we have per-day ON/OFF events for days within the window even though ON-day is before the window
        result.IsSuccess.ShouldBeTrue();
        var events = result.Value.ToList();
        events.ShouldContain(e => e.Kind == "On" && e.Date == new DateOnly(2025, 12, 5));
        events.ShouldContain(e => e.Kind == "Off" && e.Date == new DateOnly(2025, 12, 5));
        events.ShouldContain(e => e.Kind == "On" && e.Date == new DateOnly(2025, 12, 6));
        events.ShouldContain(e => e.Kind == "Off" && e.Date == new DateOnly(2025, 12, 6));
    }


}