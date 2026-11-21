using JellyFish.Api.Features.Schedule.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;
using System.Collections.Generic;

namespace JellyFish.Api.Tests.Features.Schedule.Commands;

public class CreateScheduleCommandValidatorTests
{
    private readonly Mock<IScheduleRepository> _schedules = new();
    private readonly Mock<IPatternRepository> _patterns = new();
    private readonly Mock<IZoneRepository> _zones = new();

    [Fact]
    public async Task Should_Fail_When_Schedule_Exists()
    {
        _schedules.Setup(r => r.ExistsByNameAsync("Dup", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _zones.Setup(z => z.ExistsByNameAsync("Z1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var v = new CreateScheduleCommandValidator(_schedules.Object, _patterns.Object, _zones.Object);
        var cmd = new CreateScheduleCommand("Dup", "Home", "Sunrise", "Normal", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"});
        var result = await v.ValidateAsync(cmd);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Fail_When_Pattern_Not_Found()
    {
        _schedules.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _patterns.Setup(r => r.ExistsAsync("Home", "Sunset", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _zones.Setup(z => z.ExistsByNameAsync("Z1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var v = new CreateScheduleCommandValidator(_schedules.Object, _patterns.Object, _zones.Object);
        var cmd = new CreateScheduleCommand("New", "Home", "Sunset", "Low", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"});
        var result = await v.ValidateAsync(cmd);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Succeed_With_Valid_Data()
    {
        _schedules.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _patterns.Setup(r => r.ExistsAsync("Home", "Sunrise", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _zones.Setup(z => z.ExistsByNameAsync("Z1", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var v = new CreateScheduleCommandValidator(_schedules.Object, _patterns.Object, _zones.Object);
        var cmd = new CreateScheduleCommand("New", "Home", "Sunrise", "High", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"});
        var result = await v.ValidateAsync(cmd);
        result.IsValid.ShouldBeTrue();
    }
}

