using FluentValidation;

namespace JellyFish.Api.Features.Pattern;

/// <summary>
/// Structural rules the controller requires to parse pattern data (color triples, ranges).
/// Safe to apply when running stored patterns: data that fails these rules would not run correctly anyway.
/// </summary>
public sealed class PatternDetailsStructureValidator : AbstractValidator<PatternDetailsDto>
{
    public PatternDetailsStructureValidator()
    {
        RuleFor(x => x.Colors)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Colors are required")
            .Must(c => c.Length > 0).WithMessage("At least one color is required")
            .Must(c => c.Length % 3 == 0).WithMessage("Colors must be RGB triples (length must be a multiple of 3)")
            .Must(c => c.Length <= PatternVocabulary.MaxColorValues)
                .WithMessage($"A pattern can have at most {PatternVocabulary.MaxColors} colors ({PatternVocabulary.MaxColorValues} values)");
        RuleForEach(x => x.Colors)
            .InclusiveBetween(PatternVocabulary.MinColorChannel, PatternVocabulary.MaxColorChannel)
            .WithMessage($"Color values must be between {PatternVocabulary.MinColorChannel} and {PatternVocabulary.MaxColorChannel}");

        RuleFor(x => x.SpaceBetweenPixels).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.NumOfLeds).GreaterThanOrEqualTo(0);

        RuleFor(x => x.RunData).NotNull().WithMessage("RunData is required");
        When(x => x.RunData is not null, () =>
        {
            RuleFor(x => x.RunData.Speed).GreaterThanOrEqualTo(0);
            RuleFor(x => x.RunData.Brightness)
                .InclusiveBetween(PatternVocabulary.MinBrightness, PatternVocabulary.MaxBrightness)
                .WithMessage($"Brightness must be between {PatternVocabulary.MinBrightness} and {PatternVocabulary.MaxBrightness}");
            RuleFor(x => x.RunData.RgbAdj)
                .Must(r => r is null || r.Length == 3)
                .WithMessage("RgbAdj must have exactly 3 values");
        });
    }
}

/// <summary>
/// Full rules for user-authored pattern data: structure plus the documented values for
/// type, effectBetweenPixels and direction.
/// </summary>
public sealed class PatternDetailsValidator : AbstractValidator<PatternDetailsDto>
{
    public PatternDetailsValidator()
    {
        Include(new PatternDetailsStructureValidator());

        RuleFor(x => x.Type)
            .Must(PatternVocabulary.IsKnownType)
            .WithMessage($"Type must be one of: {string.Join(", ", PatternVocabulary.Types)}");
        RuleFor(x => x.EffectBetweenPixels)
            .Must(PatternVocabulary.IsKnownEffectBetweenPixels)
            .WithMessage($"EffectBetweenPixels must be one of: {string.Join(", ", PatternVocabulary.EffectsBetweenPixels)}");
        RuleFor(x => x.Direction)
            .Must(PatternVocabulary.IsKnownDirection)
            .WithMessage($"Direction must be one of: {string.Join(", ", PatternVocabulary.Directions)}");
    }
}
