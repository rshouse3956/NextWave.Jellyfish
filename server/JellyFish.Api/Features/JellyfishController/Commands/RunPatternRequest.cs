using JellyFish.Api.Features.Pattern;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class RunPatternRequest
{
    public int State { get; init; } = 1; // 0=off, 1=on
    public IReadOnlyList<string> ZoneNames { get; init; } = Array.Empty<string>();
    public PatternDetailsDto? Data { get; init; }
    // Optional: when activating from a specific schedule, include its name so UI can reflect it
    public string? ScheduleName { get; init; }
}
