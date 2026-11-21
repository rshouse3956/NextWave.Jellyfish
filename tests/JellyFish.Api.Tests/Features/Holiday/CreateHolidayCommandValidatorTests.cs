using JellyFish.Api.Features.Holiday.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;

namespace JellyFish.Api.Tests.Features.Holiday;

public class CreateHolidayCommandValidatorTests
{
    [Fact]
    public async Task Valid_Command_Passes()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ExistsByNameAsync("NewYear", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new CreateHolidayCommandValidator(repo.Object);

        var cmd = new CreateHolidayCommand("NewYear", "US Federal", "0 0 1 1 ?", "", true);
        var result = await validator.ValidateAsync(cmd);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Duplicate_Name_Fails()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ExistsByNameAsync("NewYear", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var validator = new CreateHolidayCommandValidator(repo.Object);

        var cmd = new CreateHolidayCommand("NewYear", "US Federal", "0 0 1 1 ?", "", true);
        var result = await validator.ValidateAsync(cmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateHolidayCommand.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Empty_Category_Fails(string category)
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new CreateHolidayCommandValidator(repo.Object);

        var cmd = new CreateHolidayCommand("Any", category, "0 0 1 1 ?", "", true);
        var result = await validator.ValidateAsync(cmd);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Invalid_Cron_Fails()
    {
        var repo = new Mock<IHolidayRepository>();
        repo.Setup(r => r.ExistsByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new CreateHolidayCommandValidator(repo.Object);

        var cmd = new CreateHolidayCommand("Any", "US Federal", "not a cron", "", true);
        var result = await validator.ValidateAsync(cmd);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateHolidayCommand.CronExpression));
    }
}

