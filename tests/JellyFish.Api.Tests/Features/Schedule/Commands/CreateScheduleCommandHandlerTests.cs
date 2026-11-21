using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.Schedule.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;
using System.Collections.Generic;


namespace JellyFish.Api.Tests.Features.Schedule.Commands;

public class CreateScheduleCommandHandlerTests
{
    private readonly Mock<IScheduleRepository> _schedules = new();
    private readonly Mock<IPatternRepository> _patterns = new();
    private readonly Mock<IScheduleNextEventService> _next = new();

    [Fact]
    public async Task Handle_Should_Create_When_Valid()
    {
        _schedules.Setup(r => r.ExistsByNameAsync("New", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _patterns.Setup(r => r.ExistsAsync("Home", "Sunrise", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _next.Setup(n => n.ComputeNextEventUtc("07:00", "* * *", "08:00", "* * *")).Returns(Result.Success(new ScheduleNextEventResult(DateTime.UtcNow.AddHours(1), "On")));
        _schedules.Setup(r => r.AddAsync(It.IsAny<JellyFish.Api.Domain.Schedule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var handler = new CreateScheduleCommandHandler(_schedules.Object, _patterns.Object, _next.Object);
        var res = await handler.Handle(new CreateScheduleCommand("New", "Home", "Sunrise", "Normal", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"}), default);

        res.IsSuccess.ShouldBeTrue();
        res.Value.Name.ShouldBe("New");
        _schedules.Verify(r => r.AddAsync(It.IsAny<JellyFish.Api.Domain.Schedule>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Pattern_Missing()
    {
        _schedules.Setup(r => r.ExistsByNameAsync("New", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _patterns.Setup(r => r.ExistsAsync("Home", "Missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new CreateScheduleCommandHandler(_schedules.Object, _patterns.Object, _next.Object);
        var res = await handler.Handle(new CreateScheduleCommand("New", "Home", "Missing", "Low", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"}), default);

        res.IsFailure.ShouldBeTrue();
        res.Error.Type.ShouldBe(ErrorType.NotFound);
    }
}

