using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.PatternAssistant;
using Shouldly;

namespace JellyFish.Api.Tests.Features.PatternAssistant;

public class PatternAnimationsTests
{
    private static AssistantPattern Pattern(string type, string effect, int space = 4, int leds = 1, string runEffect = "No Effect") =>
        new("Test", "x", [new(255, 0, 0), new(0, 255, 0)], type, effect, space, 1, leds, 5, 100, "Left", runEffect);

    private static GeneratedPatternDto Apply(AssistantPattern model, string prompt, PatternDetailsDto? current = null, params string[] history) =>
        PatternDetailsSanitizer.ApplyRequestRules(PatternDetailsSanitizer.ToGenerated(model), [prompt, .. history], current);

    private static PatternDetailsDto Current(string type, string effect, string runEffect = "No Effect") =>
        PatternAssistantTestData.Details(type: type) with
        {
            EffectBetweenPixels = effect,
            RunData = new PatternRunDataDto(10, 100, runEffect, 0, [100, 100, 100])
        };

    // --- detection ---

    [Theory]
    [InlineData("red and green slowly breathing", "Sequence with Fade")]
    [InlineData("pulsing purple and orange", "Sequence with Fade")]
    [InlineData("cycle through red, white and blue", "Sequence")]
    [InlineData("switch between green and gold", "Sequence")]
    [InlineData("a sequence of red, green and blue", "Sequence")]
    [InlineData("paint the roof in fall colors", "Paint")]
    [InlineData("a color wipe of blue then white", "Paint")]
    [InlineData("red, white and blue filling in sections", "Multi-Paint")]
    [InlineData("multi-paint in team colors", "Multi-Paint")]
    [InlineData("stack up red and green", "Stacker")]
    [InlineData("colors building up from the left", "Stacker")]
    public void Detect_FindsTheAnimationARequestDescribes(string prompt, string expected) =>
        PatternAnimations.Detect(prompt).ShouldNotBeNull().Name.ShouldBe(expected);

    [Theory]
    [InlineData("slow red and white candy cane chase")]   // general motion: the motion rule's job
    [InlineData("candy cane stripes")]
    [InlineData("alternating red and white")]             // static stripes, not a sequence
    [InlineData("warm white")]
    [InlineData("orange and purple fading into each other")]
    public void Detect_LeavesPlainRequestsAlone(string prompt) =>
        PatternAnimations.Detect(prompt).ShouldBeNull();

    // --- request rules ---

    [Theory]
    [InlineData("Color", "Repeat", "red and green slowly breathing", "Sequence", "Fade")]
    [InlineData("Chase", "Fade", "pulsing blue", "Sequence", "Fade")]
    [InlineData("Color", "Repeat", "cycle through red white and blue", "Sequence", "Repeat")]
    [InlineData("Chase", "Repeat", "paint the roof in fall colors", "Paint", "Repeat")]
    [InlineData("Paint", "Repeat", "red, white and blue filling in sections", "Multi-Paint", "Repeat")]
    [InlineData("Color", "Repeat", "stack up red and green", "Stacker", "No Color Transform")]   // Big Stacker's fields
    [InlineData("Chase", "Progression", "blending chase of blue and green", "Chase", "Progression")]
    [InlineData("Chase", "Fill with Black", "red chase with gaps", "Chase", "Fill with Black")]
    public void Rules_UseTheDescribedAnimation(string modelType, string modelEffect, string prompt, string type, string effect)
    {
        var d = Apply(Pattern(modelType, modelEffect), prompt).Details;
        d.Type.ShouldBe(type);
        d.EffectBetweenPixels.ShouldBe(effect);
    }

    [Theory]
    [InlineData("Sequence", "Fade", "red and green breathing", "Sequence with Fade")]
    [InlineData("Chase", "Progression", "blending chase", "Chase with Progression")]
    [InlineData("Chase", "Fill with Black", "chase with gaps", "Chase with Fill Black")]
    [InlineData("Chase", "Repeat", "candy cane chase", "Chase")]
    [InlineData("Color", "Repeat", "warm white", "Color")]
    public void Rules_NameTheAnimation(string modelType, string modelEffect, string prompt, string expected) =>
        Apply(Pattern(modelType, modelEffect), prompt).Animation.ShouldBe(expected);

    [Fact]
    public void Rules_NewestRequestWins_ChaseOverEarlierBreathing()
    {
        var d = Apply(Pattern("Chase", "Fade"), "make it a chase instead", Current("Sequence", "Fade"), "red and green breathing").Details;
        d.Type.ShouldBe("Chase");
    }

    [Fact]
    public void Rules_RefineWithoutAnimationWordsKeepsTheEarlierOne()
    {
        var d = Apply(Pattern("Sequence", "Fade"), "slower", Current("Sequence", "Fade"), "red and green breathing").Details;
        d.Type.ShouldBe("Sequence");
        d.EffectBetweenPixels.ShouldBe("Fade");
    }

    [Fact]
    public void Rules_StaticStopsTheMotion() =>
        Apply(Pattern("Chase", "Repeat"), "make it static", Current("Chase", "Repeat"), "red and white chase")
            .Details.Type.ShouldBe("Color");

    [Fact]
    public void Rules_NewChaseOverridesAnEarlierStatic() =>
        Apply(Pattern("Chase", "Repeat"), "now make it chase", Current("Color", "Repeat"), "static red and white")
            .Details.Type.ShouldBe("Chase");

    [Fact]
    public void Rules_NotMovingIsStatic() =>
        Apply(Pattern("Chase", "Repeat"), "red and white, not moving").Details.Type.ShouldBe("Color");

    [Theory]
    [InlineData(1, PatternDetailsSanitizer.DefaultMultiPaintSection)]
    [InlineData(10, 10)]
    public void Rules_MultiPaintGetsASectionSize(int leds, int expected) =>
        Apply(Pattern("Multi-Paint", "Repeat", leds: leds), "multi-paint in red and white").Details.NumOfLeds.ShouldBe(expected);

    // --- run effects ---

    [Theory]
    [InlineData("twinkling warm white", "No Effect", "Twinkle")]
    [InlineData("sparkly blue and white", "No Effect", "Twinkle")]
    [InlineData("purple lightning storm", "No Effect", "Lightning")]
    [InlineData("warm white", "Twinkle", "No Effect")]          // not asked for: the model can't add it
    [InlineData("warm white, no twinkle", "Twinkle", "No Effect")]
    public void RunEffect_FollowsTheRequest(string prompt, string modelRunEffect, string expected) =>
        Apply(Pattern("Color", "Repeat", runEffect: modelRunEffect), prompt).Details.RunData.Effect.ShouldBe(expected);

    [Fact]
    public void RunEffect_RefineKeepsIt() =>
        Apply(Pattern("Color", "Repeat", runEffect: "Twinkle"), "slower", Current("Color", "Repeat", "Twinkle"))
            .Details.RunData.Effect.ShouldBe("Twinkle");

    [Fact]
    public void RunEffect_RefineCanRemoveIt() =>
        Apply(Pattern("Color", "Repeat", runEffect: "Twinkle"), "remove the twinkle", Current("Color", "Repeat", "Twinkle"), "twinkling white")
            .Details.RunData.Effect.ShouldBe("No Effect");

    [Fact]
    public void RunEffect_AppearsInTheAnimationName() =>
        Apply(Pattern("Chase", "Progression"), "blending chase with twinkle").Animation.ShouldBe("Chase with Progression + Twinkle");

    [Theory]
    [InlineData("Twinkle", "Twinkle")]
    [InlineData("None", "No Effect")]     // the controller also stores "None"
    [InlineData("Sparkle", "No Effect")]
    public void FromDetails_ReadsTheRunEffect(string stored, string expected) =>
        PatternDetailsSanitizer.FromDetails(Current("Color", "Repeat", stored)).RunEffect.ShouldBe(expected);

    [Fact]
    public void ToGenerated_UnknownRunEffectIsNoEffect() =>
        PatternDetailsSanitizer.ToGenerated(Pattern("Color", "Repeat", runEffect: "Fireworks")).Details.RunData.Effect.ShouldBe("No Effect");

    // --- fireworks ---

    [Theory]
    [InlineData("gold and silver New Year's fireworks")]
    [InlineData("bursts of red and blue")]
    [InlineData("exploding colors for the 4th")]
    public void Detect_FindsFireworks(string prompt) =>
        PatternAnimations.Detect(prompt).ShouldNotBeNull().Type.ShouldBe("Firework");

    [Fact]
    public void Fireworks_UseTheSettingsCheckedOnTheLights()
    {
        var g = Apply(Pattern("Chase", "Repeat", space: 6, leds: 3), "gold and silver fireworks");
        var d = g.Details;
        d.Type.ShouldBe("Firework");
        d.FireworkSettings.ShouldBe(PatternAnimations.DefaultFireworkSettings);
        (d.EffectBetweenPixels, d.SpaceBetweenPixels, d.Skip, d.NumOfLeds, d.RunData.Speed, d.Direction)
            .ShouldBe(("No Color Transform", 2, 1, 1, 1, "Left"));
        d.Colors.ShouldBe([255, 0, 0, 0, 255, 0]);   // the model's colors are kept
        g.Animation.ShouldBe("Fireworks");
    }

    [Fact]
    public void Fireworks_NotAskedFor_FallBackWithoutSettings()
    {
        var d = Apply(Pattern("Firework", "No Color Transform"), "warm white").Details;
        d.Type.ShouldBe("Color");
        d.FireworkSettings.ShouldBeNull();
    }

    [Fact]
    public void Fireworks_RefineKeepsTheCurrentSettings()
    {
        var current = Current("Firework", "No Color Transform") with { FireworkSettings = new FireworkSettingsDto(0, 3, 80) };
        Apply(Pattern("Firework", "No Color Transform"), "use red and green instead", current, "fireworks")
            .Details.FireworkSettings.ShouldBe(new FireworkSettingsDto(0, 3, 80));
    }

    [Theory]
    [InlineData("fast gold fireworks", 80)]
    [InlineData("slow gold fireworks", 20)]
    [InlineData("gold fireworks", 50)]
    public void Fireworks_SpeedWordsMoveFireworkSpeed(string prompt, int expected) =>
        Apply(Pattern("Firework", "No Color Transform"), prompt).Details.FireworkSettings!.FireworkSpeed.ShouldBe(expected);

    [Theory]
    [InlineData(50, "make it faster", 80)]
    [InlineData(80, "make it faster", 80)]   // clamped to the range checked on the lights
    [InlineData(50, "slow it down", 20)]
    [InlineData(20, "slower please", 20)]
    [InlineData(80, "use red and green", 80)]
    public void Fireworks_RefineMovesSpeedWithinRange(int currentSpeed, string prompt, int expected)
    {
        var current = Current("Firework", "No Color Transform") with { FireworkSettings = new FireworkSettingsDto(0, 2, currentSpeed) };
        var fw = Apply(Pattern("Firework", "No Color Transform"), prompt, current, "fireworks").Details.FireworkSettings!;
        fw.FireworkSpeed.ShouldBe(expected);
        (fw.SpawnRate, fw.ColorPerLed).ShouldBe((0, 2));
    }

    // --- checked on the lights (2026-11-20) ---

    [Fact]
    public void Stacker_UsesBigStackerFields()
    {
        var d = Apply(Pattern("Stacker", "Repeat", space: 1, leds: 1), "stack up green and gold lights").Details;
        (d.Type, d.EffectBetweenPixels, d.SpaceBetweenPixels, d.Skip, d.NumOfLeds, d.RunData.Speed, d.Direction)
            .ShouldBe(("Stacker", "No Color Transform", 2, 1, 4, 1, "Left"));
        d.Colors.ShouldBe([255, 0, 0, 0, 255, 0]);
    }

    [Theory]
    [InlineData("stack up green and gold", 4)]
    [InlineData("slowly stack up green and gold", 2)]
    [InlineData("fast stacking green and gold", 8)]
    public void Stacker_SpeedWordsPickLightsPerStep(string prompt, int leds) =>
        Apply(Pattern("Stacker", "Repeat"), prompt).Details.NumOfLeds.ShouldBe(leds);

    [Theory]
    [InlineData(4, "make it faster", 8)]
    [InlineData(8, "faster", 8)]   // top of the scale
    [InlineData(4, "slower", 2)]
    [InlineData(2, "use red and blue", 2)]
    public void Stacker_RefineStepsLightsPerStep(int currentLeds, string prompt, int leds)
    {
        var current = Current("Stacker", "No Color Transform") with { NumOfLeds = currentLeds };
        Apply(Pattern("Stacker", "Repeat"), prompt, current, "stack up green and gold").Details.NumOfLeds.ShouldBe(leds);
    }

    [Theory]
    [InlineData("red and green breathing", 1)]
    [InlineData("red and green slowly breathing", 3)]
    public void Breathing_UsesTheMeasuredSpeeds(string prompt, int speed) =>
        Apply(Pattern("Sequence", "Fade"), prompt).Details.RunData.Speed.ShouldBe(speed);

    [Theory]
    [InlineData(3, "slower", 10)]
    [InlineData(10, "slower", 10)]
    [InlineData(3, "faster", 1)]
    [InlineData(50, "use blue and white", 10)]   // an old stored speed snaps to the nearest step
    public void Breathing_RefineStepsTheSpeed(int currentSpeed, string prompt, int speed)
    {
        var current = Current("Sequence", "Fade") with { RunData = new PatternRunDataDto(currentSpeed, 100, "No Effect", 0, [100, 100, 100]) };
        Apply(Pattern("Sequence", "Fade"), prompt, current, "red and green breathing").Details.RunData.Speed.ShouldBe(speed);
    }

    [Theory]
    [InlineData("red and green slowly breathing", "Fade")]
    [InlineData("a sequence of blue and green blending", "Progression")]
    public void Sequence_BlendGetsAFadeLongEnoughToSee(string prompt, string effect)
    {
        var d = Apply(Pattern("Sequence", effect, space: 1), prompt).Details;
        (d.Type, d.EffectBetweenPixels).ShouldBe(("Sequence", effect));
        d.SpaceBetweenPixels.ShouldBe(PatternDetailsSanitizer.MinSequenceBlendWidth);
    }

    [Fact]
    public void Sequence_WithoutBlendKeepsItsWidth() =>
        Apply(Pattern("Sequence", "Repeat", space: 1), "cycle through red, white and blue").Details.SpaceBetweenPixels.ShouldBe(1);

    [Fact]
    public void Lightning_OnItsOwnBecomesAStorm()
    {
        var g = Apply(Pattern("Color", "Repeat", space: 7, runEffect: "Lightning"), "purple lightning storm");
        var d = g.Details;
        (d.Type, d.EffectBetweenPixels, d.SpaceBetweenPixels, d.RunData.Speed, d.RunData.Effect)
            .ShouldBe(("Chase", "Progression", 7, PatternDetailsSanitizer.LightningStormSpeed, "Lightning"));
        g.Animation.ShouldBe("Chase with Progression + Lightning");
    }

    [Theory]
    [InlineData("static purple with lightning", "Color")]
    [InlineData("purple lightning breathing", "Sequence")]
    public void Lightning_KeepsAnAskedForAnimationOrStillness(string prompt, string type) =>
        Apply(Pattern("Color", "Repeat", runEffect: "Lightning"), prompt).Details.Type.ShouldBe(type);

    [Fact]
    public void Lightning_RefineOfAStaticLightningPatternStaysStatic()
    {
        var current = Current("Color", "Repeat", runEffect: "Lightning");
        Apply(Pattern("Color", "Repeat", runEffect: "Lightning"), "make it brighter", current, "purple with lightning")
            .Details.Type.ShouldBe("Color");
    }

    [Fact]
    public void Fireworks_RefineToAChaseDropsTheSettings()
    {
        var current = Current("Firework", "No Color Transform") with { FireworkSettings = PatternAnimations.DefaultFireworkSettings };
        var d = Apply(Pattern("Chase", "Repeat"), "make it a chase instead", current, "fireworks").Details;
        d.Type.ShouldBe("Chase");
        d.FireworkSettings.ShouldBeNull();
    }

    [Theory]
    [InlineData("Firework", "Repeat", "Fireworks")]          // only an effect-specific entry: still named
    [InlineData("Firework", "No Color Transform", "Fireworks")]
    [InlineData("Soffit", "No Color Transform", "Soffit")]   // not in the table: the type itself
    public void Describe_NamesEveryType(string type, string effect, string expected) =>
        PatternAnimations.Describe(type, effect).ShouldBe(expected);

    [Fact]
    public void Fireworks_ExampleIsShownFirst() =>
        PatternAssistantPrompt.SelectExamples("new year's fireworks", Candidates, 2)[0].Name.ShouldBe("New Years Fireworks");

    // --- prompt and schema ---

    [Fact]
    public void Prompt_ListsEveryAnimationAndRunEffect()
    {
        var prompt = PatternAssistantPrompt.SystemInstructions;
        prompt.ShouldNotContain("{animations}");
        foreach (var a in PatternAnimations.All) prompt.ShouldContain(a.Name + " (type " + a.Type);
        prompt.ShouldContain("runEffect");
        prompt.ShouldContain("Lightning");
    }

    [Fact]
    public void Schema_AllowsTheRunEffects() =>
        AssistantPatternSchema.Schema.GetProperty("properties").GetProperty("runEffect").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()).ShouldBe(PatternVocabulary.RunEffects);

    // --- examples ---

    private static PatternExample Example(string folder, string name, string type, string effect = "Repeat", bool fromController = true) =>
        new(folder, name, PatternAssistantTestData.Details(type: type) with { EffectBetweenPixels = effect }, fromController);

    private static readonly PatternExample[] Candidates =
    [
        Example("Christmas", "Christmas Tree", "Chase"),
        Example("Christmas", "Candy Cane", "Color"),
        Example("Legacy", "19-Seq Red White Blu", "Sequence"),
        Example("Legacy", "22-Seq Red Blue Gree", "Sequence", "Progression"),
        Example("Legacy", "25-Paint Red Orange", "Paint"),
        Example("Accent", "Soft White", "Soffit"),
        Example("New Years", "New Years Fireworks", "Firework", "No Color Transform"),
    ];

    [Fact]
    public void Examples_ShowTheRequestedAnimationFirst()
    {
        var picked = PatternAssistantPrompt.SelectExamples("christmas colors painting the roof", Candidates, 2);
        picked[0].Details.Type.ShouldBe("Paint");
        picked.Count.ShouldBe(2);
    }

    [Fact]
    public void Examples_LimitTheRequestedAnimationToTwo()
    {
        var picked = PatternAssistantPrompt.SelectExamples("cycle through colors", Candidates, 4);
        picked.Count(p => p.Details.Type == "Sequence").ShouldBe(PatternAssistantPrompt.MaxAnimationExamples);
        picked.Count.ShouldBe(4);
    }

    [Fact]
    public void Examples_SkipTypesTheAssistantDoesNotMake() =>
        PatternAssistantPrompt.SelectExamples("soft white accent", Candidates, 10).ShouldNotContain(p => p.Details.Type == "Soffit");
}
