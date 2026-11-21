using JellyFish.Api.Features.Holiday.Queries;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;

namespace JellyFish.Api.Tests.Features.Holiday;

public class IsHolidayQueryHandlerTests
{
    [Fact]
    public async Task Thanksgiving_2025_IsHoliday()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(JellyFish.Api.Common.Result.Success(new List<JellyFish.Api.Domain.Holiday>
        {
            JellyFish.Api.Domain.Holiday.Create("Thanksgiving Day", "US Federal", "0 0 ? 11 4#4", "", true).Value
        }));

        var handler = new IsHolidayQueryHandler(repo.Object);
        var res = await handler.Handle(new IsHolidayQuery(new DateOnly(2025, 11, 27)), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value.IsHoliday);
        Assert.Contains("Thanksgiving Day", res.Value.Matches);
    }

    [Fact]
    public async Task Random_NonHoliday_IsFalse()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(JellyFish.Api.Common.Result.Success(new List<JellyFish.Api.Domain.Holiday>
        {
            JellyFish.Api.Domain.Holiday.Create("Thanksgiving Day", "US Federal", "0 0 ? 11 4#4", "", true).Value
        }));

        var handler = new IsHolidayQueryHandler(repo.Object);
        var res = await handler.Handle(new IsHolidayQuery(new DateOnly(2025, 2, 19)), CancellationToken.None);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value.IsHoliday);
        Assert.Empty(res.Value.Matches);
    }
}

