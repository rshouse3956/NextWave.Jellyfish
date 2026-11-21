using JellyFish.Api.Common;
using JellyFish.Api.Features.Schedule.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using Shouldly;

namespace JellyFish.Api.Tests.Features.Schedule.Commands;

public class DeleteScheduleCommandHandlerTests
{
    private readonly Mock<IScheduleRepository> _schedules = new();

    [Fact]
    public async Task Handle_Should_Return_Result_From_Repo()
    {
        _schedules.Setup(r => r.DeleteAsync("Name", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var handler = new DeleteScheduleCommandHandler(_schedules.Object);
        var res = await handler.Handle(new DeleteScheduleCommand("Name"), default);
        res.IsSuccess.ShouldBeTrue();
    }
}

