using JellyFish.Api.Features.PatternAssistant;
using JellyFish.Api.Features.PatternAssistant.Commands;
using Microsoft.Extensions.AI;
using Shouldly;
using static JellyFish.Api.Tests.Features.PatternAssistant.PatternAssistantTestData;

namespace JellyFish.Api.Tests.Features.PatternAssistant;

public class PatternAssistantPromptTests
{
    private static readonly PatternExample[] Candidates =
    [
        new("Christmas", "Christmas Tree", Details("Chase")),
        new("Christmas", "Candy Cane", Details("Color")),
        new("Halloween", "Spooky", Details("Paint")),
        new("Colors", "Orange", Details("Color")),
        new("Legacy", "Stack Attack", Details("Stacker")),
        new("Empty", "No Colors", Details("Sequence", colors: Array.Empty<int>())),
    ];

    [Fact]
    public void SelectExamples_PrefersKeywordMatches()
    {
        var picked = PatternAssistantPrompt.SelectExamples("a warm christmas look", Candidates, 2);
        picked.Select(p => p.FolderName).ShouldAllBe(f => f == "Christmas");
    }

    [Fact]
    public void SelectExamples_FillsWithDistinctTypes()
    {
        var picked = PatternAssistantPrompt.SelectExamples("something nice", Candidates, 10);
        picked.Select(p => p.Details.Type).ShouldBeUnique();
        picked.Select(p => p.Details.Type).ShouldBe(new[] { "Chase", "Color", "Paint", "Stacker" }, ignoreOrder: true);
    }

    [Fact]
    public void SelectExamples_SkipsPatternsWithoutColors_AndRespectsMax()
    {
        PatternAssistantPrompt.SelectExamples("no colors", Candidates, 10).ShouldNotContain(p => p.Name == "No Colors");
        PatternAssistantPrompt.SelectExamples("christmas", Candidates, 1).Count.ShouldBe(1);
        PatternAssistantPrompt.SelectExamples("christmas", Candidates, 0).ShouldBeEmpty();
    }

    [Fact]
    public void SelectExamples_PrefersControllerPatterns_OverAppMadeMatches()
    {
        PatternExample[] candidates =
        [
            new("Custom", "Green Red Fade", Details("Color"), FromController: false), // best keyword match, app-made
            new("Christmas", "Christmas Tree", Details("Chase")),
            new("Legacy", "Stack Attack", Details("Stacker")),
        ];

        var picked = PatternAssistantPrompt.SelectExamples("green red fade", candidates, 2);

        picked.ShouldAllBe(p => p.FromController);
    }

    [Fact]
    public void SelectExamples_AppMadeFillRemainingSlots()
    {
        PatternExample[] candidates =
        [
            new("Christmas", "Christmas Tree", Details("Chase")),
            new("Custom", "Green Red Fade", Details("Color"), FromController: false),
            new("Custom", "Another Chase", Details("Chase"), FromController: false),
        ];

        var picked = PatternAssistantPrompt.SelectExamples("green red fade", candidates, 3);

        picked.Select(p => p.Name).ShouldBe(new[] { "Christmas Tree", "Green Red Fade" });
    }

    [Fact]
    public void Build_NewPattern_HasSystemExamplesAndRequest()
    {
        var messages = PatternAssistantPrompt.Build(new GeneratePatternCommand("slow candy cane"), [Candidates[1]]);

        messages.Count.ShouldBe(2);
        messages[0].Role.ShouldBe(ChatRole.System);
        messages[0].Text.ShouldContain("Christmas / Candy Cane");
        messages[0].Text.ShouldContain("\"speedLevel\":");
        messages[1].Role.ShouldBe(ChatRole.User);
        messages[1].Text.ShouldBe("Create a pattern: slow candy cane");
    }

    [Fact]
    public void Build_Refine_IncludesCurrentPatternAndHistory()
    {
        var cmd = new GeneratePatternCommand("make it slower", Details(), "Candy Cane Chase", ["candy cane chase"]);
        var user = PatternAssistantPrompt.Build(cmd, []).Last().Text;

        user.ShouldContain("Earlier requests");
        user.ShouldContain("- candy cane chase");
        user.ShouldContain("Current pattern:");
        user.ShouldContain("\"suggestedName\":\"Candy Cane Chase\"");
        user.ShouldContain("keeping everything else the same: make it slower");
    }
}
