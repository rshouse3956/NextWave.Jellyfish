using System.Text;
using System.Text.Json;
using JellyFish.Api.Features.Pattern;
using JellyFish.Api.Features.PatternAssistant.Commands;
using Microsoft.Extensions.AI;

namespace JellyFish.Api.Features.PatternAssistant;

/// <summary>
/// A stored pattern shown to the model as an example. <see cref="FromController"/>: synced from the controller
/// (known to render well) rather than created in the app, where it may be hand-edited or saved assistant output.
/// </summary>
public sealed record PatternExample(string FolderName, string Name, PatternDetailsDto Details, bool FromController = true);

/// <summary>Builds the chat messages for a generate or refine request.</summary>
public static class PatternAssistantPrompt
{
    /// <summary>The system prompt; the animation list comes from <see cref="PatternAnimations"/>.</summary>
    public static readonly string SystemInstructions = SystemTemplate.ReplaceLineEndings("\n").Replace(
        "{animations}\n", PatternAnimations.PromptSection().ReplaceLineEndings("\n"));

    private const string SystemTemplate = """
        You design lighting patterns for a Jellyfish permanent outdoor LED lighting controller. The lights are a
        long line of individually addressable RGB pixels along a roofline. Reply with one pattern as JSON matching
        the given schema, and nothing else.

        Fields:
        - colors: the palette, 1 to 30 colors as {r,g,b} (0-255). Colors repeat along the line in order. Use
          saturated, real-world colors (e.g. warm white 255,180,100; red 255,0,0). Black {0,0,0} is an unlit
          pixel and can be used for gaps. Use brightness, not darker colors, to dim the whole pattern.
          List each color once and set its block width with spaceBetweenPixels. Only list a color several times
          when blocks need different widths (e.g. 3 red then 1 white: [red, red, red, white], width 1).
        {animations}
        - effectBetweenPixels and spaceBetweenPixels work together (measured on these lights). Use Fade or
          Progression only when the request asks for fading, blending, a gradient or a rainbow; otherwise use
          Repeat (or Fill with Black for gaps).
          Repeat = hard-edged blocks, each color spaceBetweenPixels lights wide. Use it for stripes and blocks.
          Fade = each color spaceBetweenPixels lights wide; where one color changes to the next, the lights dim
            down and back up (no in-between colors). Use it for "fading" between colors. A moving fade needs at
            least 4 lights per color to be visible (2 shows nothing).
          Fill with Black = each color is 1 light followed by spaceBetweenPixels unlit lights (use only when
            gaps are asked for).
          No Color Transform = each color exactly 1 light; spaceBetweenPixels is ignored.
          Progression = each color spaceBetweenPixels lights wide, changing into the next through the colors in
            between on the color wheel and staying bright (green to red passes through yellow and orange). Use it
            for blend, gradient, rainbow and spectrum requests.
        - spaceBetweenPixels: lights per color (or the gap length for Fill with Black). When the request gives
          a count per color ("2 green and 2 red", "blocks of 3", "5 lights each"), use exactly that count.
          The number before a color is that color's width, never a total: "a block of 2 green and 2 red" means
          spaceBetweenPixels 2, not 4.
          Otherwise 3-8 gives visible blocks (stripes, candy cane, team colors); 10-20 gives long sweeps of
          color. Fade and Progression need at least 4 to show a gradient; use 10-20 for rainbows and long
          gradients. For one solid color it doesn't matter.
        - skip: pixels moved per animation step (1 = smooth; Paint usually uses 2). numOfLeds: the Multi-Paint
          section size, usually 6-10 (use 1 for every other type).
        - speedLevel: 1 = slowest, 10 = fastest. Ignored by static patterns.
        - brightness: percent, 1-100. Use 100 unless asked for dim or subtle lighting.
        - direction: Left, Right, or Center (moves outward from the middle).
        - suggestedName: a short Title Case name, no slashes. explanation: one or two sentences describing it.

        Holiday and team requests mean their well-known colors (e.g. Christmas = red, green, white).

        How requests map to fields (illustrations, not templates to copy):
        - "blue and white blended together, 6 lights each" -> colors [blue, white], spaceBetweenPixels 6,
          effectBetweenPixels Progression, type Color.
        - "orange and purple fading into each other" -> colors [orange, purple], spaceBetweenPixels 6,
          effectBetweenPixels Fade, type Color.
        - "a fading block of 3 blue and 3 white" -> colors [blue, white], spaceBetweenPixels 3 (3 per color,
          not 6), effectBetweenPixels Fade, type Color.
        - "red and white candy cane stripes chasing slowly" -> colors [red, white], spaceBetweenPixels 4,
          effectBetweenPixels Repeat, type Chase, speedLevel 3.
        - "green with a gap of 3 dark lights" -> colors [green], spaceBetweenPixels 3, effectBetweenPixels
          Fill with Black, type Color.
        - "warm white" -> colors [warm white], type Color, effectBetweenPixels Repeat, spaceBetweenPixels 1.
        - "red and green slowly breathing" -> colors [red, green], type Sequence, effectBetweenPixels Fade,
          speedLevel 3.
        - "paint the roof in fall colors" -> colors [red, orange, yellow, ...], type Paint, effectBetweenPixels
          No Color Transform, skip 2, direction Center.
        - "red, white and blue filling in sections" -> type Multi-Paint, numOfLeds 6.
        - "twinkling warm white" -> colors [warm white], type Color, runEffect Twinkle.
        - "gold and silver New Year's fireworks" -> colors [gold, silver], type Firework.
        """;

    private const string ExamplesHeader =
        "Existing patterns on this controller, showing how the fields combine. The request always decides the " +
        "colors, type, effect and block width; do not copy them from these:";

    public static List<ChatMessage> Build(GeneratePatternCommand request, IReadOnlyList<PatternExample> examples)
    {
        var system = new StringBuilder(SystemInstructions);
        if (examples.Count > 0)
        {
            system.AppendLine().AppendLine(ExamplesHeader);
            foreach (var e in examples)
            {
                system.Append("- ").Append(e.FolderName).Append(" / ").Append(e.Name).Append(": ")
                    .AppendLine(ToJson(PatternDetailsSanitizer.FromDetails(e.Details, e.Name)));
            }
        }

        var messages = new List<ChatMessage> { new(ChatRole.System, system.ToString()) };

        var user = new StringBuilder();
        if (request.History is { Count: > 0 })
        {
            user.AppendLine("Earlier requests for this pattern, oldest first:");
            foreach (var h in request.History) user.Append("- ").AppendLine(h);
            user.AppendLine();
        }

        if (request.CurrentDetails is not null)
        {
            user.AppendLine("Current pattern:")
                .AppendLine(ToJson(PatternDetailsSanitizer.FromDetails(request.CurrentDetails, request.CurrentName)))
                .AppendLine()
                .Append("Change it as follows, keeping everything else the same: ").Append(request.Prompt.Trim());
        }
        else
        {
            user.Append("Create a pattern: ").Append(request.Prompt.Trim());
        }

        messages.Add(new ChatMessage(ChatRole.User, user.ToString()));
        return messages;
    }

    /// <summary>Examples of the requested animation's type shown first, out of the total.</summary>
    public const int MaxAnimationExamples = 2;

    /// <summary>
    /// Picks stored patterns to show the model, from all folders: first up to <see cref="MaxAnimationExamples"/> of
    /// the animation the prompt describes (e.g. Paint for "paint the roof"; those types are rare, so the model
    /// needs to see one), then those whose folder or name shares a word with the prompt (e.g. "christmas"), then
    /// one of each pattern type not yet covered, so the model sees the variety. Controller patterns are used
    /// first; app-made ones (which may include the assistant's own saved output, and so feed its mistakes back)
    /// only fill slots that controller patterns can't. Types the assistant doesn't make (e.g. Soffit) are skipped.
    /// </summary>
    public static IReadOnlyList<PatternExample> SelectExamples(string prompt, IEnumerable<PatternExample> candidates, int max)
    {
        if (max <= 0) return [];
        var usable = candidates
            .Where(c => c.Details?.Colors is { Length: >= 3 } && c.Details.RunData is not null)
            .Where(c => PatternAnimations.Types.Contains(c.Details.Type?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
            .OrderBy(c => c.FolderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var words = Words(prompt);
        var animation = PatternAnimations.Detect(prompt);
        var chosen = new List<PatternExample>();
        foreach (var pool in new[] { usable.Where(c => c.FromController).ToList(), usable.Where(c => !c.FromController).ToList() })
        {
            if (animation is not null)
            {
                bool OfType(PatternExample c) => string.Equals(c.Details.Type, animation.Type, StringComparison.OrdinalIgnoreCase);
                chosen.AddRange(pool
                    .Where(OfType)
                    // the animation's own effect first, e.g. Fade for Sequence with Fade
                    .OrderByDescending(c => animation.Effect is not null &&
                        string.Equals(c.Details.EffectBetweenPixels, animation.Effect, StringComparison.OrdinalIgnoreCase))
                    .Take(Math.Min(max, MaxAnimationExamples) - chosen.Count(OfType)));
            }

            chosen.AddRange(pool
                .Where(c => !chosen.Contains(c))
                .Select(c => (Example: c, Score: Words($"{c.FolderName} {c.Name}").Count(words.Contains)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Example)
                .Take(max - chosen.Count));

            foreach (var c in pool)
            {
                if (chosen.Count >= max) break;
                if (chosen.Contains(c)) continue;
                if (chosen.Any(x => string.Equals(x.Details.Type, c.Details.Type, StringComparison.OrdinalIgnoreCase))) continue;
                chosen.Add(c);
            }
            if (chosen.Count >= max) break;
        }
        return chosen;
    }

    private static HashSet<string> Words(string text) =>
        text.ToLowerInvariant()
            .Split(text.Where(ch => !char.IsLetterOrDigit(ch)).Distinct().ToArray(), StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .ToHashSet();

    private static string ToJson(AssistantPattern p) => JsonSerializer.Serialize(p, AssistantPatternSchema.JsonOptions);
}
