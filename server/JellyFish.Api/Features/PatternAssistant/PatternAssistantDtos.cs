using JellyFish.Api.Features.Pattern;

namespace JellyFish.Api.Features.PatternAssistant;

/// <summary>A generated pattern, ready to preview (run with Data) or save (create + upsert details).</summary>
public sealed record GeneratedPatternDto(string SuggestedName, string Explanation, PatternDetailsDto Details)
{
    /// <summary>The Jellyfish animation name for the details, e.g. "Sequence with Fade" or "Chase + Twinkle".</summary>
    public string Animation { get; init; } = PatternAnimations.Describe(Details);
}

/// <summary>Whether the assistant can be used: the UI hides or disables it when not ready.</summary>
public sealed record PatternAssistantStatusDto(
    bool Enabled,
    string Model,
    bool Reachable,
    bool ModelInstalled,
    string? Message)
{
    public bool Ready => Enabled && Reachable && ModelInstalled;
}
