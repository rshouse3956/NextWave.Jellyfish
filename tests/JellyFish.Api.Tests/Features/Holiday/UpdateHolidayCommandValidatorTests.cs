using JellyFish.Api.Features.Holiday.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;

namespace JellyFish.Api.Tests.Features.Holiday;

public class UpdateHolidayCommandValidatorTests
{
    [Fact]
    public async Task Valid_Update_Passes_When_Exists()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ExistsByNameAsync("Existing", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new UpdateHolidayCommandValidator(repo.Object);

        var cmd = new UpdateHolidayCommand("Existing", "US Federal", "0 0 1 1 ?", "", true);
        var result = await validator.ValidateAsync(cmd);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Missing_Name_Fails()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new UpdateHolidayCommandValidator(repo.Object);

        var cmd = new UpdateHolidayCommand(" ", "US Federal", "0 0 1 1 ?", "", true);
        var result = await validator.ValidateAsync(cmd);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Not_Exists_Fails()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ExistsByNameAsync("Missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new UpdateHolidayCommandValidator(repo.Object);

        var cmd = new UpdateHolidayCommand("Missing", "US Federal", "0 0 1 1 ?", "", true);
        var result = await validator.ValidateAsync(cmd);
        Assert.False(result.IsValid);
    }
}

