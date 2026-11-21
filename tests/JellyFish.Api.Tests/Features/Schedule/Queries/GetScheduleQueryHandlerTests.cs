using JellyFish.Api.Common;
using JellyFish.Api.Features.Schedule.Queries;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;
using System.Collections.Generic;
using DomainSchedule = JellyFish.Api.Domain.Schedule;

namespace JellyFish.Api.Tests.Features.Schedule.Queries;

public class GetScheduleQueryHandlerTests
{
    private readonly Mock<IScheduleRepository> _schedules = new();

    [Fact]
    public async Task Handle_Should_Return_Dto_When_Found()
    {
        var s = DomainSchedule.Create("Name", "Home", "P", "Normal", "07:00", "* * *", "08:00", "* * *", new List<string>{"Z1"}, null).Value;
        _schedules.Setup(r => r.GetByNameAsync("Name", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(s));

        var handler = new GetScheduleQueryHandler(_schedules.Object);
        var res = await handler.Handle(new GetScheduleQuery("Name"), default);

        res.IsSuccess.ShouldBeTrue();
        res.Value.Name.ShouldBe("Name");
    }
}

