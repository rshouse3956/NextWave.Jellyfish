using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Features.Schedule.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;
using System.Collections.Generic;
using DomainSchedule = JellyFish.Api.Domain.Schedule;

namespace JellyFish.Api.Tests.Features.Schedule.Commands;

public class UpdateScheduleCommandHandlerTests
{
    private readonly Mock<IScheduleRepository> _schedules = new();
    private readonly Mock<IPatternRepository> _patterns = new();
    private readonly Mock<IScheduleNextEventService> _next = new();

    [Fact]
    public async Task Handle_Should_Update_When_Valid()
    {
        var existing = DomainSchedule.Create("Sched", "Home", "Sunrise", "Normal", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"}, null).Value;
        _schedules.Setup(r => r.GetByNameAsync("Sched", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(existing));
        _patterns.Setup(r => r.ExistsAsync("Home", "Sunset", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _next.Setup(n => n.ComputeNextEventUtc("20:00", "* * *", "21:00", "* * *"))
            .Returns(Result.Success(new ScheduleNextEventResult(DateTime.UtcNow.AddHours(2), "On")));
        _schedules.Setup(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var handler = new UpdateScheduleCommandHandler(_schedules.Object, _patterns.Object, _next.Object);
        var res = await handler.Handle(new UpdateScheduleCommand("Sched", "Home", "Sunset", "High", "20:00", "* * *", "21:00", "* * *", new List<string>{"Z1"}), default);

        res.IsSuccess.ShouldBeTrue();
        res.Value.PatternName.ShouldBe("Sunset");
        res.Value.Priority.ShouldBe("High");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_Schedule_Not_Found()
    {
        _schedules.Setup(r => r.GetByNameAsync("Missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<DomainSchedule>(Error.NotFound("missing")));

        var handler = new UpdateScheduleCommandHandler(_schedules.Object, _patterns.Object, _next.Object);
        var res = await handler.Handle(new UpdateScheduleCommand("Missing", "Home", "Sunrise", "Normal", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"}), default);

        res.IsFailure.ShouldBeTrue();
        res.Error.Type.ShouldBe(ErrorType.NotFound);
    }
}

