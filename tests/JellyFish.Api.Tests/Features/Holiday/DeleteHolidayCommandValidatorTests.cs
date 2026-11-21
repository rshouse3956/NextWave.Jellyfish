using JellyFish.Api.Features.Holiday.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;

namespace JellyFish.Api.Tests.Features.Holiday;

public class DeleteHolidayCommandValidatorTests
{
    [Fact]
    public async Task Delete_Passes_When_Exists()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ExistsByNameAsync("Existing", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new DeleteHolidayCommandValidator(repo.Object);

        var cmd = new DeleteHolidayCommand("Existing");
        var result = await validator.ValidateAsync(cmd);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Delete_Fails_When_Not_Exists()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ExistsByNameAsync("Missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new DeleteHolidayCommandValidator(repo.Object);

        var cmd = new DeleteHolidayCommand("Missing");
        var result = await validator.ValidateAsync(cmd);
        Assert.False(result.IsValid);
    }
}

