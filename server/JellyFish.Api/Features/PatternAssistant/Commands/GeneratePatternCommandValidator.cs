using FluentValidation;
using JellyFish.Api.Features.Pattern;

namespace JellyFish.Api.Features.PatternAssistant.Commands;

public sealed class GeneratePatternCommandValidator : AbstractValidator<GeneratePatternCommand>
{
    public const int MaxPromptLength = 500;
    public const int MaxHistory = 10;

    public GeneratePatternCommandValidator()
    {
        RuleFor(x => x.Prompt)
            .NotEmpty().WithMessage("Describe the pattern you want")
            .MaximumLength(MaxPromptLength);

        RuleFor(x => x.CurrentName).MaximumLength(100);

        RuleFor(x => x.History)
            .Must(h => h is null || h.Count <= MaxHistory)
            .WithMessage($"At most {MaxHistory} earlier requests can be sent");
        RuleForEach(x => x.History).NotEmpty().MaximumLength(MaxPromptLength);

        // Structure only: a stored controller pattern may use values the API doc doesn't list.
        When(x => x.CurrentDetails is not null, () =>
        {
            RuleFor(x => x.CurrentDetails!).SetValidator(new PatternDetailsStructureValidator());
        });
    }
}
