using FluentValidation;
using JellyFish.Api.Features.Pattern;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed class RunPatternCommandValidator : AbstractValidator<RunPatternCommand>
{
    public RunPatternCommandValidator()
    {
        RuleFor(x => x.FolderName).NotEmpty().WithMessage("FolderName is required");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
        RuleFor(x => x.State).InclusiveBetween(0, 1).WithMessage("State must be 0 (off) or 1 (on)");
        RuleFor(x => x.ZoneNames)
            .NotNull().WithMessage("ZoneNames are required")
            .Must(z => z.Count > 0).WithMessage("At least one zone name is required");
        RuleForEach(x => x.ZoneNames).NotEmpty().WithMessage("Zone name cannot be empty");

        // Structure only (not type/effect/direction values): schedules also run stored patterns through
        // this command, and those must not break if the controller uses values the API doc doesn't list.
        When(x => x.Data is not null, () =>
        {
            RuleFor(x => x.Data!).SetValidator(new PatternDetailsStructureValidator());
        });
    }
}
