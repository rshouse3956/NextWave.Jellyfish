namespace JellyFish.Api.Features.Pattern;

/// <summary>
/// Allowed values and limits for Jellyfish pattern data, per the controller API documentation
/// (docs/Jellyfish Lighting API Documentation.pdf). Shared by validation and the pattern assistant.
/// </summary>
public static class PatternVocabulary
{
    /// <summary>Maximum number of colors in a pattern (30 RGB triples = 90 values).</summary>
    public const int MaxColors = 30;
    public const int MaxColorValues = MaxColors * 3;
    public const int MinColorChannel = 0;
    public const int MaxColorChannel = 255;

    public const int MinBrightness = 0;
    public const int MaxBrightness = 100;

    /// <summary>
    /// runData.effect when no effect is layered over the pattern. The API doc says the field is unused, but
    /// controller patterns also use <see cref="RunEffects"/>.
    /// </summary>
    public const string DefaultRunEffect = "No Effect";

    /// <summary>runData.effect values seen on the controller's own patterns (2026-11-20). It also stores "None".</summary>
    public static readonly IReadOnlyList<string> RunEffects = [DefaultRunEffect, "Twinkle", "Lightning"];
    public const int DefaultEffectValue = 0;
    public static readonly int[] DefaultRgbAdj = [100, 100, 100];

    /// <summary>
    /// The documented types, plus "Firework", which the API doc doesn't list but the Jellyfish app saves (with
    /// fireworkSettings). The pattern assistant only makes the types in its animation table
    /// (<c>PatternAnimations.Types</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> Types =
        ["Color", "Chase", "Paint", "Stacker", "Sequence", "Multi-Paint", "Firework"];

    /// <summary>
    /// The API doc lists "No Color Transformation", but its examples and the controller's own pattern data
    /// use "No Color Transform". Tested on the controller (2026-11-18): it treats any
    /// unrecognized value (including "No Color Transformation") as no transform, so only the known-good spelling
    /// is allowed.
    /// </summary>
    public static readonly IReadOnlyList<string> EffectsBetweenPixels =
        ["No Color Transform", "Repeat", "Progression", "Fade", "Fill with Black"];

    /// <summary>The API doc lists Left/Right; its examples also use Center.</summary>
    public static readonly IReadOnlyList<string> Directions = ["Left", "Right", "Center"];

    public static bool IsKnownType(string? value) => Contains(Types, value);
    public static bool IsKnownEffectBetweenPixels(string? value) => Contains(EffectsBetweenPixels, value);
    public static bool IsKnownDirection(string? value) => Contains(Directions, value);

    private static bool Contains(IReadOnlyList<string> allowed, string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        allowed.Any(a => string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase));
}
