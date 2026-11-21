using JellyFish.Api.Common;
using JellyFish.Api.Features.Schedule.Queries;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;
using System.Collections.Generic;
using DomainSchedule = JellyFish.Api.Domain.Schedule;

namespace JellyFish.Api.Tests.Features.Schedule.Queries;

public class ListSchedulesQueryHandlerTests
{
    private readonly Mock<IScheduleRepository> _schedules = new();

    [Fact]
    public async Task Handle_Should_Return_List()
    {
        var list = new List<DomainSchedule>
        {
            DomainSchedule.Create("A","Home","P1","Low","06:00","* * *","07:00","* * *", new List<string>{"Z1"}, null).Value,
            DomainSchedule.Create("B","Home","P2","High","20:00","* * *","21:00","* * *", new List<string>{"Z1"}, null).Value,
        };
        _schedules.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success(list));

        var handler = new ListSchedulesQueryHandler(_schedules.Object);
        var res = await handler.Handle(new ListSchedulesQuery(), default);

        res.IsSuccess.ShouldBeTrue();
        res.Value.Count.ShouldBe(2);
    }
}

