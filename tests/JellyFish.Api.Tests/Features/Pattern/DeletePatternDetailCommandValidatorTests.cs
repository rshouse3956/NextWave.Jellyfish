using FluentValidation.TestHelper;
using JellyFish.Api.Features.Pattern.Commands;

namespace JellyFish.Api.Tests.Features.Pattern;

public class DeletePatternDetailCommandValidatorTests
{
    [Fact]
    public async Task EmptyFolder_IsInvalid()
    {
        var validator = new DeletePatternDetailCommandValidator();
        var result = await validator.TestValidateAsync(new DeletePatternDetailCommand("", "N"));
        result.ShouldHaveValidationErrorFor(x => x.FolderName);
    }

    [Fact]
    public async Task EmptyName_IsInvalid()
    {
        var validator = new DeletePatternDetailCommandValidator();
        var result = await validator.TestValidateAsync(new DeletePatternDetailCommand("F", ""));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task Valid_IsValid()
    {
        var validator = new DeletePatternDetailCommandValidator();
        var result = await validator.TestValidateAsync(new DeletePatternDetailCommand("F", "N"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

