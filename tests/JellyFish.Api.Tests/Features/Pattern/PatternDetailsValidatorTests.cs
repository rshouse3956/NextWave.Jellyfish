using FluentValidation.TestHelper;
using JellyFish.Api.Features.JellyfishController.Commands;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.Pattern.Commands;

namespace JellyFish.Api.Tests.Features.Pattern;

public class PatternDetailsValidatorTests
{
    private static PatternDetailsDto Valid() => new(
        Colors: new[] { 255, 0, 0, 255, 255, 255 },
        SpaceBetweenPixels: 2,
        EffectBetweenPixels: "No Color Transform",
        ColorPos: null,
        SoffitZone: null,
        Cursor: null,
        Type: "Chase",
        Skip: 1,
        NumOfLeds: 1,
        RunData: new PatternRunDataDto(10, 100, "No Effect", 0, new[] { 100, 100, 100 }),
        Direction: "Left"
    );

    private static TestValidationResult<PatternDetailsDto> Full(PatternDetailsDto d) =>
        new PatternDetailsValidator().TestValidate(d);

    private static TestValidationResult<PatternDetailsDto> Structure(PatternDetailsDto d) =>
        new PatternDetailsStructureValidator().TestValidate(d);

    [Fact]
    public void ValidDetails_Pass()
    {
        Full(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ZeroSpeedAndSkip_Pass()
    {
        // Existing stored/controller data uses 0 for speed and skip
        Full(Valid() with { Skip = 0, RunData = Valid().RunData with { Speed = 0 } }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyColors_Fail()
    {
        Full(Valid() with { Colors = Array.Empty<int>() }).ShouldHaveValidationErrorFor(x => x.Colors);
    }

    [Fact]
    public void ColorsNotMultipleOfThree_Fail()
    {
        Full(Valid() with { Colors = new[] { 255, 0 } }).ShouldHaveValidationErrorFor(x => x.Colors);
    }

    [Fact]
    public void MoreThanThirtyColors_Fail()
    {
        Full(Valid() with { Colors = Enumerable.Repeat(10, 93).ToArray() }).ShouldHaveValidationErrorFor(x => x.Colors);
    }

    [Fact]
    public void ThirtyColors_Pass()
    {
        Full(Valid() with { Colors = Enumerable.Repeat(10, 90).ToArray() }).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void ColorChannelOutOfRange_Fail(int value)
    {
        Full(Valid() with { Colors = new[] { 0, value, 0 } }).ShouldHaveValidationErrorFor("Colors[1]");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void BrightnessOutOfRange_Fail(int value)
    {
        Full(Valid() with { RunData = Valid().RunData with { Brightness = value } })
            .ShouldHaveValidationErrorFor(x => x.RunData.Brightness);
    }

    [Fact]
    public void NegativeSpeed_Fail()
    {
        Full(Valid() with { RunData = Valid().RunData with { Speed = -1 } })
            .ShouldHaveValidationErrorFor(x => x.RunData.Speed);
    }

    [Fact]
    public void RgbAdjWrongLength_Fail()
    {
        Full(Valid() with { RunData = Valid().RunData with { RgbAdj = new[] { 100, 100 } } })
            .ShouldHaveValidationErrorFor(x => x.RunData.RgbAdj);
    }

    [Fact]
    public void NegativeCounts_Fail()
    {
        var r = Full(Valid() with { SpaceBetweenPixels = -1, Skip = -1, NumOfLeds = -1 });
        r.ShouldHaveValidationErrorFor(x => x.SpaceBetweenPixels);
        r.ShouldHaveValidationErrorFor(x => x.Skip);
        r.ShouldHaveValidationErrorFor(x => x.NumOfLeds);
    }

    [Fact]
    public void NullRunData_Fail()
    {
        Full(Valid() with { RunData = null! }).ShouldHaveValidationErrorFor(x => x.RunData);
    }

    [Theory]
    [InlineData("Color")]
    [InlineData("multi-paint")]
    [InlineData(" Stacker ")]
    public void KnownTypes_Pass(string type)
    {
        Full(Valid() with { Type = type }).ShouldNotHaveValidationErrorFor(x => x.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sparkle")]
    public void UnknownType_Fail(string type)
    {
        Full(Valid() with { Type = type }).ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Theory]
    [InlineData("No Color Transform")]
    [InlineData("Fill with Black")]
    public void KnownEffects_Pass(string effect)
    {
        Full(Valid() with { EffectBetweenPixels = effect }).ShouldNotHaveValidationErrorFor(x => x.EffectBetweenPixels);
    }

    // "No Color Transformation" is the API doc's spelling, but the controller doesn't recognize it
    // (it silently falls back to no transform, like any unknown value).
    [Theory]
    [InlineData("Glitter")]
    [InlineData("No Color Transformation")]
    public void UnknownEffect_Fail(string effect)
    {
        Full(Valid() with { EffectBetweenPixels = effect }).ShouldHaveValidationErrorFor(x => x.EffectBetweenPixels);
    }

    [Theory]
    [InlineData("Left")]
    [InlineData("Right")]
    [InlineData("Center")]
    public void KnownDirections_Pass(string direction)
    {
        Full(Valid() with { Direction = direction }).ShouldNotHaveValidationErrorFor(x => x.Direction);
    }

    [Fact]
    public void UnknownDirection_Fail()
    {
        Full(Valid() with { Direction = "Up" }).ShouldHaveValidationErrorFor(x => x.Direction);
    }

    [Fact]
    public void StructureValidator_IgnoresVocabulary()
    {
        Structure(Valid() with { Type = "Undocumented", EffectBetweenPixels = "Other", Direction = "Up" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpsertCommand_UsesFullRules()
    {
        var v = new UpsertPatternDetailCommandValidator();
        var r = v.TestValidate(new UpsertPatternDetailCommand("F", "N", Valid() with { Type = "Sparkle", Colors = new[] { 1, 2 } }));
        r.ShouldHaveValidationErrorFor("Details.Type");
        r.ShouldHaveValidationErrorFor("Details.Colors");
    }

    [Fact]
    public void RunCommand_WithData_UsesStructureRulesOnly()
    {
        var v = new RunPatternCommandValidator();
        var zones = new[] { "Zone" };

        v.TestValidate(new RunPatternCommand("F", "N", 1, zones, Valid() with { Type = "Undocumented" }))
            .ShouldNotHaveAnyValidationErrors();

        v.TestValidate(new RunPatternCommand("F", "N", 1, zones, Valid() with { Colors = new[] { 1, 2 } }))
            .ShouldHaveValidationErrorFor("Data.Colors");
    }

    [Fact]
    public void RunCommand_WithoutData_IsValid()
    {
        new RunPatternCommandValidator()
            .TestValidate(new RunPatternCommand("F", "N", 1, new[] { "Zone" }, null))
            .ShouldNotHaveAnyValidationErrors();
    }
}
