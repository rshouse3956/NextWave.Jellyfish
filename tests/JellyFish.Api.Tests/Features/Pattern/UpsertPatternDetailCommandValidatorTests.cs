using FluentValidation.TestHelper;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.Pattern.Commands;

namespace JellyFish.Api.Tests.Features.Pattern;

public class UpsertPatternDetailCommandValidatorTests
{
    private static PatternDetailsDto MakeDetailsDto() => new(
        Colors: new[] { 1, 2, 3 },
        SpaceBetweenPixels: 2,
        EffectBetweenPixels: "No Color Transform",
        ColorPos: new[] { -1 },
        SoffitZone: null,
        Cursor: -1,
        Type: "Chase",
        Skip: 0,
        NumOfLeds: 1,
        RunData: new PatternRunDataDto(0, 100, "No Effect", 0, new[] { 100, 100, 100 }),
        Direction: "Left"
    );

    [Fact]
    public async Task EmptyFolder_IsInvalid()
    {
        var validator = new UpsertPatternDetailCommandValidator();
        var result = await validator.TestValidateAsync(new UpsertPatternDetailCommand("", "N", MakeDetailsDto()));
        result.ShouldHaveValidationErrorFor(x => x.FolderName);
    }

    [Fact]
    public async Task EmptyName_IsInvalid()
    {
        var validator = new UpsertPatternDetailCommandValidator();
        var result = await validator.TestValidateAsync(new UpsertPatternDetailCommand("F", "", MakeDetailsDto()));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task NullDetails_IsInvalid()
    {
        var validator = new UpsertPatternDetailCommandValidator();
        var result = await validator.TestValidateAsync(new UpsertPatternDetailCommand("F", "N", null!));
        result.ShouldHaveValidationErrorFor(x => x.Details);
    }

    [Fact]
    public async Task ValidCommand_IsValid()
    {
        var validator = new UpsertPatternDetailCommandValidator();
        var result = await validator.TestValidateAsync(new UpsertPatternDetailCommand("F", "N", MakeDetailsDto()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}

