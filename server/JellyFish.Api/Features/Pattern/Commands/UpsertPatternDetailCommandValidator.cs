using FluentValidation;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed class UpsertPatternDetailCommandValidator : AbstractValidator<UpsertPatternDetailCommand>
{
    public UpsertPatternDetailCommandValidator()
    {
        RuleFor(x => x.FolderName).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Details).NotNull();

        When(x => x.Details is not null, () =>
        {
            RuleFor(x => x.Details).SetValidator(new PatternDetailsValidator());
        });
    }
}

