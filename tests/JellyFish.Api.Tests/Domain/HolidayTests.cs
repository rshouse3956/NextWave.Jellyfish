using JellyFish.Api.Domain;
using JellyFish.Api.Common;

namespace JellyFish.Api.Tests.Domain;

public class HolidayTests
{
    [Fact]
    public void Create_Succeeds_WithValidData()
    {
        var result = Holiday.Create("Sample", "US Federal", "0 0 1 1 ?", "comment", true);
        Assert.True(result.IsSuccess);
        Assert.Equal("Sample", result.Value.Name);
        Assert.Equal("US Federal", result.Value.Category);
        Assert.Equal("0 0 1 1 ?", result.Value.CronExpression);
        Assert.True(result.Value.IsEnabled);
    }

    [Fact]
    public void Create_Fails_WhenCategoryInvalid()
    {
        var result = Holiday.Create("Sample", "InvalidCat", "0 0 1 1 ?", "comment", true);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_Fails_WhenNameMissing()
    {
        var result = Holiday.Create(" ", "US Federal", "0 0 1 1 ?", "comment", true);
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }
}

