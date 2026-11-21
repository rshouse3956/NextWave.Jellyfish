using FluentValidation;

namespace JellyFish.Api.Features.Pattern.Commands;

public sealed class DeletePatternDetailCommandValidator : AbstractValidator<DeletePatternDetailCommand>
{
    public DeletePatternDetailCommandValidator()
    {
        RuleFor(x => x.FolderName).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}

