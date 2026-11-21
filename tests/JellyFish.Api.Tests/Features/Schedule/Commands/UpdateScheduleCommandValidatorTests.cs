using JellyFish.Api.Features.Schedule.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;
using System.Collections.Generic;

namespace JellyFish.Api.Tests.Features.Schedule.Commands;

public class UpdateScheduleCommandValidatorTests
{
    private readonly Mock<IScheduleRepository> _schedules = new();
    private readonly Mock<IPatternRepository> _patterns = new();
    private readonly Mock<IZoneRepository> _zones = new();

    [Fact]
    public async Task Should_Fail_When_Schedule_Not_Found()
    {
        _schedules.Setup(r => r.ExistsByNameAsync("Missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _zones.Setup(z => z.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var v = new UpdateScheduleCommandValidator(_schedules.Object, _patterns.Object, _zones.Object);
        var cmd = new UpdateScheduleCommand("Missing", "Home", "Sunrise", "Normal", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"});
        var result = await v.ValidateAsync(cmd);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Fail_When_Pattern_Not_Found()
    {
        _schedules.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _patterns.Setup(r => r.ExistsAsync("Home", "Nope", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _zones.Setup(z => z.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var v = new UpdateScheduleCommandValidator(_schedules.Object, _patterns.Object, _zones.Object);
        var cmd = new UpdateScheduleCommand("Name", "Home", "Nope", "Low", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"});
        var result = await v.ValidateAsync(cmd);
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Succeed_With_Valid_Data()
    {
        _schedules.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _patterns.Setup(r => r.ExistsAsync("Home", "Sunrise", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _zones.Setup(z => z.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var v = new UpdateScheduleCommandValidator(_schedules.Object, _patterns.Object, _zones.Object);
        var cmd = new UpdateScheduleCommand("Name", "Home", "Sunrise", "High", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"});
        var result = await v.ValidateAsync(cmd);
        result.IsValid.ShouldBeTrue();
    }
}

