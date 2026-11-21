using System.Text.RegularExpressions;
using JellyFish.Api.Features.Pattern;

namespace JellyFish.Api.Features.PatternAssistant;

/// <summary>
/// Converts between the model's <see cref="AssistantPattern"/> and the controller's <see cref="PatternDetailsDto"/>,
/// clamping every value into range and normalizing enum values. Whatever the model returns, the result passes
/// <see cref="PatternDetailsValidator"/>.
/// </summary>
public static class PatternDetailsSanitizer
{
    public const string DefaultName = "AI Pattern";
    public const string DefaultType = "Color";
    public const string DefaultEffect = "No Color Transform";
    public const string DefaultDirection = "Left";

    /// <summary>
    /// Controller speed for each speed level 1 (slowest) to 10 (fastest). The controller runs at 1/speed, so
    /// smaller is faster. A starting point: tune by watching the lights.
    /// </summary>
    private static readonly int[] SpeedByLevel = [100, 75, 50, 35, 25, 15, 10, 5, 3, 1];

    public static int ToControllerSpeed(int speedLevel) =>
        SpeedByLevel[Math.Clamp(speedLevel, AssistantPatternSchema.MinSpeedLevel, AssistantPatternSchema.MaxSpeedLevel) - 1];

    /// <summary>The nearest speed level for a controller speed. 0 or less (seen in stored patterns) counts as fastest.</summary>
    public static int ToSpeedLevel(int controllerSpeed)
    {
        if (controllerSpeed <= 1) return AssistantPatternSchema.MaxSpeedLevel;
        var best = 0;
        for (var i = 1; i < SpeedByLevel.Length; i++)
        {
            if (Math.Abs(SpeedByLevel[i] - controllerSpeed) < Math.Abs(SpeedByLevel[best] - controllerSpeed)) best = i;
        }
        return best + 1;
    }

    public static GeneratedPatternDto ToGenerated(AssistantPattern p)
    {
        var colors = (p.Colors ?? [])
            .Take(PatternVocabulary.MaxColors)
            .SelectMany(c => new[] { Channel(c.R), Channel(c.G), Channel(c.B) })
            .ToArray();
        if (colors.Length == 0) colors = [255, 255, 255];

        var (effect, space) = NormalizeSpacing(
            Canonical(PatternVocabulary.EffectsBetweenPixels, p.EffectBetweenPixels, DefaultEffect),
            Math.Clamp(p.SpaceBetweenPixels, 0, AssistantPatternSchema.MaxSpacing));

        var details = new PatternDetailsDto(
            Colors: colors,
            SpaceBetweenPixels: space,
            EffectBetweenPixels: effect,
            ColorPos: [],
            SoffitZone: null,
            Cursor: null,
            Type: Canonical(PatternAnimations.Types, p.Type, DefaultType),
            Skip: Math.Clamp(p.Skip, 1, AssistantPatternSchema.MaxSpacing),
            NumOfLeds: Math.Clamp(p.NumOfLeds, 1, AssistantPatternSchema.MaxSpacing),
            RunData: new PatternRunDataDto(
                Speed: ToControllerSpeed(p.SpeedLevel),
                Brightness: Math.Clamp(p.Brightness, 1, PatternVocabulary.MaxBrightness),
                Effect: Canonical(PatternVocabulary.RunEffects, p.RunEffect, PatternVocabulary.DefaultRunEffect),
                EffectValue: PatternVocabulary.DefaultEffectValue,
                RgbAdj: [.. PatternVocabulary.DefaultRgbAdj]),
            Direction: Canonical(PatternVocabulary.Directions, p.Direction, DefaultDirection));

        return new GeneratedPatternDto(CleanName(p.SuggestedName), CleanText(p.Explanation, AssistantPatternSchema.MaxExplanationLength), details);
    }

    /// <summary>The model's view of a stored or current pattern (examples and refine requests).</summary>
    public static AssistantPattern FromDetails(PatternDetailsDto d, string? name = null) => new(
        SuggestedName: name,
        Explanation: null,
        Colors: (d.Colors ?? [])
            .Chunk(3)
            .Where(c => c.Length == 3)
            .Select(c => new AssistantColor(c[0], c[1], c[2]))
            .ToArray(),
        Type: d.Type,
        EffectBetweenPixels: d.EffectBetweenPixels,
        // The controller ignores the width with No Color Transform; show what it actually draws.
        SpaceBetweenPixels: IsEffect(d.EffectBetweenPixels, NoColorTransform) ? 1 : d.SpaceBetweenPixels,
        Skip: d.Skip,
        NumOfLeds: d.NumOfLeds,
        SpeedLevel: ToSpeedLevel(d.RunData?.Speed ?? 0),
        Brightness: d.RunData?.Brightness ?? PatternVocabulary.MaxBrightness,
        Direction: d.Direction,
        // The controller also stores "None"; anything unknown reads as no effect.
        RunEffect: Canonical(PatternVocabulary.RunEffects, d.RunData?.Effect, PatternVocabulary.DefaultRunEffect));

    private const RegexOptions WordOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex FadeWords = new(@"\bfad\w*", WordOptions);
    private static readonly Regex BlendWords = new(@"\b(blend\w*|gradient\w*|rainbow\w*|spectrum\w*|ombre|transition\w*|progression)\b", WordOptions);
    private static readonly Regex FasterWords = new(@"\b(fast\w*|quick\w*|rapid\w*|speed\s+(it\s+)?up)\b", WordOptions);
    private static readonly Regex SlowerWords = new(@"\b(slow\w*|calm\w*|gentl\w*)\b", WordOptions);
    private static readonly Regex MotionWords = new(@"\b(chas\w*|mov\w*|run\w*|flow\w*|scroll\w*|animat\w*|travel\w*)\b", WordOptions);
    private static readonly Regex StaticWords = new(@"\b(static|stationary|(not|stop|no longer)\s+mov\w*|no\s+(motion|movement))\b", WordOptions);
    private static readonly Regex RemoveWords = new(@"\b(no|without|remove|stop|drop|lose|turn\s+off|get\s+rid\s+of|take\s+(out|off|away))\b(\s+\w+){0,2}\s*$", WordOptions);

    /// <summary>Progression needs a few lights per color to show a gradient (1 light per color shows none).</summary>
    public const int MinGradientWidth = 4;

    /// <summary>Multi-Paint section size when the model leaves it at 1; the controller's Multi-Paint patterns use 6.</summary>
    public const int DefaultMultiPaintSection = 6;

    /// <summary>
    /// Width for a Sequence with Fade or Progression. A breathing Sequence with width 1 switched colors without
    /// dimming on the lights (2026-11-20); the controller's blended Sequence patterns use 10.
    /// </summary>
    public const int MinSequenceBlendWidth = 10;

    /// <summary>
    /// Lightning asked for on its own gets the app's "Purple Lightning Storm" look (a slow Chase with
    /// Progression, width 7, speed 2): on the lights the static version looked flat next to it.
    /// </summary>
    public const int LightningStormWidth = 7;
    public const int LightningStormSpeed = 2;

    /// <summary>
    /// Deterministic rules applied to the model's choice, from the user's own words. The model tends to copy type
    /// and effect from stored examples, and prompt wording alone kept flipping. Requests are read newest first
    /// (this request, then earlier ones), so "make it a chase" overrides an earlier "breathing".
    /// <list type="bullet">
    /// <item>Type: an animation the request describes (<see cref="PatternAnimations.Detect"/>, e.g. "breathing" =
    /// Sequence with Fade) sets the type, and its effect if it implies one. Otherwise Color unless motion is
    /// asked for (then Chase); Sequence, Paint, Stacker and Multi-Paint are not used unless described. A refine
    /// keeps the current pattern's type.</item>
    /// <item>Effect: Fade/Progression only when fading or blending is asked for (or the refined pattern has one),
    /// otherwise Repeat. "Fade" words mean Fade (dims between colors); blend, gradient and rainbow mean
    /// Progression (passes through the colors in between).</item>
    /// <item>Progression gets at least <see cref="MinGradientWidth"/> lights per color; Multi-Paint gets a section
    /// size of at least 2 (<see cref="DefaultMultiPaintSection"/> when unset).</item>
    /// <item>Run effect (Twinkle, Lightning) only when asked for, or kept from the refined pattern unless the
    /// request removes it. Newly asked-for Lightning with no animation or stillness asked for becomes a
    /// lightning storm (Chase with Progression).</item>
    /// <item>A blended Sequence gets at least <see cref="MinSequenceBlendWidth"/>; Stacker and Firework use the
    /// fields of the app patterns that work on the lights.</item>
    /// </list>
    /// </summary>
    public static GeneratedPatternDto ApplyRequestRules(GeneratedPatternDto generated, IEnumerable<string?> requestTexts, PatternDetailsDto? current)
    {
        var texts = requestTexts.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!).ToList();
        var text = string.Join('\n', texts);
        var d = generated.Details;

        // --- type ---
        var type = d.Type;
        var requested = RequestedAnimation(texts);
        if (requested is not null)
        {
            type = requested.Type;
        }
        else
        {
            var keepType = current is not null && string.Equals(current.Type, type, StringComparison.OrdinalIgnoreCase);
            var motion = AskedForMotion(texts);
            if (texts.Count > 0 && StaticWords.IsMatch(texts[0]))
            {
                type = PatternAnimations.Color;   // "make it static" in this request
            }
            else if (!keepType)
            {
                if (IsType(type, PatternAnimations.Chase))
                {
                    if (!motion) type = PatternAnimations.Color;
                }
                else if (!IsType(type, PatternAnimations.Color))
                {
                    type = motion ? PatternAnimations.Chase : PatternAnimations.Color;
                }
            }
        }

        // --- effect ---
        var effect = d.EffectBetweenPixels;
        var space = d.SpaceBetweenPixels;
        if (IsEffect(effect, "Fade") || IsEffect(effect, "Progression"))
        {
            var fade = FadeWords.IsMatch(text);
            var blend = BlendWords.IsMatch(text);
            var currentBlended = current is not null && (IsEffect(current.EffectBetweenPixels, "Fade") || IsEffect(current.EffectBetweenPixels, "Progression"));

            if (!fade && !blend && !currentBlended) effect = Repeat;
            else if (IsEffect(effect, "Fade") && blend && !fade) effect = "Progression";
            else if (IsEffect(effect, "Progression") && fade && !blend) effect = "Fade";
        }
        // An animation that implies an effect ("breathing" = Sequence with Fade) sets it, unless the request
        // also asks for blending (a breathing gradient keeps Progression).
        if (requested?.Effect is { } implied && !(IsEffect(effect, "Progression") && BlendWords.IsMatch(text)))
            effect = implied;
        if (IsEffect(effect, "Progression")) space = Math.Max(MinGradientWidth, space);
        if (IsEffect(effect, Repeat) || IsEffect(effect, "Fade")) space = Math.Max(1, space);
        if (IsType(type, PatternAnimations.Sequence) && (IsEffect(effect, "Fade") || IsEffect(effect, "Progression")))
            space = Math.Max(MinSequenceBlendWidth, space);

        // --- Multi-Paint section size ---
        var numOfLeds = d.NumOfLeds;
        if (IsType(type, PatternAnimations.MultiPaint) && numOfLeds < 2) numOfLeds = DefaultMultiPaintSection;

        // --- run effect (Twinkle, Lightning) ---
        var runEffect = RequestedRunEffect(texts, current, d.RunData.Effect);
        var speed = d.RunData.Speed;
        var newLightning = IsEffect(runEffect, Lightning) && !IsEffect(current?.RunData?.Effect, Lightning);
        if (newLightning && requested is null && IsType(type, PatternAnimations.Color) && !texts.Any(StaticWords.IsMatch))
        {
            type = PatternAnimations.Chase;
            effect = "Progression";
            space = Math.Max(LightningStormWidth, space);
            speed = LightningStormSpeed;
        }

        var details = d with
        {
            Type = type,
            EffectBetweenPixels = effect,
            SpaceBetweenPixels = space,
            NumOfLeds = numOfLeds,
            RunData = d.RunData with { Effect = runEffect, Speed = speed },
            FireworkSettings = null
        };
        var newest = texts.Count > 0 ? texts[0] : string.Empty;
        if (IsType(type, PatternAnimations.Stacker)) details = AsStacker(details, current, newest);
        if (IsBlendedSequence(details)) details = AsBreathing(details, current, newest);
        if (IsType(type, PatternAnimations.Firework)) details = AsFirework(details, current, texts);
        return generated with { Details = details, Animation = PatternAnimations.Describe(details) };
    }

    /// <summary>
    /// A Firework uses the fields of the one checked on the lights ("New Years Fireworks"): only the colors, name,
    /// brightness and run effect come from the model. A refine keeps the current pattern's fireworkSettings;
    /// "faster" or "slower" in this request moves fireworkSpeed (higher is faster) by a step within the range
    /// checked on the lights.
    /// </summary>
    private static PatternDetailsDto AsFirework(PatternDetailsDto d, PatternDetailsDto? current, IReadOnlyList<string> newestFirst)
    {
        var settings = (IsType(current?.Type, PatternAnimations.Firework) ? current!.FireworkSettings : null)
                       ?? PatternAnimations.DefaultFireworkSettings;
        var newest = newestFirst.Count > 0 ? newestFirst[0] : string.Empty;
        var step = (FasterWords.IsMatch(newest), SlowerWords.IsMatch(newest)) switch
        {
            (true, false) => PatternAnimations.FireworkSpeedStep,
            (false, true) => -PatternAnimations.FireworkSpeedStep,
            _ => 0
        };
        if (step != 0)
        {
            settings = settings with
            {
                FireworkSpeed = Math.Clamp(settings.FireworkSpeed + step, PatternAnimations.MinFireworkSpeed, PatternAnimations.MaxFireworkSpeed)
            };
        }

        return d with
        {
            Type = PatternAnimations.Firework,
            EffectBetweenPixels = NoColorTransform,
            SpaceBetweenPixels = 2,
            Skip = 1,
            NumOfLeds = 1,
            Direction = "Left",
            RunData = d.RunData with { Speed = 1 },
            FireworkSettings = settings
        };
    }

    /// <summary>
    /// Stacker numOfLeds, slowest first. Measured on the lights (2026-11-20, Front zone, speed 1): 2 leaves about
    /// 5 s between stacked lights, 4 about 1 s. 8 is a guess at faster still. Speed only sets how fast each light
    /// travels (1 crosses in under a second, 20 takes about 30 s, 0 doesn't run), and skip makes it jump, so
    /// the lights-per-step setting is the speed control.
    /// </summary>
    public static readonly int[] StackerLedsSlowToFast = [2, 4, 8];
    public const int DefaultStackerStep = 1;

    /// <summary>
    /// Speed for a breathing (blended) Sequence, slowest first. Measured on the lights (2026-11-20, width 10):
    /// speed 10 cycles in about 10 s with visible steps, 3 in about 6 s, 1 in about 3 s and smooth. 50 took about
    /// 35 s and stepped visibly, so the general speed levels don't fit this animation.
    /// </summary>
    public static readonly int[] BreathingSpeedSlowToFast = [10, 3, 1];
    public const int DefaultBreathingStep = 2;

    /// <summary>
    /// A Stacker uses the fields of the app's "Big Stacker" (the assistant's first Stacker, width 1, 1 light,
    /// speed 10, showed static color). Its pace comes from <see cref="StackerLedsSlowToFast"/>, not the model.
    /// </summary>
    private static PatternDetailsDto AsStacker(PatternDetailsDto d, PatternDetailsDto? current, string newest) => d with
    {
        EffectBetweenPixels = NoColorTransform,
        SpaceBetweenPixels = 2,
        Skip = 1,
        NumOfLeds = Step(StackerLedsSlowToFast, DefaultStackerStep,
            IsType(current?.Type, PatternAnimations.Stacker) ? current!.NumOfLeds : null, newest),
        Direction = "Left",
        RunData = d.RunData with { Speed = 1 }
    };

    /// <summary>A breathing Sequence's pace comes from <see cref="BreathingSpeedSlowToFast"/>, not the model.</summary>
    private static PatternDetailsDto AsBreathing(PatternDetailsDto d, PatternDetailsDto? current, string newest) => d with
    {
        RunData = d.RunData with
        {
            Speed = Step(BreathingSpeedSlowToFast, DefaultBreathingStep,
                current is not null && IsBlendedSequence(current) ? current.RunData?.Speed : null, newest)
        }
    };

    private static bool IsBlendedSequence(PatternDetailsDto d) =>
        IsType(d.Type, PatternAnimations.Sequence) && (IsEffect(d.EffectBetweenPixels, "Fade") || IsEffect(d.EffectBetweenPixels, "Progression"));

    /// <summary>
    /// A value from a measured scale (slowest first): the current pattern's nearest step on a refine, otherwise the
    /// default, moved one step by "faster" or "slower" in the newest request.
    /// </summary>
    private static int Step(int[] slowToFast, int defaultIndex, int? currentValue, string newest)
    {
        var i = defaultIndex;
        if (currentValue is { } v)
        {
            i = 0;
            for (var j = 1; j < slowToFast.Length; j++)
                if (Math.Abs(slowToFast[j] - v) < Math.Abs(slowToFast[i] - v)) i = j;
        }
        var (faster, slower) = (FasterWords.IsMatch(newest), SlowerWords.IsMatch(newest));
        if (faster && !slower) i++;
        else if (slower && !faster) i--;
        return slowToFast[Math.Clamp(i, 0, slowToFast.Length - 1)];
    }

    /// <summary>
    /// The newest request that describes an animation decides it; a newer request that only asks for general
    /// motion (or for it to stop) ends the search, so it isn't overridden by an older one.
    /// </summary>
    private static PatternAnimation? RequestedAnimation(IReadOnlyList<string> newestFirst)
    {
        foreach (var t in newestFirst)
        {
            var a = PatternAnimations.Detect(t);
            if (a is not null) return a;
            if (MotionWords.IsMatch(t) || StaticWords.IsMatch(t)) return null;
        }
        return null;
    }

    /// <summary>
    /// Whether motion is wanted: the newest request that mentions motion or stillness decides. Static wording is
    /// checked first because "not moving" contains a motion word.
    /// </summary>
    private static bool AskedForMotion(IReadOnlyList<string> newestFirst)
    {
        foreach (var t in newestFirst)
        {
            if (StaticWords.IsMatch(t)) return false;
            if (MotionWords.IsMatch(t)) return true;
        }
        return false;
    }

    /// <summary>
    /// The newest request that mentions a run effect decides it ("with twinkle" adds it, "no twinkle" removes it).
    /// With no mention, a refine keeps the current pattern's effect and a new pattern gets none: the model is not
    /// trusted to add one unasked.
    /// </summary>
    private static string RequestedRunEffect(IReadOnlyList<string> newestFirst, PatternDetailsDto? current, string? modelEffect)
    {
        foreach (var t in newestFirst)
        {
            foreach (var e in PatternAnimations.RunEffects)
            {
                var m = e.Triggers.Match(t);
                if (!m.Success) continue;
                return RemoveWords.IsMatch(t[..m.Index]) ? PatternVocabulary.DefaultRunEffect : e.Name;
            }
        }

        var currentEffect = Canonical(PatternVocabulary.RunEffects, current?.RunData?.Effect, PatternVocabulary.DefaultRunEffect);
        return current is not null && IsEffect(modelEffect, currentEffect) ? currentEffect : PatternVocabulary.DefaultRunEffect;
    }

    private static bool IsType(string? value, string type) =>
        string.Equals(value?.Trim(), type, StringComparison.OrdinalIgnoreCase);

    private const string NoColorTransform = "No Color Transform";
    private const string Repeat = "Repeat";
    private const string FillWithBlack = "Fill with Black";
    private const string Lightning = "Lightning";

    /// <summary>
    /// Matches what the lights do (measured on the controller, 2026-11-18): with No Color Transform every color is
    /// one light and spaceBetweenPixels is ignored, so a wider hard-edged block means Repeat. Repeat, Fade and
    /// Progression use spaceBetweenPixels as lights per color (at least 1); Fill with Black uses it as the
    /// number of unlit lights after each color (0 allowed).
    /// </summary>
    internal static (string Effect, int Space) NormalizeSpacing(string effect, int space)
    {
        if (IsEffect(effect, NoColorTransform)) return space > 1 ? (Repeat, space) : (NoColorTransform, 1);
        if (IsEffect(effect, FillWithBlack)) return (effect, space);
        return (effect, Math.Max(1, space));
    }

    private static bool IsEffect(string? value, string effect) =>
        string.Equals(value?.Trim(), effect, StringComparison.OrdinalIgnoreCase);

    private static int Channel(int v) => Math.Clamp(v, PatternVocabulary.MinColorChannel, PatternVocabulary.MaxColorChannel);

    private static string Canonical(IReadOnlyList<string> allowed, string? value, string fallback) =>
        allowed.FirstOrDefault(a => string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? fallback;

    /// <summary>Pattern names are URL path segments, so slashes are removed.</summary>
    private static string CleanName(string? name)
    {
        var cleaned = CleanText(name?.Replace('/', ' ').Replace('\\', ' '), AssistantPatternSchema.MaxNameLength);
        return cleaned.Length == 0 ? DefaultName : cleaned;
    }

    private static string CleanText(string? text, int max)
    {
        var t = string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length <= max ? t : t[..max].TrimEnd();
    }
}
