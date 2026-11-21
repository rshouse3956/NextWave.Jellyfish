using System.Text.Json;
using JellyFish.Api.Features.Pattern;

namespace JellyFish.Api.Features.PatternAssistant;

/// <summary>
/// The pattern as the model sees and writes it. Simpler than <see cref="PatternDetailsDto"/>: colors are
/// {r,g,b} objects (no flat-array length mistakes), speed is a 1-10 slow-to-fast level (the controller's
/// speed is inverted), and the unused runData fields are left out. <see cref="PatternDetailsSanitizer"/>
/// converts it to and from the controller's format.
/// </summary>
public sealed record AssistantPattern(
    string? SuggestedName,
    string? Explanation,
    IReadOnlyList<AssistantColor>? Colors,
    string? Type,
    string? EffectBetweenPixels,
    int SpaceBetweenPixels,
    int Skip,
    int NumOfLeds,
    int SpeedLevel,
    int Brightness,
    string? Direction,
    string? RunEffect = null);

public sealed record AssistantColor(int R, int G, int B);

public static class AssistantPatternSchema
{
    public const int MaxNameLength = 40;
    public const int MaxExplanationLength = 300;
    public const int MaxSpacing = 50;
    public const int MinSpeedLevel = 1;
    public const int MaxSpeedLevel = 10;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// JSON schema sent to Ollama as the structured-output format, so every reply has this shape.
    /// Enum values come from <see cref="PatternVocabulary"/>; types from <see cref="PatternAnimations.Types"/>.
    /// </summary>
    public static readonly JsonElement Schema = JsonSerializer.SerializeToElement(new Dictionary<string, object>
    {
        ["type"] = "object",
        ["required"] = new[]
        {
            "suggestedName", "explanation", "colors", "type", "effectBetweenPixels", "spaceBetweenPixels",
            "skip", "numOfLeds", "speedLevel", "brightness", "direction", "runEffect"
        },
        ["properties"] = new Dictionary<string, object>
        {
            ["suggestedName"] = new { type = "string", maxLength = MaxNameLength },
            ["explanation"] = new { type = "string", maxLength = MaxExplanationLength },
            ["colors"] = new
            {
                type = "array",
                minItems = 1,
                maxItems = PatternVocabulary.MaxColors,
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["required"] = new[] { "r", "g", "b" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["r"] = Channel(),
                        ["g"] = Channel(),
                        ["b"] = Channel()
                    }
                }
            },
            ["type"] = new Dictionary<string, object> { ["enum"] = PatternAnimations.Types },
            ["effectBetweenPixels"] = new Dictionary<string, object> { ["enum"] = PatternVocabulary.EffectsBetweenPixels },
            ["spaceBetweenPixels"] = new { type = "integer", minimum = 0, maximum = MaxSpacing },
            ["skip"] = new { type = "integer", minimum = 1, maximum = MaxSpacing },
            ["numOfLeds"] = new { type = "integer", minimum = 1, maximum = MaxSpacing },
            ["speedLevel"] = new
            {
                type = "integer",
                minimum = MinSpeedLevel,
                maximum = MaxSpeedLevel,
                description = "1 = slowest, 10 = fastest"
            },
            ["brightness"] = new
            {
                type = "integer",
                minimum = 1,
                maximum = PatternVocabulary.MaxBrightness
            },
            ["direction"] = new Dictionary<string, object> { ["enum"] = PatternVocabulary.Directions },
            ["runEffect"] = new Dictionary<string, object> { ["enum"] = PatternVocabulary.RunEffects }
        }
    });

    private static object Channel() => new
    {
        type = "integer",
        minimum = PatternVocabulary.MinColorChannel,
        maximum = PatternVocabulary.MaxColorChannel
    };
}
