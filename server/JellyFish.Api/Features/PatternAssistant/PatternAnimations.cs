using System.Text;
using System.Text.RegularExpressions;
using JellyFish.Api.Features.Pattern;

namespace JellyFish.Api.Features.PatternAssistant;

/// <summary>
/// An animation as Jellyfish names it (https://www.jellyfishlighting.com/support/animation), and how it maps to
/// the controller's fields. Several vendor animations are one controller type plus an effect between colors
/// (e.g. "Chase with Progression" = Chase + Progression).
/// </summary>
/// <param name="Name">The vendor's name, shown to users and the model.</param>
/// <param name="Type">Controller <c>type</c>.</param>
/// <param name="Effect">The <c>effectBetweenPixels</c> this animation implies, or null when any effect fits.</param>
/// <param name="Description">What it looks like, for the model (vendor description plus what we know).</param>
/// <param name="Triggers">
/// Words in a request that ask for this animation. Null for the general animations (Color, Chase), which the
/// motion rule handles; a trigger lets the assistant use a type the model wouldn't otherwise be trusted with.
/// </param>
/// <param name="Measured">
/// Whether its field behavior was checked on the lights. Unmeasured ones are described from the vendor page and
/// the controller's own patterns.
/// </param>
public sealed record PatternAnimation(
    string Name,
    string Type,
    string? Effect,
    string Description,
    Regex? Triggers,
    bool Measured);

/// <summary>An optional sparkle or flash the controller layers over any animation (<c>runData.effect</c>).</summary>
public sealed record PatternRunEffect(string Name, string Description, Regex Triggers);

/// <summary>
/// The animations the assistant can make, in one place: the request rules, the prompt and example selection all
/// read this table.
/// </summary>
public static class PatternAnimations
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static Regex Words(string pattern) => new($@"\b(?:{pattern})\b", Options);

    public const string Color = "Color";
    public const string Chase = "Chase";
    public const string Paint = "Paint";
    public const string MultiPaint = "Multi-Paint";
    public const string Sequence = "Sequence";
    public const string Stacker = "Stacker";
    public const string Firework = "Firework";

    /// <summary>
    /// Firework settings from "New Years Fireworks", saved from the Jellyfish app. Checked on the lights
    /// (2026-11-20): fireworkSpeed sets the pace of each burst, higher is faster (20 and 80 both work); spawnRate
    /// 0 to 5 and colorPerLed 1 to 4 made no visible change, so those stay as the app saved them.
    /// </summary>
    public static readonly FireworkSettingsDto DefaultFireworkSettings = new(SpawnRate: 0, ColorPerLed: 2, FireworkSpeed: 50);

    /// <summary>The fireworkSpeed range checked on the lights, and the step for "faster" or "slower".</summary>
    public const int MinFireworkSpeed = 20;
    public const int MaxFireworkSpeed = 80;
    public const int FireworkSpeedStep = 30;

    /// <summary>
    /// The vendor's animations. Order matters for <see cref="Detect"/>: more specific triggers come first
    /// (Multi-Paint before Paint, Sequence with Fade before Sequence).
    /// </summary>
    public static readonly IReadOnlyList<PatternAnimation> All =
    [
        new("Color", Color, null,
            "static: every light keeps its color, nothing moves. The default.",
            null, Measured: true),
        new("Chase", Chase, null,
            "the color blocks travel along the line, one light per step (skip moves more per step).",
            null, Measured: true),
        new("Chase with Progression", Chase, "Progression",
            "a chase whose colors blend into each other, so a moving gradient travels along the line.",
            null, Measured: true),
        new("Chase with Fill Black", Chase, "Fill with Black",
            "a chase with unlit gaps between the colors; a more subdued moving look.",
            null, Measured: true),
        new("Multi-Paint", MultiPaint, null,
            "like Paint, but the line is split into sections (numOfLeds lights each, usually 6-10) that all fill " +
            "at the same time, 2 lights per step.",
            Words(@"multi-?paint\w*|(?:sections?|segments?)\s+(?:that\s+)?(?:fill|paint)\w*|(?:fill|paint)\w*\s+(?:in|by)\s+(?:sections?|segments?)"),
            Measured: true),
        new("Paint", Paint, null,
            "fills the line from one end with a color until it is solid, then the next color. Direction Center " +
            "alternates ends (one color from the right, the next from the left); Left always fills from the left. " +
            "Controller Paint patterns use many colors (often with white between them), skip 2 and No Color Transform.",
            Words(@"paint\w*|wipe\w*|color\s+wipe|fill(?:s|ing)?\s+(?:up|in)|one\s+light\s+at\s+a\s+time"),
            Measured: true),
        new("Sequence with Fade", Sequence, "Fade",
            "the whole line shows one color, then dims and brightens softly into the next: a breathing effect. " +
            "The server sets the width and speed.",
            Words(@"breath\w*|puls\w*|throb\w*"),
            Measured: false),
        new("Sequence", Sequence, null,
            "the whole line shows one solid color, then switches to the next color each step. Width and lights " +
            "per color make no visible difference.",
            Words(@"sequenc\w*|cycl\w*|(?:switch|chang|flash|alternat)\w*\s+(?:between|through)"),
            Measured: true),
        new("Stacker", Stacker, null,
            "each color is sent from one end and stacks up at the other until the line is full, then the next " +
            "color stacks. The server sets its width, lights and speed, so only choose the colors (more colors " +
            "give a livelier stack).",
            Words(@"stack\w*|build(?:s|ing)?\s+up|pil(?:e|es|ing)\s+up"),
            Measured: false),
        new("Fireworks", Firework, "No Color Transform",
            "a white charge travels along the line and bursts into the colors, like a firework. Not in the API doc; " +
            "the server sets its fireworkSettings, skip, width and speed, so only choose the colors.",
            Words(@"firework\w*|burst\w*|explo\w*"),
            Measured: true),
    ];

    /// <summary>The controller types the assistant makes: the schema's type enum.</summary>
    public static readonly IReadOnlyList<string> Types = All.Select(a => a.Type).Distinct().ToList();

    /// <summary>
    /// Effects layered over the animation (<c>runData.effect</c>). The API doc says the field is unused, but
    /// controller patterns use these. <c>effectValue</c> is sent as 0 (one controller pattern uses 38; its meaning
    /// is unknown).
    /// </summary>
    public static readonly IReadOnlyList<PatternRunEffect> RunEffects =
    [
        new("Twinkle", "random lights sparkle over the pattern (the speed doesn't change the sparkle rate).",
            Words(@"twinkl\w*|sparkl\w*|glitter\w*|shimmer\w*")),
        new("Lightning", "bright flashes like lightning every couple of seconds (e.g. a storm). Asked for on its own, " +
            "the server makes it a slow blending chase like the app's Purple Lightning Storm.",
            Words(@"lightning|storm\w*|thunder\w*")),
    ];

    /// <summary>
    /// The animation a request asks for by name or description, or null when it only names colors (or general
    /// motion, which the motion rule handles).
    /// </summary>
    public static PatternAnimation? Detect(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : All.FirstOrDefault(a => a.Triggers?.IsMatch(text) == true);

    /// <summary>The run effect a request asks for, or null.</summary>
    public static PatternRunEffect? DetectRunEffect(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : RunEffects.FirstOrDefault(e => e.Triggers.IsMatch(text));

    /// <summary>The vendor name for a pattern's type and effect, e.g. Chase + Progression = "Chase with Progression".</summary>
    public static string Describe(string? type, string? effect)
    {
        var sameType = All.Where(a => Is(a.Type, type)).ToList();
        if (sameType.Count == 0) return string.IsNullOrWhiteSpace(type) ? Color : type.Trim();
        // A type with only an effect-specific entry (Fireworks) still gets its name with any effect.
        return (sameType.FirstOrDefault(a => a.Effect is not null && Is(a.Effect, effect))
                ?? sameType.FirstOrDefault(a => a.Effect is null)
                ?? sameType[0]).Name;
    }

    /// <summary>The vendor name plus any run effect, e.g. "Chase with Progression + Twinkle".</summary>
    public static string Describe(PatternDetailsDto d)
    {
        var name = Describe(d.Type, d.EffectBetweenPixels);
        var run = RunEffects.FirstOrDefault(e => Is(e.Name, d.RunData?.Effect));
        return run is null ? name : $"{name} + {run.Name}";
    }

    /// <summary>The animation list for the system prompt.</summary>
    public static string PromptSection()
    {
        var sb = new StringBuilder();
        sb.AppendLine("- type: how the colors behave. The animations, by their Jellyfish names:");
        foreach (var a in All)
        {
            sb.Append("    ").Append(a.Name).Append(" (type ").Append(a.Type);
            if (a.Effect is not null) sb.Append(", effectBetweenPixels ").Append(a.Effect);
            sb.Append("): ").AppendLine(a.Description);
        }
        sb.AppendLine("  Use Color unless the request asks for motion (chase, moving, running, flowing, scrolling,");
        sb.AppendLine("  animated) or describes one of the other animations; use Chase for general motion.");
        sb.AppendLine("  \"Fade\", \"fading\", \"blend\" and \"gradient\" describe how neighbouring colors meet, not motion:");
        sb.AppendLine("  they set effectBetweenPixels (Fade or Progression) and do not by themselves make the pattern move.");
        sb.AppendLine("  Breathing or pulsing means Sequence with Fade. Fireworks, bursts or exploding mean Fireworks.");
        sb.AppendLine("- runEffect: No Effect unless asked for. Twinkle = " + RunEffects[0].Description +
                      " Lightning = " + RunEffects[1].Description + " They work with any type.");
        return sb.ToString();
    }

    private static bool Is(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
