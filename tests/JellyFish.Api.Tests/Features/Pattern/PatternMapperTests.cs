using JellyFish.Api.Domain;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.Pattern.Mappers;
using JellyFish.Api.Features.PatternAssistant;
using Shouldly;

namespace JellyFish.Api.Tests.Features.Pattern;

public class PatternMapperTests
{
    private static PatternDetails Domain() => new()
    {
        Colors = [250, 146, 51],
        Type = "Firework",
        EffectBetweenPixels = "No Color Transform",
        LedOnPos = new Dictionary<string, int[]> { ["0"] = [], ["1"] = [0] },
        RunData = new PatternRunData { Speed = 1, Brightness = 100, Effect = "No Effect", RgbAdj = [100, 100, 100] },
        Direction = "Left",
        FireworkSettings = new FireworkSettings { SpawnRate = 0, ColorPerLed = 2, FireworkSpeed = 50 }
    };

    [Fact]
    public void RoundTrip_KeepsControllerOnlyFields()
    {
        var back = PatternMapper.FromDetailsDto(PatternMapper.ToDetailsDto(Domain()));

        back.LedOnPos!["1"].ShouldBe([0]);
        var fw = back.FireworkSettings.ShouldNotBeNull();
        (fw.SpawnRate, fw.ColorPerLed, fw.FireworkSpeed).ShouldBe((0, 2, 50));
    }

    [Fact]
    public void RoundTrip_NullStaysNull()
    {
        var d = Domain();
        d.LedOnPos = null;
        d.FireworkSettings = null;

        var back = PatternMapper.FromDetailsDto(PatternMapper.ToDetailsDto(d));

        back.LedOnPos.ShouldBeNull();
        back.FireworkSettings.ShouldBeNull();
    }

    [Fact]
    public void Firework_IsAKnownType() =>
        PatternVocabulary.IsKnownType("Firework").ShouldBeTrue();      // the pattern editor accepts it
}
