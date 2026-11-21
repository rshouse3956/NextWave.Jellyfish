using System.Text.Json;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.PatternAssistant;
using Shouldly;

namespace JellyFish.Api.Tests.Features.PatternAssistant;

public class PatternDetailsSanitizerTests
{
    private static AssistantPattern Pattern(
        IReadOnlyList<AssistantColor>? colors = null, string? type = "Chase", string? effect = "Fade",
        int space = 4, int skip = 1, int leds = 1, int speedLevel = 5, int brightness = 80,
        string? direction = "Center", string? name = "Test", string? explanation = "x") =>
        new(name, explanation, colors ?? [new(255, 0, 0)], type, effect, space, skip, leds, speedLevel, brightness, direction);

    [Theory]
    [InlineData(1, 100)]
    [InlineData(5, 25)]
    [InlineData(10, 1)]
    [InlineData(0, 100)]   // clamped to 1
    [InlineData(99, 1)]    // clamped to 10
    public void ToControllerSpeed_MapsLevels(int level, int expected) =>
        PatternDetailsSanitizer.ToControllerSpeed(level).ShouldBe(expected);

    [Theory]
    [InlineData(100, 1)]
    [InlineData(25, 5)]
    [InlineData(1, 10)]
    [InlineData(0, 10)]    // stored patterns use 0
    [InlineData(28, 5)]    // nearest (25 vs 35)
    [InlineData(500, 1)]
    public void ToSpeedLevel_FindsNearest(int speed, int expected) =>
        PatternDetailsSanitizer.ToSpeedLevel(speed).ShouldBe(expected);

    [Fact]
    public void SpeedLevels_RoundTrip()
    {
        for (var level = 1; level <= 10; level++)
            PatternDetailsSanitizer.ToSpeedLevel(PatternDetailsSanitizer.ToControllerSpeed(level)).ShouldBe(level);
    }

    [Fact]
    public void ToGenerated_MapsFields()
    {
        var g = PatternDetailsSanitizer.ToGenerated(Pattern(colors: [new(255, 0, 0), new(0, 0, 255)]));

        g.SuggestedName.ShouldBe("Test");
        var d = g.Details;
        d.Colors.ShouldBe(new[] { 255, 0, 0, 0, 0, 255 });
        d.Type.ShouldBe("Chase");
        d.EffectBetweenPixels.ShouldBe("Fade");
        d.Direction.ShouldBe("Center");
        d.SpaceBetweenPixels.ShouldBe(4);
        d.RunData.Speed.ShouldBe(25);
        d.RunData.Brightness.ShouldBe(80);
        d.RunData.Effect.ShouldBe("No Effect");
        d.RunData.EffectValue.ShouldBe(0);
        d.RunData.RgbAdj.ShouldBe(new[] { 100, 100, 100 });
    }

    [Fact]
    public void ToGenerated_ClampsAndNormalizes()
    {
        var g = PatternDetailsSanitizer.ToGenerated(Pattern(
            colors: [new(-5, 300, 128)], type: "multi-paint", effect: "fade", space: 999, skip: 0, leds: -3,
            brightness: 0, direction: "right"));

        var d = g.Details;
        d.Colors.ShouldBe(new[] { 0, 255, 128 });
        d.Type.ShouldBe("Multi-Paint");
        d.EffectBetweenPixels.ShouldBe("Fade");
        d.Direction.ShouldBe("Right");
        d.SpaceBetweenPixels.ShouldBe(AssistantPatternSchema.MaxSpacing);
        d.Skip.ShouldBe(1);
        d.NumOfLeds.ShouldBe(1);
        d.RunData.Brightness.ShouldBe(1);
    }

    // Measured on the controller: No Color Transform ignores the width (1 light per color); Repeat gives
    // hard-edged blocks of N; Fade uses N per color; Fill with Black uses N as the gap after each color.
    [Theory]
    [InlineData("No Color Transform", 4, "Repeat", 4)]
    [InlineData("No Color Transform", 1, "No Color Transform", 1)]
    [InlineData("No Color Transform", 0, "No Color Transform", 1)]
    [InlineData("Repeat", 0, "Repeat", 1)]
    [InlineData("Fade", 2, "Fade", 2)]
    [InlineData("Fade", 0, "Fade", 1)]
    [InlineData("Fill with Black", 0, "Fill with Black", 0)]
    [InlineData("Fill with Black", 8, "Fill with Black", 8)]
    public void ToGenerated_MapsEffectAndWidthToWhatTheLightsDo(string effect, int space, string expectedEffect, int expectedSpace)
    {
        var d = PatternDetailsSanitizer.ToGenerated(Pattern(effect: effect, space: space)).Details;
        d.EffectBetweenPixels.ShouldBe(expectedEffect);
        d.SpaceBetweenPixels.ShouldBe(expectedSpace);
    }

    private static PatternDetailsDto Apply(string type, string effect, int space, string prompt, PatternDetailsDto? current = null, params string[] history)
    {
        var g = PatternDetailsSanitizer.ToGenerated(Pattern(type: type, effect: effect, space: space));
        return PatternDetailsSanitizer.ApplyRequestRules(g, [prompt, .. history], current).Details;
    }

    [Theory]
    [InlineData("Fade", "a block of 3 red and 3 white", "Repeat")]            // not asked for: hard blocks
    [InlineData("Progression", "Christmas colors", "Repeat")]
    [InlineData("Fade", "A fading block of 2 green and 2 red", "Fade")]       // fade words: Fade
    [InlineData("Progression", "green and red fading", "Fade")]
    [InlineData("Fade", "soft BLEND of blue and white", "Progression")]       // blend words: Progression
    [InlineData("Fade", "rainbow across the roofline", "Progression")]
    [InlineData("Progression", "gradient fading from green to red", "Progression")] // both: keep the model's
    [InlineData("Repeat", "a fading look", "Repeat")]                         // never upgrades
    public void Effect_FollowsTheRequestWords(string effect, string prompt, string expected) =>
        Apply("Color", effect, 4, prompt).EffectBetweenPixels.ShouldBe(expected);

    [Fact]
    public void Effect_RefiningABlendedPatternKeepsIt() =>
        Apply("Color", "Fade", 4, "make it slower", current: PatternAssistantTestData.Details() with { EffectBetweenPixels = "Fade" })
            .EffectBetweenPixels.ShouldBe("Fade");

    [Fact]
    public void Effect_ReadsEarlierRequests() =>
        Apply("Color", "Fade", 4, "make it slower", null, "green and red fading").EffectBetweenPixels.ShouldBe("Fade");

    [Theory]
    [InlineData("Progression", 1, 4)]   // gradient needs room
    [InlineData("Progression", 10, 10)]
    [InlineData("Fade", 2, 2)]          // Fade at 2 was measured visible (static)
    public void Width_GradientMinimum(string effect, int space, int expected) =>
        Apply("Color", effect, space, "blend fading").SpaceBetweenPixels.ShouldBe(expected);

    [Theory]
    [InlineData("Sequence", "rainbow across the roofline", "Color")]          // the reported case
    [InlineData("Sequence", "rainbow moving along the roofline", "Chase")]
    [InlineData("Chase", "Christmas colors", "Color")]                        // no motion asked
    [InlineData("Chase", "slow red and white candy cane chase", "Chase")]
    [InlineData("Color", "green and red running to the right", "Color")]      // never upgrades
    [InlineData("Sequence", "a sequence of red, green and blue", "Sequence")] // named: allowed
    [InlineData("Stacker", "stacking lights", "Stacker")]
    [InlineData("Multi-Paint", "multi-paint in team colors", "Multi-Paint")]
    [InlineData("Paint", "paint it red", "Paint")]
    public void Type_FollowsTheRequestWords(string type, string prompt, string expected) =>
        Apply(type, "Repeat", 4, prompt).Type.ShouldBe(expected);

    [Fact]
    public void Type_RefineKeepsCurrentType() =>
        Apply("Sequence", "Repeat", 4, "make it slower", current: PatternAssistantTestData.Details(type: "Sequence"))
            .Type.ShouldBe("Sequence");

    [Fact]
    public void ReportedRainbow_BecomesAStillGradient()
    {
        // From the lights: rainbow came back as Sequence + Fade at width 1 (whole line one color, off, next color)
        var d = Apply("Sequence", "Fade", 1, "rainbow across the roofline");
        d.Type.ShouldBe("Color");
        d.EffectBetweenPixels.ShouldBe("Progression");
        d.SpaceBetweenPixels.ShouldBe(PatternDetailsSanitizer.MinGradientWidth);
    }

    [Fact]
    public void FromDetails_ShowsNoColorTransformAsWidthOne()
    {
        var stored = PatternAssistantTestData.Details() with { SpaceBetweenPixels = 14 }; // No Color Transform
        PatternDetailsSanitizer.FromDetails(stored).SpaceBetweenPixels.ShouldBe(1);
        PatternDetailsSanitizer.FromDetails(stored with { EffectBetweenPixels = "Fade" }).SpaceBetweenPixels.ShouldBe(14);
    }

    [Fact]
    public void ToGenerated_UnknownValuesFallBack()
    {
        var d = PatternDetailsSanitizer.ToGenerated(Pattern(type: "Sparkle", effect: "No Color Transformation", space: 1, direction: null)).Details;
        d.Type.ShouldBe(PatternDetailsSanitizer.DefaultType);
        d.EffectBetweenPixels.ShouldBe(PatternDetailsSanitizer.DefaultEffect);
        d.Direction.ShouldBe(PatternDetailsSanitizer.DefaultDirection);
    }

    [Fact]
    public void ToGenerated_CapsColorsAndDefaultsEmpty()
    {
        var many = Enumerable.Range(0, 40).Select(i => new AssistantColor(i, i, i)).ToList();
        PatternDetailsSanitizer.ToGenerated(Pattern(colors: many)).Details.Colors.Length.ShouldBe(PatternVocabulary.MaxColorValues);
        PatternDetailsSanitizer.ToGenerated(Pattern(colors: [])).Details.Colors.ShouldBe(new[] { 255, 255, 255 });
    }

    [Theory]
    [InlineData("Red/White \\ Chase", "Red White Chase")]
    [InlineData("   ", PatternDetailsSanitizer.DefaultName)]
    [InlineData(null, PatternDetailsSanitizer.DefaultName)]
    public void ToGenerated_CleansName(string? name, string expected) =>
        PatternDetailsSanitizer.ToGenerated(Pattern(name: name)).SuggestedName.ShouldBe(expected);

    [Fact]
    public void ToGenerated_TruncatesLongText()
    {
        var g = PatternDetailsSanitizer.ToGenerated(Pattern(name: new string('n', 100), explanation: new string('e', 1000)));
        g.SuggestedName.Length.ShouldBeLessThanOrEqualTo(AssistantPatternSchema.MaxNameLength);
        g.Explanation.Length.ShouldBeLessThanOrEqualTo(AssistantPatternSchema.MaxExplanationLength);
    }

    [Fact]
    public void ToGenerated_AlwaysPassesFullValidation()
    {
        var worst = Pattern(colors: [new(999, -1, 5)], type: null, effect: null, space: -1, skip: -1, leds: 0,
            speedLevel: -4, brightness: 400, direction: "Up", name: null, explanation: null);
        new PatternDetailsValidator().Validate(PatternDetailsSanitizer.ToGenerated(worst).Details).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void FromDetails_ConvertsToModelView()
    {
        var p = PatternDetailsSanitizer.FromDetails(PatternAssistantTestData.Details(speed: 1, colors: new[] { 1, 2, 3, 4, 5, 6, 7 }), "Name");
        p.SuggestedName.ShouldBe("Name");
        p.Colors!.Count.ShouldBe(2); // trailing partial triple dropped
        p.Colors[1].ShouldBe(new AssistantColor(4, 5, 6));
        p.SpeedLevel.ShouldBe(10);
    }

    [Fact]
    public void Schema_UsesVocabularyEnums()
    {
        var props = AssistantPatternSchema.Schema.GetProperty("properties");
        Enums(props, "type").ShouldBe(PatternAnimations.Types);   // the animation table, not every controller type
        Enums(props, "effectBetweenPixels").ShouldBe(PatternVocabulary.EffectsBetweenPixels);
        Enums(props, "direction").ShouldBe(PatternVocabulary.Directions);
        props.GetProperty("colors").GetProperty("maxItems").GetInt32().ShouldBe(PatternVocabulary.MaxColors);
    }

    private static string[] Enums(JsonElement props, string name) =>
        props.GetProperty(name).GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ToArray();
}
