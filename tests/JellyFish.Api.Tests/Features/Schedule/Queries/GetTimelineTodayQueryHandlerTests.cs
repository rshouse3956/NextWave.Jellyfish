using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.Schedule.Queries;
using JellyFish.Api.Infrastructure.Repositories;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using DomainSchedule = JellyFish.Api.Domain.Schedule;

namespace JellyFish.Api.Tests.Features.Schedule.Queries;

public class GetTimelineTodayQueryHandlerTests
{
    private sealed class FakeSolarTimesServiceLocal : ISolarTimesService
    {
        private readonly TimeZoneInfo _tz;
        public FakeSolarTimesServiceLocal(string tzId)
        {
            _tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        public Result<SolarTimes> CalculateUtc(double latitudeDeg, double longitudeDeg, double elevationMeters, DateOnly date)
        {
            // Return sunrise/sunset at fixed LOCAL times (06:00/18:00 local), converted to UTC
            var sunriseLocal = date.ToDateTime(new TimeOnly(6, 0), DateTimeKind.Unspecified);
            var sunsetLocal = date.ToDateTime(new TimeOnly(18, 0), DateTimeKind.Unspecified);
            var sunriseUtc = TimeZoneInfo.ConvertTimeToUtc(sunriseLocal, _tz);
            var sunsetUtc = TimeZoneInfo.ConvertTimeToUtc(sunsetLocal, _tz);
            var solarNoonLocal = date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified);
            var solarNoonUtc = TimeZoneInfo.ConvertTimeToUtc(solarNoonLocal, _tz);
            return Result.Success(new SolarTimes(sunriseUtc, sunsetUtc, solarNoonUtc, 12 * 60));
        }
    }

    private static (IScheduleNextEventService NextSvc, IOptions<ScheduleSettings> Settings) CreateSvcAndSettings(string tzId)
    {
        var settings = Options.Create(new ScheduleSettings
        {
            TimeZoneId = tzId,
            Latitude = 40.0,
            Longitude = -105.0,
            ElevationMeters = 1600
        });
        var solar = new FakeSolarTimesServiceLocal(tzId);
        var svc = new ScheduleNextEventService(settings, solar);
        return (svc, settings);
    }

    [Fact]
    public async Task Timeline_Should_Have_Two_Ranges_For_Sunset_To_Sunrise()
    {
        // Arrange
        var tzId = "America/Denver";
        var (nextSvc, settings) = CreateSvcAndSettings(tzId);

        var schedule = DomainSchedule.Create(
            name: "Night Lights",
            folderName: "Home",
            patternName: "WarmWhite",
            priority: "Normal",
            onTime: "Sunset",
            onDay: "* * *",
            offTime: "Sunrise",
            offDay: "* * *",
            zoneNames: new List<string> { "Z1" },
            nextEventUtc: null
        ).Value;

        var schedules = new List<DomainSchedule> { schedule };
        var repo = new Mock<IScheduleRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(schedules));

        var handler = new GetTimelineTodayQueryHandler(repo.Object, nextSvc, settings);

        // Pick a summer date where DST is active; our fake ensures 06:00/18:00 LOCAL
        var target = DateOnly.Parse("2025-07-01");

        // Act
        var res = await handler.Handle(new GetTimelineTodayQuery(target), default);

        // Assert
        res.IsSuccess.ShouldBeTrue();
        res.Value.Count.ShouldBe(1);
        var item = res.Value[0];
        item.Ranges.Count.ShouldBe(2);

        // Expect: [0, 6] and [18, 24]
        var r1 = item.Ranges[0];
        var r2 = item.Ranges[1];
        r1.StartHour.ShouldBeGreaterThanOrEqualTo(0.0);
        r1.StartHour.ShouldBeLessThan(0.01);
        (r1.EndHour).ShouldBeInRange(5.99, 6.01);
        (r2.StartHour).ShouldBeInRange(17.99, 18.01);
        (r2.EndHour).ShouldBeInRange(23.99, 24.01);

        }

        [Fact]
        public async Task Timeline_CrossDay_OnNov24Sunset_OffFourthThu21_ShouldBeSunsetTo21_On_25th()
        {
            var tzId = "America/Denver";
            var (nextSvc, settings) = CreateSvcAndSettings(tzId);

            var schedule = DomainSchedule.Create(
                name: "Thanksgiving Window",
                folderName: "Home",
                patternName: "Hold",
                priority: "Normal",
                onTime: "Sunset",
                onDay: "24 11 *",
                offTime: "21:00",
                offDay: "* 11 4#4",
                zoneNames: new List<string> { "Z1" },
                nextEventUtc: null
            ).Value;

            var schedules = new List<DomainSchedule> { schedule };
            var repo = new Moq.Mock<IScheduleRepository>();
            repo.Setup(r => r.ListAsync(Moq.It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(schedules));

            var handler = new GetTimelineTodayQueryHandler(repo.Object, nextSvc, settings);

            var target = DateOnly.Parse("2025-11-25");
            var res = await handler.Handle(new GetTimelineTodayQuery(target), default);

            res.IsSuccess.ShouldBeTrue();
            res.Value.Count.ShouldBe(1);
            var item = res.Value[0];
            item.Ranges.Count.ShouldBe(1);
            var r = item.Ranges[0];
            r.StartHour.ShouldBeInRange(17.99, 18.01);
            r.EndHour.ShouldBeInRange(20.99, 21.01);
        }

        [Fact]
        public async Task Timeline_CrossDay_OnNov24Sunset_OffFourthThu21_ShouldBeSunsetTo21_On_27th()
        {
            var tzId = "America/Denver";
            var (nextSvc, settings) = CreateSvcAndSettings(tzId);

            var schedule = DomainSchedule.Create(
                name: "Thanksgiving Window",
                folderName: "Home",
                patternName: "Hold",
                priority: "Normal",
                onTime: "Sunset",
                onDay: "24 11 *",
                offTime: "21:00",
                offDay: "* 11 4#4",
                zoneNames: new List<string> { "Z1" },
                nextEventUtc: null
            ).Value;

            var schedules = new List<DomainSchedule> { schedule };
            var repo = new Moq.Mock<IScheduleRepository>();
            repo.Setup(r => r.ListAsync(Moq.It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(schedules));

            var handler = new GetTimelineTodayQueryHandler(repo.Object, nextSvc, settings);

            var target = DateOnly.Parse("2025-11-27");
            var res = await handler.Handle(new GetTimelineTodayQuery(target), default);

            res.IsSuccess.ShouldBeTrue();
            res.Value.Count.ShouldBe(1);
            var item = res.Value[0];
            item.Ranges.Count.ShouldBe(1);
            var r = item.Ranges[0];
            r.StartHour.ShouldBeInRange(17.99, 18.01);
            r.EndHour.ShouldBeInRange(20.99, 21.01);
        }

        [Fact]
        public async Task Timeline_NewYearsEve_Overnight_ShouldShow_MidnightTo2am_On_Jan1()
        {
            // Arrange: Schedule On: 12/31 @ 22:00, Off: 12/31 @ 02:00 (overnight, off is actually 01/01 @ 02:00)
            var tzId = "America/Denver";
            var (nextSvc, settings) = CreateSvcAndSettings(tzId);

            var schedule = DomainSchedule.Create(
                name: "New Years Eve",
                folderName: "Home",
                patternName: "Party",
                priority: "Normal",
                onTime: "22:00",
                onDay: "31 12 *",
                offTime: "02:00",
                offDay: "31 12 *",
                zoneNames: new List<string> { "Z1" },
                nextEventUtc: null
            ).Value;

            var schedules = new List<DomainSchedule> { schedule };
            var repo = new Moq.Mock<IScheduleRepository>();
            repo.Setup(r => r.ListAsync(Moq.It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(schedules));

            var handler = new GetTimelineTodayQueryHandler(repo.Object, nextSvc, settings);

            // Act: View timeline for January 1st (the day after the On date)
            var target = DateOnly.Parse("2026-01-01");
            var res = await handler.Handle(new GetTimelineTodayQuery(target), default);

            // Assert: Should show a range from midnight (0:00) to 2:00am
            res.IsSuccess.ShouldBeTrue();
            res.Value.Count.ShouldBe(1);
            var item = res.Value[0];
            item.Ranges.Count.ShouldBe(1);
            var r = item.Ranges[0];
            r.StartHour.ShouldBeInRange(0.0, 0.01); // Midnight
            r.EndHour.ShouldBeInRange(1.99, 2.01);  // 2:00 AM
        }

        [Fact]
        public async Task Timeline_NewYearsEve_Overnight_ShouldShow_10pmToMidnight_On_Dec31()
        {
            // Arrange: Schedule On: 12/31 @ 22:00, Off: 12/31 @ 02:00 (overnight, off is actually 01/01 @ 02:00)
            var tzId = "America/Denver";
            var (nextSvc, settings) = CreateSvcAndSettings(tzId);

            var schedule = DomainSchedule.Create(
                name: "New Years Eve",
                folderName: "Home",
                patternName: "Party",
                priority: "Normal",
                onTime: "22:00",
                onDay: "31 12 *",
                offTime: "02:00",
                offDay: "31 12 *",
                zoneNames: new List<string> { "Z1" },
                nextEventUtc: null
            ).Value;

            var schedules = new List<DomainSchedule> { schedule };
            var repo = new Moq.Mock<IScheduleRepository>();
            repo.Setup(r => r.ListAsync(Moq.It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(schedules));

            var handler = new GetTimelineTodayQueryHandler(repo.Object, nextSvc, settings);

            // Act: View timeline for December 31st
            var target = DateOnly.Parse("2025-12-31");
            var res = await handler.Handle(new GetTimelineTodayQuery(target), default);

            // Assert: Should show a range from 22:00 to midnight (24:00)
            res.IsSuccess.ShouldBeTrue();
            res.Value.Count.ShouldBe(1);
            var item = res.Value[0];
            item.Ranges.Count.ShouldBe(1);
            var r = item.Ranges[0];
            r.StartHour.ShouldBeInRange(21.99, 22.01); // 10:00 PM
            r.EndHour.ShouldBeInRange(23.99, 24.01);   // Midnight
        }
    }
